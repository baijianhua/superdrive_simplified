using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ConnectTo.Foundation.Business;
using ConnectTo.Foundation.Core;
using ConnectTo.Foundation.Helper;
using ConnectTo.Foundation.Messages;

namespace ConnectTo.Foundation.Business
{
    public class GetItemsResponder: Responder, IProgressable
    {
        List<Item> _items;
        ListSequencable sendHolder;

        internal override ConversationRequestMessage RequestMessage
        {
            set
            {
                var msg = value as GetItemsMessage;
                if (msg != null)
                {
                    base.RequestMessage = value;
                    var tmpItems = msg.Items;
                    var browseResponder = AppModel.Instance.Conversations.GetByID(msg.BrowseId) as BrowseResponder;
                    if (browseResponder == null || !browseResponder.FillItems(tmpItems))
                    {
                        //如果对方请求了一些Item,但我并没有展示这些东西给对方，或者我展示过，但现在失效了，拒绝对方的下载请求。
                        Reject();
                    }
                    Items = tmpItems;
                }
            }
        }

        public event Action<IProgressable, long> Progressed;
        public event Action<ICompletable> Started;
        public event Action<ICompletable> Completed;
        public long TransferredLength
        {
            get
            {
                return sendHolder.TransferredLength;
            }

            set
            {
                sendHolder.TransferredLength = value;
            }
        }

        public long Length
        {
            get
            {
                return sendHolder.Length;
            }

            set
            {
                sendHolder.Length = value;
            }
        }
        
        /// <summary>
        /// 接收方的界面可以直接绑定到各个Item的事件，以更新进度。
        /// </summary>
        public List<Item> Items
        {
            get
            {
                return _items;
            }
            set
            {
                //这样做之后，所有的进度信息、开始结束事件，都可以绑到holder上面了。但注意构造holder时，那里面不要做深拷贝。否则外面对这些item的绑定都无效了。
                _items = value;
                

                sendHolder = new ListSequencable(ID, _items);
                sendHolder.StopWhenSubItemError = false;//如果一个子项目出错了，还继续传输。传输错误的item会放在PendingItem里面。
                sendHolder.Name = "Getholder";
                sendHolder.RelativePath = "Test";
                sendHolder.Started += o => Started?.Invoke(this);
                //还缺一个holder.PostCompleted的监控。
                sendHolder.Completed += o => Completed?.Invoke(this);
                sendHolder.Progressed += (o, v) => Progressed?.Invoke(this, v);
                sendHolder.Errored += (o) => Errored?.Invoke(this);

                _items.ForEach((item) => {
                    Length += item.Length;
                    item.ConversationID = ID;
                });
            }
        }

        internal GetItemsResponder()
        {
        }

        public void Cancel(List<Item> list)
        {
        }

        

        protected override void OnAgreed()
        {
            sendHolder = new ListSequencable(ID,Items);
            //把被请求的item交给底层开始发送。
            PostAsSendable(sendHolder);
        }

        public void Progress(int length)
        {
            //不需要实现
            throw new NotSupportedException("实际进度由sendHolder提供");
        }
        protected internal override void OnMessageReceived(ConversationMessage message)
        {
            if(message is GetItemsRecoverMessage)
            {
                //修改holder获取已经有进度的Item,seek到进度位置。其它的全都seek到0，然后重新Post holder.
                var sendItemRecoverResponse = message as RecoverSendItemsResponse;
                if (sendItemRecoverResponse == null)
                {
                    return;
                }
                var remoteItems = sendItemRecoverResponse.Items;
                //因为所有的item，都是传输完毕确认之后才删除的，所以只要检查已有item即可。
                sendHolder.Items.ForEach((localItem) =>
                {
                    if (localItem is ISeekable)
                    {
                        var localSeekable = localItem as ISeekable;
                        var remoteItem = remoteItems.Find(i=>i.ID == localItem.ID);
                        //TODO 文件夹怎么处理？还需要再想想。文件夹能Seek吗？不应该吧？
                        if (remoteItem == null)
                        {
                            localSeekable.SeekTo(0);
                        }
                        else
                        {
                            localSeekable.SeekTo(remoteItem.TransferredLength);
                        }
                    }
                });
                PostAsSendable(sendHolder);
            }
            else if (message is CancelItemMessage)
            {
                CancelItemMessage msg = message as CancelItemMessage;
                msg.Items.ForEach((o) =>
                {
                    var cutItem = Items.Find(d => d.ID == o.ID);
                    if (cutItem != null) cutItem.TransferState = TransferState.Canceled;
                });

                sendHolder.Remove(msg.Items);
            }
        }
    }
}
