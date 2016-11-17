using System;
using System.Diagnostics;
using System.IO;
using ConnectTo.Foundation.Business;
using ConnectTo.Foundation.Common;
using ConnectTo.Foundation.Helper;
using ConnectTo.Foundation.Messages;
using Newtonsoft.Json;
using NLog;

namespace ConnectTo.Foundation.Core
{
    [JsonObject(Newtonsoft.Json.MemberSerialization.OptIn)]
    public class FileItem : AbstractFileItem, ISeekable
    {
        //无参数构造函数用于反序列化
        public FileItem():base(ItemType.File)
        {
            
        }


        public virtual void RenameExistingAndCreateNew()
        {
        }


        protected override void OnPostCompleted()
        {
            CloseFile();
        }

        protected override void OnErrored()
        {
            CloseFile();
        }

        protected override void OnCanceled()
        {
            CloseFile();
        }

        protected virtual void CloseFile()
        {
            
        }
        protected override void OnCompleted()
        {
            //Env.Instance.Logger.Error("Put fi " + ID + " Completed");
            CloseFile();
        }

        public virtual void Write(long offset, int length, byte[] data)
        {
        }

        public virtual void SeekTo(long position)
        {
        }

        
        public override object Clone()
        {
            return new FileItem()
            {
                ID = this.ID,
                Name = this.Name,
                RelativePath = this.RelativePath,
                Length = this.Length,
            };
        }

        protected override Message GetNextMessageImpl()
        {
            throw new NotImplementedException();
        }
    }
}
