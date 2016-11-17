using ConnectTo.Foundation.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using ConnectTo.Foundation.Messages;
using System.Diagnostics;
using SuperDrive.Library;

namespace ConnectTo.Foundation.Business
{
    public class SendItemsResponder : Responder,IProgressable, ICancellable
    {
        List<Item> _items;
        /// <summary>
        /// 接收方的界面可以直接绑定到各个Item的事件，以更新进度。
        /// </summary>
        public List<Item> Items {
            get
            {
                return _items;
            }
            set
            {
                _items = value;
            }
        }

        ItemReceiverComponent receiver = null;

        internal SendItemsResponder()
        {
            receiver = new ItemReceiverComponent(this);
        }
        public void Cancel(List<Item> list)
        {
            receiver.Remove(list);
            var msg = new CancelItemMessage();
            msg.Items = list;
            PostMessage(msg);
        }

        internal override ConversationRequestMessage RequestMessage
        {
            set
            {
                var msg = value as SendItemsMessage;
                if (msg != null)
                {
                    base.RequestMessage = value;
                    Items = msg.Items;
                    receiver.Length = msg.TotalLength;
                    receiver.Started += o => Started?.Invoke(this);
                    receiver.Completed += o => Completed?.Invoke(this);
                    receiver.Progressed += (o, v) => Progressed?.Invoke(this, v);
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
                return receiver.TransferredLength;
            }

            set
            {
                throw new NotSupportedException("can not set TransferredLength to repsonder mannully");
            }
        }

        public long Length
        {
            get
            {
                return receiver.Length;
            }

            set
            {
                throw new NotSupportedException();
            }
        }

        protected override void OnAgreed()
        {
            receiver.SaveToPath = receiver.SaveToPath ?? Env.Instance.GetRealPath(BrowseLocation.CONNECT2_DEFAULT);
            receiver.PutItems(Items);
        }


        protected internal override void OnMessageReceived(ConversationMessage message)
        {
            if (message is FileDataMessage || message is SendItemsMessage)
            {
                //TODO 这个应该交给一个线程去做。
                receiver.QueueMessage(message);
            }
            else if(message is ConversationRecoverRequestMessage)
            {
                //对方发一个请求，问我正在接收哪些文件。如果我这一边已经异常退出了，那么其实我是没什么信息可以回给对方。
                RecoverSendItemsResponse resp = new RecoverSendItemsResponse();
                resp.SetItems(receiver.TransferringItem);
                PostMessage(resp);
            }
            else if (message is CancelItemMessage)
            {
                CancelItemMessage msg = message as CancelItemMessage;
                msg.Items.ForEach((o) => 
                {
                    var cutItem = Items.FirstOrDefault(d => d.ID == o.ID);
                    if (cutItem != null)
                    {
                        cutItem.TransferState = TransferState.Canceled;
                        if (cutItem.Type == ItemType.Directory)
                        {
                            var dir = cutItem as DirItem;
                            if (dir != null)
                            {
                                receiver.ProcessReceiveListChildItems(dir);
                            }
                        }
                    }
                });
            }
        }

        public void SaveTo(string v)
        {
            receiver.SaveToPath = v;
            Agree();
        }

        void IProgressable.Progress(int length)
        {
            //无需实现。
            throw new NotSupportedException();
        }
        int confirmedCount = 0;
        long confirmedLen = 0;
        internal override void InternalWormHole(object obj)
        {
            if(obj is Item)
            {
                var item = (Item)obj;
                item.Completed += _ =>
                {
                    if (item.NeedConfirm)
                    {
                        Console.WriteLine("confirmed name=" + item.Name + " confirmedLen = " + (confirmedLen += item.Length) + " confirmedCount=" + (confirmedCount++));
                        var msg = new ConfirmItemMessage(item.ID);
                        msg.SendCompleted += __ => item.TransferState = TransferState.Confirmed;
                        PostMessage(msg);
                    }
                };
                
                item.Errored += o =>
                {
                    var msg = new ReceiveItemErrorMessage(item.ID);
                    //TODO 改进 这个做法其实不太好。msg.ErrorCode怎么会是Item的ErrorCode?
                    msg.ErrorCode = item.ErrorCode;
                    receiver.Remove(item);
                    PostMessage(msg);
                };
            }
        }
    }

    
}
