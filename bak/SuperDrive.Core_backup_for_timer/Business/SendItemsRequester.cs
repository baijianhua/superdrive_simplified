using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ConnectTo.Foundation.Core;
using ConnectTo.Foundation.Messages;
using NLog;

namespace ConnectTo.Foundation.Business
{
    public class SendItemsRequester : Requester, IProgressable, ICancellable
    {
        private List<Item> _items;
        //为什么需要单独定义它？因为PostCompleted之后，holder里面就没有了。而且，如果子文件夹中的文件传输出错，那么_items里面是没有的
        //都可以放在这个里面。当发生了这样的事情，界面可以来这里取那些没有发送成功的东西，重新发送，重新发送的行为仍然可以使用当前会话，如果对方
        //没有关闭程序，不需要再次确认接收。

        //等到程序运行完毕，这个里面剩下的是出错，或者等待确认超时的Item.
        public List<Item> ProcessingItems { get; private set; }

        private ListSequencable holder = null; //帮助实现Progressable.

        /// <summary>
        /// 注意！！！！！！ 如果要监控各个文件的传输进度，一定记得设置Items之后，遍历Items来做，因为这里进行了深拷贝。
        /// </summary>
        public List<Item> Items {
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
                //深度拷贝。只有这样，同一个文件发送给多个目标，进度等状态信息才能独立跟踪。事件才能独立处理。
                _items = new List<Item>();
                value.ForEach(tmp => 
                {
                    Item item = (Item)tmp.Clone();
                    item.NeedConfirm = item.Length ==0 ? false: true;
                    item.WaitConfirmTimeoutMilliSeconds = 10000; //等待确认时间为10秒钟
                    item.RelativePath = "";
                    item.ConversationID = ID;
                    _items.Add(item);
                });

                //这样做之后，所有的进度信息、开始结束事件，都可以绑到holder上面了。但注意构造holder时，那里面不要做深拷贝。否则外面对这些item的绑定都无效了。
                holder = new ListSequencable(ID, _items);
                holder.StopWhenSubItemError = false;//如果一个子项目出错了，还继续传输。传输错误的item会放在PendingItem里面。
                holder.Name = "holder";
                holder.RelativePath = "Test";
                holder.Started += o => Started?.Invoke(this);
                //还缺一个holder.PostCompleted的监控。
                //holder.Completed += o => Completed?.Invoke(this);
                //holder.Confirmed += o => Completed?.Invoke(this);
                holder.Progressed += (o,v) => Progressed?.Invoke(this,v);
                holder.Errored += (o)=>Errored?.Invoke(this);
            }
        }

        public event Action<IProgressable, long> Progressed;
        public event Action<ICompletable> Started;
        public event Action<ICompletable> Completed;
        public long TransferredLength
        {
            get
            {
                return holder.TransferredLength;
            }

            set
            {
                holder.TransferredLength = value;
            }
        }

        public long Length
        {
            get
            {
                return holder.Length;
            }

            set
            {
                throw new NotSupportedException("Do not support set length on sender.");
            }
        }



        //速度可以计算出来？用传送开始时间、传输的数据量，再用一个timer定时更新速度就可以了。更新总的速度，或者一个会话的速度都可以。
        //public event Action<int> SpeedChanged;
        internal protected override void OnInitRequest()
        {
            SendItemsMessage message = new SendItemsMessage(Items);
            Items.ForEach(item =>
            {
                if (item is FileItem) ProcessingItems.Add(item);
            });
            PostMessage(message);
        }

        internal protected override void OnAgreed()
        {
            PostAsSendable(holder);
        }
        
        //TODO 需要思考一下，这层封装很多余。Items和Datas也是重复的。
        public void Cancel(List<Item> list)
        {
            holder.Remove(list);
            list.ForEach((i) =>
           {
               i.TransferState = TransferState.Canceled;
           });
            var msg = new CancelItemMessage();
            msg.Items = list;
            PostMessage(msg);
        }

