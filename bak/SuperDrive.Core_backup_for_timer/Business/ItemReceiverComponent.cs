using System;
using System.Collections.Generic;
using System.Text;
using ConnectTo.Foundation.Common;
using ConnectTo.Foundation.Core;
using ConnectTo.Foundation.Messages;
using ConnectTo.Foundation.Helper;
using System.Linq;
using System.Diagnostics;
using System.IO;
using NLog;
using SuperDrive.Core.Library;
using SuperDrive.Library;

namespace ConnectTo.Foundation.Business
{
    /// <summary>
    /// 公用的接收文件的组件，只要接收的东西有定义，就会接收所有的item，并在接收发出ItemReceived事件
    /// </summary>
    internal class ItemReceiverComponent : IProgressable
    {
        Dictionary<string, Item> receiveList = new Dictionary<string, Item>();
        

        public ItemReceiverComponent(Conversation conv)
        {
            Conversation = conv;
        }

        public string SaveToPath { get; set; }

        public event Action<IProgressable, long> Progressed;
        public event Action<ICompletable> Started;
        public event Action<ICompletable> Completed;

        internal Conversation Conversation { get; set; }

        //TODO 小心，这么做的前提是不会移除那些已经完成状态的item，只有出错或者取消，才能这么干。
        internal void Remove(Item item)
        {
            if (item == null) return;
            var t = receiveList.GetByID(item.ID);
            if (t != null)
            {
                Length -= t.Length;
                TransferredLength -= t.TransferredLength;

                t.TransferState = TransferState.Canceled;
                if (t.Type == ItemType.Directory)
                    ProcessReceiveListChildItems(t as DirItem);

                receiveList.Remove(item.ID);
            }
        }

        internal void Remove(List<Item> list)
        {
            foreach(var item in list) 
            {
                Remove(item);
            }
        }

        public long TransferredLength { get; set; }

        public long Length { get; set; }
        /// <summary>
        /// 正在传输的进度不为0的Item列表。
        /// </summary>
        public List<Item> TransferringItem { get
            {
                var result = new List<Item>();
                var kvs = receiveList.Where((kv) => kv.Value.TransferredLength > 0).ToList();
                foreach (var kv in kvs) {
                    result.Add(kv.Value);
                }
                return result;
            }
        }

        /// <summary>
        /// 增加一个待接收的项目。只有先放进来，才会允许接收。
        /// </summary>
        /// <param name="sendable"></param>
        internal void PutItem(Item item)
        {
            item.Completed += (o) =>
            {
                //接收完毕
                receiveList.Remove(item.ID);
            };

            if (item is FileItem)
            {
                var fi = item as FileItem;
                Conversation?.InternalWormHole(fi);
                //Env.Instance.Logger.Error("Put fi " + fi.ID + " in to wormhole");
                if (fi.Exists)
                {
                    if (fi.FileInfo.Length == item.Length)
                    {
                        fi.ForceComplete(TransferState.Completed);
                    }
                    else
                    {
                        fi.RenameExistingAndCreateNew();
                        receiveList[item.ID] = item;
                        //fi.ErrorCode = TransferErrorCode.FileExistNotEqual;
                        //fi.TransferState = TransferState.Error;
                    }
                }
                else
                {
                    receiveList[item.ID] = item;
                }
            }
            else
            {
                Conversation?.InternalWormHole(item);
                receiveList[item.ID] = item;
            }
        }

        /// <summary>
        /// 增加一个待接收的项目。只有先放进来，才会允许接收。
        /// </summary>
        /// <param name="items"></param>
        internal void PutItems(List<Item> items)
        {
            items.ForEach((item) =>
            {
                item.AbsolutePath = Util.CombinePath(SaveToPath, item.RelativePath, item.Name);
                PutItem(item);
            });
        }


