// Copyright (c) 2017 Alachisoft
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//    http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections;
using System.Data;
using System.Threading;

using Alachisoft.NCache.Common;
using Alachisoft.NCache.Common.Stats;
using Alachisoft.NCache.Common.Util;
using Alachisoft.NCache.Common.Logger;

namespace Alachisoft.NCache.Storage
{
    /// <summary>
    /// This is the base class of all Cache stores. Provides additional optional 
    /// functions that can be overridden as well as default implementation of the
    /// some of the methods in the ICacheStorage interface, wherever possible.
    /// Implements ICacheStorage.
    /// </summary>
    internal class StorageProviderBase : ICacheStorage
    {

        #region /               --- Store Status ---                        /
        /// <summary>
        /// Staus of the Store.
        /// </summary>
        protected enum StoreStatus
        {
            /// <summary> Store has space.</summary>
            HasSpace,

            /// <summary> Store is almost full,but can accomadate some data. </summary>
            NearEviction,

            /// <summary> Store has no space to accomodate new data.</summary>
            HasNotEnoughSpace
        }

        #endregion

        public const uint KB = 1024;
        public const uint MB = KB * KB;
        public const uint GB = MB * KB;
        private string _cacheContext;
        private Boolean _virtualUnlimitedSpace = false;

        /// <summary>
        /// The default starting capacity of stores.
        /// </summary>
        protected readonly int DEFAULT_CAPACITY = 25000;

        /// <summary>
        /// The default percentage of the extra data which can be accomdated.
        /// </summary>
        protected readonly double DEFAULT_EXTRA_ACCOMDATION_PERCENTAGE = 0.20f;

        /// <summary>
        /// Maximam data size, in bytes, that store can hold
        /// </summary>
        private long _maxSize;

        /// <summary>
        ///Size of data which can be accomdated even after we reach max size. 
        /// </summary>
        private long _extraDataSize = 0;

        /// <summary>
        /// Maximam number of object in cache
        /// </summary>
        private long _maxCount;
        /// <summary>
        /// Size of data, in bytes, stored in cache
        /// </summary>
        protected long _dataSize;

        /// <summary>
        /// Reader, writer lock to be used for synchronization.
        /// </summary>
        protected ReaderWriterLock _syncObj;

        protected bool _reportCacheNearEviction = false;
        protected int _evictionReportSize = 0;
        protected int _reportInterval = 5;
        protected DateTime _lastReportedTime = DateTime.MinValue;
        ILogger _ncacheLog;

        private ISizableIndex _iSizableQueryIndexManager = null;
        private ISizableIndex _iSizableExpirationIndexManager = null;
        private ISizableIndex _iSizableEvictionIndexManager = null;

        protected long TotalDataSize
        {
            get
            {
                long temp = _dataSize;

                if (ISizableQueryIndexManager != null)
                    temp += ISizableQueryIndexManager.IndexInMemorySize;
             
                if (ISizableExpirationIndexManager != null)
                    temp += ISizableExpirationIndexManager.IndexInMemorySize;

                if (ISizableEvictionIndexManager != null)
                    temp += ISizableEvictionIndexManager.IndexInMemorySize;

                return temp;
            }
            set
            {
                _dataSize = value;
            }
        }


        public ILogger NCacheLog
        {
            get { return _ncacheLog; }
        }
        
        /// <summary>
        /// Default contructor.
        /// </summary>
        public StorageProviderBase()
            : this(0)
        {
        }

        /// <summary>
        /// Overloaded constructor. Takes the max objects limit, and the listener as parameters.
        /// </summary>
        /// <param name="maxLimit">maximum number of objects to contain.</param>
        public StorageProviderBase(long maxSize)
        {
            _syncObj = new ReaderWriterLock();
            _maxSize = maxSize;
        }
        public StorageProviderBase(IDictionary properties, bool evictionEnabled)
            : this(properties, evictionEnabled, null)
        {
        }
        /// <summary>
        /// Overloaded constructor. Takes the properties as a map.
        /// </summary>
        /// <param name="properties">property collection</param>
        public StorageProviderBase(IDictionary properties, bool evictionEnabled, ILogger NCacheLog)
        {
            Initialize(properties, evictionEnabled);
            _ncacheLog = NCacheLog;

            _evictionReportSize = ServiceConfiguration.CacheSizeThreshold;
            if (_evictionReportSize > 0)
                _reportCacheNearEviction = true;

            _reportInterval = ServiceConfiguration.CacheSizeReportInterval;
        }

        protected void CheckForStoreNearEviction()
        {
            //check for updated Properties in service config
            _evictionReportSize = ServiceConfiguration.CacheSizeThreshold;
            _reportInterval = ServiceConfiguration.CacheSizeReportInterval;

            if (_reportCacheNearEviction && !VirtualUnlimitedSpace)
            {
                if (_lastReportedTime.AddMinutes(_reportInterval) < DateTime.Now)
                {
                    if (_maxSize > 0 && _evictionReportSize > 0)
                    {
                        double currentSizeInPerc = ((double)TotalDataSize / (double)_maxSize) * (double)100;
                        if (currentSizeInPerc >= _evictionReportSize)
                        {
                            ThreadPool.QueueUserWorkItem(new WaitCallback(InformCacheNearEviction));
                            _lastReportedTime = DateTime.Now;
                        }
                    }
                }
            }
        }

