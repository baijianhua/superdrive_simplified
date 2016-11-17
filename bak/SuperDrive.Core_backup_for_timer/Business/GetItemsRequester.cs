using System;
using System.Collections.Generic;
using System.Text;
using ConnectTo.Foundation.Business;
using ConnectTo.Foundation.Core;
using ConnectTo.Foundation.Messages;

namespace ConnectTo.Foundation.Business
{
    public class GetItemsRequester: Requester, IProgressable, ICancellable
    {
        ItemReceiverComponent receiver ;
        private List<Item> _items;
        private string _path;
        public GetItemsRequester()
        {
            receiver = new ItemReceiverComponent(this);
            IsAutoRecoverable = true;
        }
        /// <summary>
        /// 此次请求下载的内容，是在哪个路径之下？
        /// </summary>
        public string Path{
            get
            {
                return _path;
            }
            set
            {
                if (IsStarted)
                {
                    throw new Exception("Can not set path after the transfer has been started");
                }
                else
                {
                    _path = value;
                }
            }
        }
        public List<Item> Items
        {
            get
            {
                return _items;
            }
            set
            {
                //如果会话开始，不再允许赋值。
                if (IsStarted)
                {
                    throw new Exception("Conversation already started. Can not change sending items. Please call AppModel::Create to start another conversation");
                }
                _items = new List<Item>();
                value.ForEach(v =>
                {
                    var o = (Item)v.Clone();
                    o.ConversationID = this.ID;
                    o.Name = v.Name;
                    _items.Add(o);
                });
            }
        }


        public event Action<IProgressable, long> Progressed;
        public event Action<ICompletable> Started;
        public event Action<ICompletable> Completed;

        public long Length
        {
            get
            {
                return receiver.Length;
            }

            set
            {
                throw new NotSupportedException("不支持设置长度。这个值根据下载数据计算得出");
            }
        }

        public long TransferredLength
        {
            get
            {
                return receiver.TransferredLength;
            }

            set
            {
                receiver.TransferredLength = value;
            }
        }

        public string BrowseConversionId { get; set; }

        public string RemotePath { get; set; }

        internal protected override void OnInitRequest()
        {
            //TODO 先检查哪些Items已经存在。直接更新其状态。
            receiver.SaveToPath = Path;
            receiver.PutItems(Items);

            GetItemsMessage message = new GetItemsMessage(Items);
            message.Path = RemotePath;
            message.BrowseId = BrowseConversionId;
            PostMessage(message);

            Items.ForEach(i => receiver.Length += i.Length);

            receiver.Started += o => Started?.Invoke(this);
            receiver.Completed += o => Completed?.Invoke(this);
            receiver.Progressed += (o, v) => Progressed?.Invoke(this, v);
        }
        internal protected override void OnAgreed()
        {
            //不需要做什么事情，等着接收就行了。
        }
        protected internal override void OnRecoverAgreed(ConversationRecoverAgreedMessage recoverResponseMessage)
        {
            //不需要做什么，对方会根据恢复时各个Item的位置，继续发消息过来
        }

        public void Cancel(List<Item> list)
        {
            receiver.Remove(list);
            var msg = new CancelItemMessage();
            msg.Items = list;
            PostMessage(msg);
        }

        protected internal override void OnMessageReceived(ConversationMessage message)
        {
            if (message is FileDataMessage || message is SendItemsMessage)
            {
                receiver.QueueMessage(message);
            }
        }

        public void Progress(int length)
        {
            throw new NotSupportedException("无需再此实现Progress，进度由Receiver提供");
        }

        protected override void OnInitRecover()
        {
            GetItemsRecoverMessage girm = new GetItemsRecoverMessage();
            //获取当前所有的正在传输的Item，切长度不为零的。发送到对端。对端收到后，seek到这个位置，然后重新Post。
            girm.Items = receiver.TransferringItem;
            PostMessage(girm);
        }
        internal override void InternalWormHole(object obj)
        {
            base.InternalWormHole(obj);
            if(obj is FileItem) 
            {
                //如果文件已经存在，若不发送这个消息到对方的话，对方会持续发文件过来。
                //但如果已经做到在发送请求列表之前就剔除，或许不需要？
                //还是不行，因为如果一个文件夹里面有子文件存在，这个逻辑还是需要的
                var fi = obj as FileItem;
                fi.Completed += (o)=>PostMessage(new ConfirmItemMessage(fi.ID));
            }
        }
    }
}