        void IProgressable.Progress(int length)
        {
            //无须实现。因为这个主要用的是holder里面的东西。
            throw new NotImplementedException();
        }
        protected internal override void OnRecoverAgreed(ConversationRecoverAgreedMessage recoverResponseMessage)
        {
            //修改holder获取已经有进度的Item,seek到进度位置。其它的全都seek到0，然后重新Post holder.
            var sendItemRecoverResponse = recoverResponseMessage as RecoverSendItemsResponse;
            if (sendItemRecoverResponse == null) return;
            var items = sendItemRecoverResponse.Items;
            //因为所有的item，都是传输完毕确认之后才删除的，所以只要检查已有item即可。
            holder.Items.ForEach((localItem) =>
            {
                if (localItem is ISeekable)
                {
                    var localSeekable = localItem as ISeekable;
                    var result = items.Find(i => i.ID == localItem.ID);
                    //TODO 文件夹怎么处理？还需要再想想。文件夹能Seek吗？不应该吧？
                    if (result == null)
                    {
                        localSeekable.SeekTo(0);
                    }
                    else
                    {
                        localSeekable.SeekTo(result.TransferredLength);
                    }
                }
            });
            PostAsSendable(holder);
        }
        public SendItemsRequester()
        {
            IsAutoRecoverable = true;
            ProcessingItems = new List<Item>();
        }
        long tmpLength = 0;
        int confirmedCount = 0;
        int confirmMsgCount = 0;
        protected internal override void OnMessageReceived(ConversationMessage message)
        {
            if (message is CancelItemMessage)
            {
                CancelItemMessage msg = message as CancelItemMessage;
                msg.Items.ForEach((o) =>
                {
                    var cutItem = Items.FirstOrDefault(d => d.ID == o.ID);
                    if (cutItem != null) cutItem.TransferState = TransferState.Canceled;
                });

                holder.Remove(msg.Items);
            }
            else if (message is ConfirmItemMessage)
            {
                Debug.WriteLine("ConfirmedItem msg count="+ (confirmMsgCount++));
                var cm = message as ConfirmItemMessage;
                //若不复制，Confirmed中的移除动作报错。测试确认一下。
                var tmpProcessingList = new List<Item>(ProcessingItems);
                //Console.WriteLine("Received a confirm message itemid=" + cm.ItemID);
                var item = tmpProcessingList.Find(i => i.ID == cm.ItemID);
                if (item != null)
                {
                    //这个状态改变，会导致item从ProcessingItems中移除。
                    item.ForceComplete(TransferState.Confirmed);
                    //移除动作是在同一线程中，移除完毕会进入这里。
                    if (ProcessingItems.Count() == 0)
                    {
                        //应该还需要做这个检查，有可能存在没有正在处理的文件，但其他也没法送，或者状态不对的情况。
                        if (Items.All(o => o.TransferState == TransferState.Confirmed))
                        {
                            //所有文件成功发送完毕
                            Completed?.Invoke(this);
                        }else
                        {
                            //该做什么？没有正在处理的Item，但也不是所有的Item状态都是Confirmed。出错了..., 如果给对方发了列表，对方马上就回复Confirm，也是0
                        }
                    }
                    else
                    {
                        if (tmpProcessingList.All(o => o.TransferState == TransferState.WaitConfirmTimeouted || o.TransferState == TransferState.Error))
                        {
                            //处理列表中的东西，都是出错 / 等待确认超时的，没办法再处理了。报告错误。上层如果要重新发送，可以到ProcessingItem里面去取。
                            //TODO　只需检查ProcessingItems即可。需要确认一个文件出错，或者等待确认超时，传递到父目录没有？
                            Errored?.Invoke(this);
                        }
                    }
                }
            } else if (message is ReceiveItemErrorMessage)
            {
                var rie = message as ReceiveItemErrorMessage;
                var item = ProcessingItems.FirstOrDefault(i => i.ID == rie.ItemID);
                if(item == null)
                {
                    item = Items.Find(i => i.ID == rie.ID);
                }
                if (item != null)
                {
                    item.ErrorCode = rie.ErrorCode;
                    item.TransferState = TransferState.Error;
                }
            }
        }

        internal override void InternalWormHole(object obj)
        {
            //文件开始发送时，会调用这个函数。所以ProcessingItems里面存储的，是所有尝试开始过的Item,如果成功了，就会从里面移除，不管是主动发送完毕，还是对方已经有这个文件，直接变成完成状态。

            //TODO 现在有个问题没有解决，获取正在等待确认的文件，或者出错的文件，是应该在这个ProcessingItems里面呢，还是应该去Items属性里面？因为这两个是重复的。
            //作为临时的解决方案，可以查找ProcessingItems里面的Item的相对路径是不是"", 如果是，就不要显示，因为Items那里已经显示了。

            //TODO 重构 有没有更好的解决方案？不要冒然删除ProcessingItems，因为Items属性只管理了顶层的文件、文件夹。嵌套的子文件在那里面查找不到。
            if(obj is FileItem)
            {
                //这里的item可以是嵌套很深的文件。在构造requester的时候设置item的回调达不到这个效果，除非遍历子元素。
                //文件等待确认或者出错，会转移到PendingItems里面去。会话会尽量完成能传输完毕的文件，传不成功的(出错或没有得到及时确认）会留在这里。
                var fi = obj as FileItem;
                if (ProcessingItems.Find(i => { return i.ID == fi.ID; }) != null || fi.TransferState == TransferState.Confirmed) return; //如果滥用了InternalWormHole,这样可以避免一下，否则会导致事件被触发多次。

                ProcessingItems.Add(fi);
                fi.Confirmed += (o) => ProcessingItems.Remove(fi);
                fi.Completed += (o) =>
                {
                    if (fi.NeedConfirm)
                    {
                        fi.TransferState = TransferState.WaitingConfirm;
                    }
                };
            }
        }
    }
    
}