        private void InformCacheNearEviction(object state)
        {
            try
            {

                string cacheserver = "NCache";
                long currentSizeInPerc = (TotalDataSize / _maxSize) * 100;
                if (currentSizeInPerc > 100) currentSizeInPerc = 100;
                AppUtil.LogEvent(cacheserver, "Cache '" + _cacheContext + "' has exceeded " + _evictionReportSize + "% of allocated cache size", System.Diagnostics.EventLogEntryType.Warning, EventCategories.Warning, EventID.CacheSizeWarning);
                
                NCacheLog.CriticalInfo("CacheStore", "cache has exceeded " + _evictionReportSize + "% of allocated cache size");
            }
            catch (Exception e)
            {

            }
        }

        /// <summary>
        /// Initialize settings
        /// </summary>
        /// <param name="properties"></param>
        public void Initialize(IDictionary properties, bool evictionEnabled)
        {
            _syncObj = new ReaderWriterLock();
            if (properties == null) return;
            if (properties.Contains("max-size"))
            {
                try
                {
                    _maxSize = ToBytes(Convert.ToInt64(properties["max-size"]));

                    if (evictionEnabled)
                    {
                        //we give user extra cution to add/insert data into the store even
                        //when you have reached the max limit. But if this limit is also reached
                        //then we reject the request.
                        _extraDataSize = (long)(_maxSize * DEFAULT_EXTRA_ACCOMDATION_PERCENTAGE);
                    }
                }
                catch (Exception)
                {
                }
            }
        }

        private long ToBytes(long mbytes)
        {
            return mbytes * 1024 * 1024;
        }

        #region	/                 --- IDisposable ---           /

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or 
        /// resetting unmanaged resources.
        /// </summary>
        public virtual void Dispose()
        {
            _syncObj = null;
            this.Cleared();
        }

        #endregion

        /// <summary>
        /// get or set the maximam size of store, in bytes
        /// </summary>
        public virtual long MaxSize
        {
            get { return _maxSize; }
            set { _maxSize = value; }
        }

        /// <summary>
        /// get or set the maximam number of objects
        /// </summary>
        public virtual long MaxCount
        {
            get { return _maxCount; }
            set { _maxCount = value; }
        }

        /// <summary>
        ///Gets/Sets the cache context used for the Compact serialization framework. 
        /// </summary>
        public string CacheContext
        {
            get { return _cacheContext; }
            set { _cacheContext = value; }
        }
        /// <summary>
        /// get the synchronization object for this store.
        /// </summary>
        public ReaderWriterLock Sync
        {
            get { return _syncObj; }
        }

        #region	/                 --- ICacheStorage ---           /

        /// <summary>
        /// returns the number of objects contained in the cache.
        /// </summary>
        public virtual long Count
        {
            get { return 0; }
        }

        /// <summary>
        /// returns the size of data, in bytes, stored in cache
        /// </summary>
        public virtual long Size
        {
            get { return TotalDataSize; }
        }


        public virtual Array Keys
        {
            get
            {
                return null;
            }
        }

        /// <summary>
        /// Removes all entries from the store.
        /// </summary>
        public virtual void Clear()
        {
        }

        /// <summary>
        /// Determines whether the store contains a specific key.
        /// </summary>
        /// <param name="key">The key to locate in the store.</param>
        /// <returns>true if the store contains an element 
        /// with the specified key; otherwise, false.</returns>
        public virtual bool Contains(object key)
        {
            return false;
        }

        /// <summary>
        /// Get an object from the store, specified by the passed in key. Must be implemented 
        /// by cache stores. 
        /// </summary>
        /// <param name="key">key</param>
        /// <returns>cache entry.</returns>
        public virtual object Get(object key)
        {
            return null;
        }

        /// <summary>
        /// Get the size of item stored in store
        /// </summary>
        /// <param name="key">The key whose items size to get</param>
        /// <returns>Items size</returns>
        public virtual int GetItemSize(object key)
        {
            return 0;
        }

        /// <summary>
        /// Add the key value pair to the store. Must be implemented by cache stores.
        /// </summary>
        /// <param name="key">key</param>
        /// <param name="item">object</param>
        /// <returns>returns the result of operation.</returns>
        public virtual StoreAddResult Add(object key, object item, Boolean allowExtendedSize)
        {
            return StoreAddResult.Failure;
        }

        /// <summary>
        /// Insert the key value pair to the store. Must be implemented by cache stores.
        /// </summary>
        /// <param name="key">key</param>
        /// <param name="item">object</param>
        /// <returns>returns the result of operation.</returns>
        public virtual StoreInsResult Insert(object key, object item, Boolean allowExtendedSize)
        {
            return StoreInsResult.Failure;
        }