        object fileStateLocker = new object();
        object locker = new object();
        /// <summary>
        /// 收到之后不一定马上处理，也可以异步的保存到文件中，现在的实现比较简单。不过也许那个实现意义不大。
        /// </summary>
        /// <param name="message"></param>
        internal void QueueMessage(Message message)
        {
            //收到消息后
            if(message is FileDataMessage)
            {
                lock(locker)
                {
                    var fm = message as FileDataMessage;
                    if (fm.Length == 0)
                    {
                        var abstractItem = receiveList.GetByID(fm.ItemID) as AbstractFileItem;
                        if (abstractItem != null)
                        {
                            try
                            {
                                abstractItem.Create();
                            }catch(Exception e)
                            {
                                abstractItem.ErrorCode = TransferErrorCode.CreateFailed;
                                abstractItem.TransferState = TransferState.Error;
                            }
                            ((IProgressable)abstractItem).Progress(0);
                            //TODO 改进 应该尝试将receiver设置为item的Parent。ItemReceverComponent与传输的内容没有父子关系，需要显示调用。
                            Progress(0);
                        }
                    }
                    else
                    {
                        var sendable = receiveList.GetByID(fm.ItemID);

                        if (sendable != null)
                        {
                            if (fm.Data == null || fm.Length != fm.Data.Length)
                            {
                                //收到了一个错误的数据包，无法处理。只能等待等一下重试。
                                sendable.ErrorCode = TransferErrorCode.WrongPacket;
                                sendable.TransferState = TransferState.Error;
                            }
                            else if (sendable is FileItem)
                            {
                                var file = sendable as FileItem;
                                //到单独的线程里面去写文件，保证响应性。那个线程里面的task仍然是顺序执行的。
                                Env.Instance.PostTask(() =>
                                {
#if DEBUG
                                    Env.Instance.CheckFileDataMessage(fm, file);
#endif
                                    //如果是不保证顺序的包发过来，必须按位置写入才可以。
                                    lock (file)
                                    {
                                        if ((file.TransferState == TransferState.Idle || file.TransferState == TransferState.Transferring))
                                        {
                                            file.Write(fm.Offset, (int)fm.Length, fm.Data);
                                            //TODO 改进 参见上一处调用说明。
                                            Progress((int)fm.Length);
                                        }
                                    }
                                });
                            }
                        }
                    }
                }
            }
            else if (message is SendItemsMessage)
            {
                var sim = message as SendItemsMessage;
                var parent = receiveList.GetByID(sim.ParentItemID);
                //只有这一次发的东西的父目录，已经在receiveList当中，才会允许接受这一批内容。
                if (parent is DirItem)
                {
                    sim.Items.ForEach((item) => item.Parent = (DirItem)parent);
                    PutItems(sim.Items);
                }
            }
            else if( message is ItemMessage)
            {
                //还有一种情况，这个Message就是一个Item. ThumbnailMessagey应该是它的子类，获取到这个消息之后，直接调用ItemMessage.Item,就是一个Thumbnail.
                var im = message as ItemMessage;
                Item item = im.Item;
                Item knownItem = receiveList.GetByID(item.ID);
                if(knownItem != null)
                {
                    //TODO 怎么让knownItem变成这个item? 只是想借用它的事件，其实这样已经做到了，因为TransferCompleted传递出去的是Item.但这样看起来很诡异。
                    //这里好像还是有bug.目的是把Item交给用户，让用户使用。用户hold过Items，应该替换那里面的item才对。
                    knownItem.TransferState = TransferState.Completed;
                    receiveList.Remove(item.ID);
                }
            }
        }
        bool isCompleted = false;
        bool isStarted = false;

        public void Progress(int len)
        {
            if (isCompleted) return;

            if (TransferredLength == 0 && !isStarted) 
            {
                isStarted = true;
                Started?.Invoke(this);
            }
            
            TransferredLength += len;
            Progressed?.Invoke(this, len);

            if (TransferredLength == Length)
            {
                isCompleted = true;
                Completed?.Invoke(this);
            }

//            else if (TransferredLength > Length)
//            {
//#if DEBUG
//                Env.Instance.ShowMessage("TransferredLength=" + TransferredLength + "> Length" + Length);
//#endif                
//            }
            
        }

        internal void ProcessReceiveListChildItems(DirItem item)
        {
            if (item == null) return;

            var items = receiveList.Where(o => o.Value != null  && o.Value.Type == ItemType.File);

            if (items.Count() > 0)
            {
                foreach (var i in items)
                {
                    var parent = i.Value.Parent;
                    while (parent != null)
                    {
                        lock (i.Value)
                        {
                            if (parent != null && parent.ID == item.ID)
                            {
                                i.Value.TransferState = item.TransferState;
                                break;
                            }
                            else
                                parent = parent.Parent;
                        }                        
                    }
                }
            }
        }
    }
}
