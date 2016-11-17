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
        DirectoryInfo curDir = null;
        
        ListSequencable listSequenceSendable = null;
        List<AbstractFileItem> children = null;
        //无参数构造函数用于反序列化
        public DirItem():base(ItemType.Directory)
        {
        }
        public DirItem(DirectoryInfo dir):this()
        {
            Preconditions.ArgumentException(dir != null);
            curDir = dir;
            Name = dir.Name;
            AbsolutePath = dir.FullName;
            Length = curDir.GetLength();
            //getChildren().ForEach(item => _length += item.Length);
        }
        bool createFolderMessageSent = false;

        protected override Message GetNextMessageImpl()
        {
            Message message = null;
            if (listSequenceSendable == null)
            {
                var tmp = new List<Item>();
                //因为3.5不支持协变，所以需要这个转化。
                GetChildren().ForEach(item =>tmp.Add(item));
                listSequenceSendable = new ListSequencable(ConversationID, tmp);
                listSequenceSendable.Name = this.AbsolutePath;
                listSequenceSendable.PostCompleted += o => TransferState = TransferState.PostCompleted;
                listSequenceSendable.Errored += o => TransferState = TransferState.Error;

                SendItemsMessage sendItemsMessage = new SendItemsMessage(tmp);
                //!!!!!!!!!注意：这里看起来很奇怪。原因是虽然添加到List中，但希望他们的Parent还是这个DirItem. 
                //TODO 重构 如果让DirItem实现ListSequenceSendable呢？
                GetChildren().ForEach(item => {
                    item.Parent = this;
                    if(item is FileItem)
                    {
                        Conversation?.InternalWormHole(item);
                    }
                });
                sendItemsMessage.ParentItemID = ID;
                sendItemsMessage.ConversationID = ConversationID;
                message = sendItemsMessage;
            }
            else
            {
                if (!createFolderMessageSent)
                {
                    FileDataMessage fm = new FileDataMessage()
                    {
                        ItemID = ID,
                        Length = 0,
                        Data = new byte[0]
                    };
                    fm.ConversationID = ConversationID;
                    message = fm;
                    //TODO 注意 如果这个文件夹是空的，这个会导致状态变化。只有长度为0，才能允许调用Progress。否则的话，会导致重复计算进度。
                    ((IProgressable)this).Progress(0);
                    createFolderMessageSent = true;
                }
                else
                {
                    //因为这个类监听了list的状态变化，当list状态变化时，这个类的状态也会变化。
                    message = listSequenceSendable.GetNextMessage();
                }
            }
            return message;
        }


        public List<AbstractFileItem> GetChildren()
        {
            if (children == null)
            {
                children = new List<AbstractFileItem>();
                if (CheckCurDir())
                {
                    AbstractFileItem child;
                    curDir.GetDirectories().ToList().ForEach(s =>
                    {
                        child = new DirItem(s);
                        AppendChild(child);
                    });

                    curDir.GetFiles().ToList().ForEach(s =>
                    {
                        child = new FileItem(s);
                        AppendChild(child);
                    });
                }
            }
            return children;
        }

        bool CheckCurDir()
        {
            if (curDir != null && curDir.Exists)
                return true;
            else
            {
                if( curDir == null && AbsolutePath != null )
                {
                    curDir = new DirectoryInfo(AbsolutePath);
                    return true;
                }
                else
                {
                    throw new System.Exception("Error DirItem.");
                }
            }
        }

        void AppendChild(AbstractFileItem child)
        {
            child.Parent = this;
            child.RelativePath = Util.CombinePath(RelativePath,Name);
            child.ConversationID = ConversationID;
            child.NeedConfirm = NeedConfirm;
            children.Add(child);
        }

        public override object Clone()
        {
            if (curDir != null)
                return new DirItem(curDir);
            else
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