        /// <summary>
        /// Removes an object from the store, specified by the passed in key. Must be implemented by cache stores.
        /// </summary>
        /// <param name="key">key</param>
        /// <returns>cache entry.</returns>
        public virtual object Remove(object key)
        {
            return null;
        }

        /// <summary>
        /// Returns a .NET IEnumerator interface so that a client should be able
        /// to iterate over the elements of the cache store.
        /// </summary>
        /// <returns>IDictionaryEnumerator enumerator.</returns>
        public virtual IDictionaryEnumerator GetEnumerator()
        {
            return null;
        }

        #endregion

        /// <summary>
        /// Check if store has enough space to add new item
        /// </summary>
        /// <param name="item">item to be added</param>
        /// <returns>true is store has space, else false</returns>
        protected StoreStatus HasSpace(ISizable item, long keySize, Boolean allowExtendedSize)
        {
            if (VirtualUnlimitedSpace)
                return StoreStatus.HasSpace;

            long maxSize = _maxSize;

            if (!allowExtendedSize)
            {
                maxSize = (long)(_maxSize * .95);
            }
            
            
            //Keysize will be included in actual cachesize
            long nextSize = TotalDataSize + item.InMemorySize + keySize;
            StoreStatus status = StoreStatus.HasSpace;

            if (nextSize > maxSize)
            {
                if (nextSize > (maxSize + _extraDataSize))
                    status = StoreStatus.HasNotEnoughSpace;
                else
                    status = StoreStatus.NearEviction;
            }

            return status;
        }

        /// <summary>
        /// Check if store has enough space to add new item
        /// </summary>
        /// <param name="oldItem">old item</param>
        /// <param name="newItem">new item to be inserted</param>
        /// <returns>true is store has space, else false</returns>
        protected StoreStatus HasSpace(ISizable oldItem, ISizable newItem, long keySize, Boolean allowExtendedSize)
        {

            if (VirtualUnlimitedSpace)
                return StoreStatus.HasSpace;

            long maxSize = _maxSize;

            if (!allowExtendedSize)
            {
                maxSize = (long)(_maxSize * .95);
            }


            long nextSize = TotalDataSize + newItem.InMemorySize - (oldItem == null ? -keySize : oldItem.InMemorySize);
            StoreStatus status = StoreStatus.HasSpace;

           

            if (nextSize > maxSize)
            {
                if (nextSize > (maxSize + _extraDataSize))
                    return StoreStatus.HasNotEnoughSpace;
                return StoreStatus.NearEviction;
            }

            return status;
        }

        /// <summary>
        /// Increments the data size in cache, after item is Added
        /// </summary>
        /// <param name="itemSize">item added</param>
        protected void Added(ISizable item,long keySize)
        {
            _dataSize += (item.InMemorySize + keySize);
        }

        /// <summary>
        /// Increments the data size in cache, after item is inserted
        /// </summary>
        /// <param name="oldItem">old item</param>
        /// <param name="newItem">new item to be inserted</param>
        protected void Inserted(ISizable oldItem, ISizable newItem, long keySize)
        {
            _dataSize += newItem.InMemorySize - (oldItem == null ? -keySize : oldItem.InMemorySize);
        }

        /// <summary>
        /// Decrement the data size in cache, after item is removed
        /// </summary>
        /// <param name="itemSize">item removed</param>
        protected void Removed(ISizable item, long keySize)
        {
            _dataSize -= (item.InMemorySize + keySize);
        }

        /// <summary>
        /// Reset data size when cache is cleared
        /// </summary>
        protected void Cleared()
        {
            TotalDataSize = 0;
        }

        /// <summary>
        /// Returns the thread safe synchronized wrapper over cache store.
        /// </summary>
        /// <param name="storageProvider"></param>
        /// <returns></returns>
        public static StorageProviderBase Synchronized(StorageProviderBase cacheStorage)
        {
            return new StorageProviderSyncWrapper(cacheStorage);
        }

        public bool VirtualUnlimitedSpace
        {
            get
            {
                return _virtualUnlimitedSpace;
            }
            set
            {
                _virtualUnlimitedSpace = value;
            }
        }

        public ISizableIndex ISizableQueryIndexManager 
        {
            get
            {
                return _iSizableQueryIndexManager;
            }
            set
            {
                _iSizableQueryIndexManager = value;
            }
        }

        public ISizableIndex ISizableExpirationIndexManager
        {
            get
            {
                return _iSizableExpirationIndexManager;
            }
            set
            {
                _iSizableExpirationIndexManager = value;
            }
        }

        public ISizableIndex ISizableEvictionIndexManager
        {
            get
            {
                return _iSizableEvictionIndexManager;
            }
            set
            {
                _iSizableEvictionIndexManager = value;
            }
        }

    }
}
