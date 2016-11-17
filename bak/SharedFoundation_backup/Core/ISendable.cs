using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Timers;
using ConnectTo.Foundation.Business;
using ConnectTo.Foundation.Common;
using ConnectTo.Foundation.Helper;
using ConnectTo.Foundation.Messages;
using Newtonsoft.Json;

namespace ConnectTo.Foundation.Core
{
    public interface ICompletable
    {
        event Action<ICompletable> Completed;
        event Action<ICompletable> Started;
    }

    //Message是Sendable，所以需要public
    public interface ISendable:ICompletable
    {
        //Post完毕,但不知道是否已经发送到socket.
        //TODO 重构 改成GetMessageCompleted?
        event Action<ISendable> PostCompleted;
        //数据已经全部交给socket。但不知道发送是否成功。
        event Action<ISendable> SendCompleted;
        //已经收到对方确认，发送成功。
        event Action<ISendable> Errored;
        //一个被发送的Item，在需要对方确认时，等待了若干时间后，仍然没有收到对方的确认消息，触发此事件。
        event Action<ISendable> WaitConfirmTimeouted;
        TransferState TransferState { get; set; }
        /// <summary>
        /// 发送的Item，是否需要对确认才算完毕？
        /// </summary>
        bool NeedConfirm { get; set; }
        bool IsPostCompleted { get; }
        /// <summary>
        /// 如果一个被发送的Item需要对方确认才算完毕，这个值表示当Post完毕后过多久多久才算超时出错。
        /// </summary>
        int WaitConfirmTimeoutMilliSeconds { get; set; }
        /// <summary>
        /// 只有在状态变成PostCompleted或者Error的时候才允许返回Null值。
        /// </summary>
        /// <returns></returns>
        Message GetNextMessage();
    }

    public interface IProgressable:ICompletable
    {
        event Action<IProgressable, long> Progressed;
        long TransferredLength { get; set; }
        void Progress(int length);
        long Length { get; set; }
    }
    internal interface ISeekable
    {
        void SeekTo(long position);
    }

    internal class SendableComparer<T> : IComparer<T>
        where T : ISendable
    {
        //会根据T生成不同的Instance。互不干扰。不用模板类不行，List<Item>, List<Sendable>会要求不同的比较器。
        public static SendableComparer<T> Instance = new SendableComparer<T>();
        internal string Name { get; set; }
        public int Compare(T x, T y)
        {
            if (x is SequencableItem)
            {
                if (y is SequencableItem)
                {
                    //为何强制类型转换不能通过编译？ SequencableItem x2 = (SequencableItem)x;
                    var x1 = x as SequencableItem;
                    var y1 = y as SequencableItem;
                    return x1.DynamicPriority - y1.DynamicPriority;
                }
                else //y是普通Sendable
                {
                    return 1; //x 它后面。
                }
            }
            else
            {
                if (y is SequencableItem)
                {
                    return -1; //x 排在ISplittableSendable 前面。
                }
                else
                {
                    return 0;
                }
            }
        }
    }

    public abstract class SendableBase : ISendable
    {
        public virtual event Action<ISendable> Errored;
        public virtual event Action<ISendable> PostCompleted; 
        public virtual event Action<ISendable> SendCompleted;
        public virtual event Action<ISendable> Canceled;
        public virtual event Action<ISendable> WaitConfirmTimeouted;
        public virtual event Action<ISendable> Confirmed;

        public virtual event Action<ICompletable> Started;
        public virtual event Action<ICompletable> Completed;

        
        [JsonProperty(PropertyName = "length")]
        public long Length { get; set; }
        [JsonProperty(PropertyName = "need_confirm")]
        public bool NeedConfirm { get; set; }
        public bool IsPostCompleted {
            get
            {
                return TransferState != TransferState.Idle && TransferState != TransferState.Transferring;
            }
        }
        public int WaitConfirmTimeoutMilliSeconds { get; set; }
        private TransferState _transferState;
        
        public TransferState TransferState
        {
            get
            {
                lock (this)
                {
                    return _transferState;
                }
            }
            set
            {
                lock (this)
                {
                    if (_transferState == TransferState.Confirmed)
                    {
                        //如果已经变成传输确认状态，就不要再变化了,
                        return;
                    }

                    if (value != _transferState)
                    {
                        var prevState = _transferState;
                        _transferState = value;
                        switch (_transferState)
                        {
                            case TransferState.Transferring:
                                if (prevState == TransferState.Idle)
                                {
                                    Started?.Invoke(this);
                                }
                                break;
                            case TransferState.Error:
                                OnErrored();
                                Errored?.Invoke(this);
                                break;
                            case TransferState.Canceled:
                                OnCanceled();
                                Canceled?.Invoke(this);
                                break;
                            //以下状态对于发送端来说，依次变化。对于接收端来说，仅有Completed状态。
                            case TransferState.PostCompleted:
                                OnPostCompleted();
                                PostCompleted?.Invoke(this);
                                break;
                            case TransferState.SentCompleted:
                                if (this is FileItem || this is ListSequencable)
                                {
                                    Console.WriteLine("test");
                                }
                                SendCompleted?.Invoke(this);
                                break;
                            case TransferState.Completed:
                                
                                OnCompleted();
                                Completed?.Invoke(this);
                                break;
                            case TransferState.WaitingConfirm:
                                waitConfirmTimer = Util.DoLater(() => TransferState = TransferState.WaitConfirmTimeouted, WaitConfirmTimeoutMilliSeconds);
                                break;
                            case TransferState.Confirmed:
                                //需要写这些重复的IsPostCompleted = true;是因为有可能直接进入Confirmed状态。
                                waitConfirmTimer?.Stop();
                                OnConfirmed();
                                Confirmed?.Invoke(this);
                                break;
                            case TransferState.WaitConfirmTimeouted:
                                WaitConfirmTimeouted?.Invoke(this);
                                break;
                        }
                    }
                }
            }
        }

