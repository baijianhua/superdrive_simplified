using System;
using ConnectTo.Foundation.Core;
using ConnectTo.Foundation.Helper;
using ConnectTo.Foundation.Messages;

namespace ConnectTo.Foundation.Business
{
    public enum RequesterState
    {
        Initiating,
        Attached,
        RequestStarting,
        RequestRecovering,

        Running,
        Erroring, //发生了错误，正在等待恢复。
        Pausing,  //现在还没用到，将来可以暂停传输？ 但这个状态只有Progressable才有。Browse不需要。

        Rejected,
        RecoverRejected,
        Finished,
    }
    public abstract class Requester : Conversation
    {
        RequesterState _state;
        public RequesterState State {
            get
            {
                return _state;
            }
            set
            {
                if (_state == value) return;

                _state = value;
                switch (value)
                {
                    case RequesterState.Attached:
                        IsStarted = false;
                        break;
                    case RequesterState.RequestStarting:
                        IsStarted = true;
                        break;
                    case RequesterState.Running:
                        StartTimer();
                        break;
                    case RequesterState.Rejected:
                        //一旦被拒绝后，不在关注设备状态信息，也不再自动发起请求。
                        Peer.Connected -= Device_Connected;
                        Peer.Errored -= Device_Errored;
                        Peer.RemoveAutoConnectRequester(this);
                        break;
                    default:
                        break;
                }
                
            }
        }
        //标记和状态并不冲突。标记是永久性的，一旦标记，一直有效。状态则可以回滚。
        public bool WasAgreed { get; protected set; }
        double timeOut = 5000d;
        private bool _isAutoRecoverable = false;
        internal protected bool IsAutoRecoverable {
            get
            {
                return _isAutoRecoverable;
            }
            set
            {
                if (IsStarted)
                    throw new Exception("会话开始后不能设置IsAutoRecoverable属性");
                else
                {
                    _isAutoRecoverable = value;
                }
            }
        }
        public event Action Rejected;
        public event Action Agreed;
        public event Action RequestTimeouted;
        public event Action RecoverAgreed;
        public event Action<RejectCode> RecoverRejected;

        public override Device Peer
        {
            get
            {
                return base.Peer;
            }

            set
            {
                base.Peer = value;
                value.Connected += Device_Connected;
                value.Errored += Device_Errored;
            }
        }

        private void Device_Errored(Device obj)
        {
            State = RequesterState.Erroring;
        }

        private void Device_Connected(Device obj)
        {
            lock (pendingActionLocker)
            {
                if (PendingAction != null)
                {
                    PendingAction.Invoke();
                    PendingAction = null;
                }
                else
                {
                    //在程序还没退出，连接再次恢复的时候，会自动进入这个函数。
                    if (IsStarted)
                    {
                        if (IsAutoRecoverable)
                        {
                            Recover();
                        }
                        else
                        {
                            //不需要恢复，直接把状态从Error改成Running. 继续处理对方发过来的消息就行了。
                            State = RequesterState.Running;
                        }
                    }
                }
            }
        }

        protected Requester()
        {
            IsAutoRecoverable = false;
#if DEBUG
            timeOut = 300000d;
#endif
        }

        public void Start()
        {
            if (IsStarted) return;

            State = RequesterState.RequestStarting;
            AppModel.AttachConversation(this);
            if(IsAutoRecoverable)
            {
                Peer.AddAutoConnectRequester(this);
            }

            PostPendableAction(OnInitRequest, timeOut
                , ()=> 
                {
                    RequestTimeouted?.Invoke();
                    //如果请求超时了，回滚到初始状态。
                    State = RequesterState.Attached;
                });
        }

        

