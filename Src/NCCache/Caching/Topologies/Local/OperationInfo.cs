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

using System.Text;
using Alachisoft.NCache.Common.Net;
using Alachisoft.NCache.Common.Util;
using Alachisoft.NCache.Caching.Queries;

namespace Alachisoft.NCache.Caching.Topologies.Local
{
    public class OperationInfo
    {
        private object _key;
        private CacheEntry _entry;
        private OperationType _opType;

        public OperationInfo(object key, OperationType type)
        {
            _key = key;
            _opType = type;
        }

        internal OperationInfo(object key, CacheEntry entry, OperationType type)
            : this(key, type)
        {
            _entry = entry;
        }

        public object Key
        {
            get { return _key; }
        }

        internal CacheEntry Entry
        {
            get { return _entry; }
        }

        public object OpType
        {
            get { return _opType; }
        }
    }

    #region/                ---Enum LogMode ---          /

    #endregion
}
