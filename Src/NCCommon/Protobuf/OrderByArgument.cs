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

//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

// Generated from: OrderByArgument.proto
// Note: requires additional types generated from: Order.proto
namespace Alachisoft.NCache.Common.Protobuf
{
  [global::System.Serializable, global::ProtoBuf.ProtoContract(Name=@"OrderByArgument")]
  public partial class OrderByArgument : global::ProtoBuf.IExtensible
  {
    public OrderByArgument() {}
    

    private string _attributeName = "";
    [global::ProtoBuf.ProtoMember(1, IsRequired = false, Name=@"attributeName", DataFormat = global::ProtoBuf.DataFormat.Default)]
    [global::System.ComponentModel.DefaultValue("")]
    public string attributeName
    {
      get { return _attributeName; }
      set { _attributeName = value; }
    }

    private Alachisoft.NCache.Common.Protobuf.Order _order = Alachisoft.NCache.Common.Protobuf.Order.ASC;
    [global::ProtoBuf.ProtoMember(2, IsRequired = false, Name=@"order", DataFormat = global::ProtoBuf.DataFormat.TwosComplement)]
    [global::System.ComponentModel.DefaultValue(Alachisoft.NCache.Common.Protobuf.Order.ASC)]
    public Alachisoft.NCache.Common.Protobuf.Order order
    {
      get { return _order; }
      set { _order = value; }
    }
    private global::ProtoBuf.IExtension extensionObject;
    global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
      { return global::ProtoBuf.Extensible.GetExtensionObject(ref extensionObject, createIfMissing); }
  }
  
}