        public void Recover()
        {
            if (State != RequesterState.Erroring) return;

            State = RequesterState.RequestRecovering; 
            PostPendableAction(OnInitRecover, timeOut, ()=> 
            {
                State = RequesterState.Erroring;
                RequestTimeouted?.Invoke();
            } );
        }
        protected virtual void OnInitRecover()
        {
            ConversationRecoverRequestMessage crrm = new ConversationRecoverRequestMessage();
            PostMessage(crrm);
        }
        /// <summary>
        /// 开始会话后，第一次可以向对方发送消息时候调用
        /// </summary>
        internal protected virtual void OnInitRequest(){}
        internal protected virtual void OnRecoverAgreed(ConversationRecoverAgreedMessage recoverResponseMessage) { }
        internal protected virtual void OnRecoverRejected(ConversationRejectMessage recoverResponseMessage) { }
        internal protected virtual void OnAgreed(){}
        internal protected virtual void OnRejected() { }

        internal protected sealed override bool Process(ConversationMessage message)
        {
            switch(State)
            {
                case RequesterState.RequestRecovering:
                    if (message is ConversationRecoverAgreedMessage)
                    {
                        //对方同意恢复会话。
                        State = RequesterState.Running;
                        OnRecoverAgreed(message as ConversationRecoverAgreedMessage);
                        RecoverAgreed?.Invoke();
                    }
                    else if(message is ConversationRejectMessage)
                    {
                        var rm = message as ConversationRejectMessage;
                        State = RequesterState.Rejected;
                        End();
                        OnRecoverRejected(rm);
                        RecoverRejected?.Invoke(rm.RejectCode);
                    }else{
                        //对方再ConversationAgree或者Reject之前发来的消息，到底要不要处理？怎样才能保证对方不在我准备好之前发消息过来？
                        //其他消息，不理会。
                        OnMessageReceived(message);
                    }
                    break;
                
                case RequesterState.RequestStarting:
                    if (message is ConversationAgreeMessage)
                    {
                        WasAgreed = true;
                        State = RequesterState.Running;
                        Agreed?.Invoke();
                        OnAgreed();
                    }
                    else if (message is ConversationRejectMessage)
                    {
                        State = RequesterState.Rejected;
                        //终止此会话
                        End();
                        Rejected?.Invoke();
                    }
                    else
                    {
                        //对方再ConversationAgree或者Reject之前发来的消息，到底要不要处理？怎样才能保证对方不在我准备好之前发消息过来？
                        //其他消息，不理会。
                        OnMessageReceived(message);
                    }
                    break;
                case RequesterState.Running:
                    OnMessageReceived(message);
                    break;
            }
            return true;
        }

        private object pendingActionLocker = new object();
        private PendableAction _pendingAction;
        //因为访问时需要锁定，所以定义get
        private PendableAction PendingAction
        {
            get
            {
                //如果有人正在DoActionWhenConnectedWithInTime那要等别人修改完毕。不要获取到已经失效的PendingTask.
                lock (pendingActionLocker) return _pendingAction;
            }
            set
            {
                _pendingAction = value;
            }
        }

        /// <summary>
        /// 让这个设备做一件事，尝试直到连接成功，或者超时
        /// 这个事情到底应该是在Conversation里面做，还是在Device里面做？在Conversation里面做才有意义。Device不知道
        /// 当连接的时候，这个事情还可不可以做。
        /// 
        /// 每个Conversation都可以有一个PendableAction,所以如果放在Device里面，Device来限制Conversation
        /// 只有一个PendableAction，那就不合适了。
        /// 
        /// 只有请求端才需要这种行为
        /// </summary>
        /// <param name="action"></param>
        /// <param name="timeout"></param>
        /// <param name="timeoutAction"></param>
        /// <returns>如果连接并执行了，返回true,否则返回false</returns>
        protected void PostPendableAction(Action action, double timeout, Action timeoutAction)
        {
            if (!Peer.Post((device) => action?.Invoke()))
            {
                PendingAction = new PendableAction(Peer, action, timeout, timeoutAction);
            }
        }

        internal override void End()
        {
            base.End();
            Peer.Connected -= Device_Connected;
            Peer.Errored -= Device_Errored;
            State = RequesterState.Finished;
            if (IsAutoRecoverable)
            {
                Peer.RemoveAutoConnectRequester(this);
            }
            PendingAction?.Cancel();
            PendingAction = null;
        }
    }
}