        protected virtual void OnConfirmed() { }

        protected virtual void OnPostCompleted() { }

        protected virtual void OnErrored() { }

        protected virtual void OnCanceled() { }

        protected virtual void OnCompleted() { }
        Timer waitConfirmTimer = null;

        protected SendableBase()
        {
            NeedConfirm = false;
            WaitConfirmTimeoutMilliSeconds = 5000;
        }
        public abstract Message GetNextMessage();

    }

    public enum TransferErrorCode
    {
        FileExistNotEqual,
        CreateFailed,
        WrongPacket,
        NullFileStream,
        ReadFileDataFailed,
        OpenFileError
    }

    [JsonConverter(typeof(ItemJsonConveter))]
    [JsonObject(Newtonsoft.Json.MemberSerialization.OptIn)]
    public abstract class Item : SendableBase, IConversational, IProgressable, ICloneable
    {
        public TransferErrorCode ErrorCode { get; set; }
        public event Action<IProgressable, long> Progressed;
        [JsonProperty(PropertyName = "conversation_id")]
        public string ConversationID { get; set; }
        [JsonProperty(PropertyName = "item_id")]
        public string ID { get; set; } //仅仅get不够，因为在反序列化时需要set.
        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }
        [JsonProperty(PropertyName = "type")]
        public ItemType Type { get; set; }
        //TODO FileItem和DirItem需要计算相对路径
        [JsonProperty(PropertyName = "relative_path")]
        public string RelativePath { get; set; }


        public long TransferredLength { get; set; }
        public string AbsolutePath { get; set; }
        public virtual bool Exists { get; }
        public SequencableItem Parent { get; set; }

        //object itemLocker = new object();
        internal protected Conversation Conversation { get { return AppModel.Instance.Conversations.GetByID(ConversationID); } }

        //仅用于便于子类初始化一些成员。
        protected Item(ItemType type)
        {
            Type = type;
            ID = StringHelper.NewRandomGUID();
        }
        internal void ForceComplete(TransferState state)
        {
            //正常状况下，如果已经IsPostCompleted,就不会更新Length了。
            //异常情况是，当对方通知文件已经存在时，这边不管已经传输多少，直接把状态修改成Confirmed。此时界面上需要直接把进度更新成１００％。
            var tmp = TransferredLength;
            TransferredLength = Length;

            var delta = Length - tmp;
            Progressed?.Invoke(this,delta);
            if (Parent != null)
                ((IProgressable)Parent).Progress((int)delta);
            
            //要放在最后，因为上面Progress会引起父状态变化，而这里会导致递归检查父状态。
            TransferState = state;
        }
        private bool ShouldProgress()
        {
            switch(TransferState)
            {
                case TransferState.Completed:
                case TransferState.Confirmed:
                case TransferState.Canceled:
                case TransferState.Error:
                    return false;
                default:
                    return true;
            }
        }
        void IProgressable.Progress(int len)
        {
            //传送文件的时候，如果文件的状态已经变成了Confirm或者Completed, 再调用Progress是不对的。
            if (!ShouldProgress()) return;

          

            if (TransferredLength == 0)
            {
                TransferState = TransferState.Transferring;
            }

            TransferredLength += len;
            Progressed?.Invoke(this, len);

            if (TransferredLength >= Length)
            {
                TransferState = TransferState.Completed;
                if (TransferredLength  > Length)
                {
                    var msg = "Transfered Lenght > Total Length. some time this does happens, and the transfer is success.";
                    Env.Instance.Logger.Warn(msg);
                }
            }
            //对于DirItem是怎么避免错误的Progress的？FileItem会导致父容器Progress。

            //容器类的Progress被直接调用是错误的。应该定义一个ProgressableContainer。只有一个具体的Item才能驱动容器的进度。
            //现在FileItem的Parent还不一定是DirItem,可以是ListSequencable。应该简化此处的结构，让Parent只有一种可能性。应该考虑让DirItem直接实现ListSequencable。

            if (Parent != null)
                ((IProgressable)Parent).Progress(len); 
        }
        protected override void OnConfirmed()
        {
            //Console.WriteLine("-----Name=" + Parent.Name + " TransferedLength=" + Parent.TransferredLength + " Name=" + Name);


            if (Parent != null)
            {
                if (Parent.TransferState == TransferState.Completed && Parent.NeedConfirm) //目录没有机会变成WaitForConfirm状态。只有文件有。
                {
                    Parent.TransferState = TransferState.Confirmed;
                }
            }

        }
        public abstract object Clone();
    }
}
