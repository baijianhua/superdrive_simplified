using System;
using System.Collections.Generic;
using System.Linq;
using ConnectTo.Foundation.Messages;
using System.IO;
using ConnectTo.Foundation.Common;
using Newtonsoft.Json;
using ConnectTo.Foundation.Helper;

namespace ConnectTo.Foundation.Core
{
    [JsonObject(Newtonsoft.Json.MemberSerialization.OptIn)]
    public class DirItem : AbstractFileItem
    {
        //无参数构造函数用于反序列化
        public DirItem():base(ItemType.Directory)
        {
        }

        protected override Message GetNextMessageImpl()
        {
            throw new NotImplementedException();
        }


        public virtual List<AbstractFileItem> GetChildren()
        {
            throw new NotImplementedException();
        }

        public override object Clone()
        {
            return new DirItem()
            {
                ID = this.ID,
                Name = this.Name,
                RelativePath = this.RelativePath,
                Length = this.Length,
            };
        }
    }
}
