using System;
using System.Threading.Tasks;
using SuperDrive.Core.Enitity;
using SuperDrive.Core.Messages;
using SuperDrive.Core.Support;

namespace SuperDrive.Core.Business
{
    public enum RequesterState
    {
        Initiating,
        Attached,
        Requesting,
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

        protected Requester(Device device, string id)
        {
            Peer = device;
            Id = id;

            IsAutoRecoverable = false;
        }
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
                    case RequesterState.Requesting:
                        IsStarted = true;
                        break;
                    case RequesterState.Running:
                        StartTimer();
                        break;
                    case RequesterState.Rejected:
                        //一旦被拒绝后，不在关注设备状态信息，也不再自动发起请求。
                        //Peer.Connected -= Device_Connected;
                        //Peer.Errored -= Device_Errored;
                        Peer.RemoveAutoConnectRequester(this);
                        break;
                }
                
            }
        }
        //标记和状态并不冲突。标记是永久性的，一旦标记，一直有效。状态则可以回滚。
        public bool WasAgreed { get; protected set; }
        private bool _isAutoRecoverable;
        protected internal bool IsAutoRecoverable {
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

        public sealed override Device Peer
        {
            get
            {
                return base.Peer;
            }

            set
            {
                base.Peer = value;
                //value.Connected += Device_Connected;
                //value.Errored += Device_Errored;
            }
        }

       
        protected TaskCompletionSource<ConversationMessage> Response;
        /// <summary>
        ///创建之后还需要start，是因为创建之后还有很多准备工作要做，比如说要发什么文件，要浏览什么目录。
        /// </summary>
        /// <returns>返回ConversationMessage,是因为可以返回有意义的结果，比如说拒绝了，为什么拒绝？比如说同意了浏览了，可以直接把文件列表发过来。</returns>
        public Task<ConversationMessage> Start()
        {
            //取消上一次的调用结果。
            Response?.TrySetResult(new ConversationCancelledMessage());

            Response = new TaskCompletionSource<ConversationMessage>();
            Response.SetValueWhenTimeout(TimeSpan.FromSeconds(Consts.DefaultConnectTimeoutSeconds),new ConversationTimeoutMessage());
            SuperDriveCore.AttachConversation(this);
            State = RequesterState.Attached;

            if(IsAutoRecoverable)
            {
                Peer.AddAutoConnectRequester(this);
            }
            State = RequesterState.Requesting;
            OnInitRequest();
            return Response.Task;
        }

        

        public void Recover()
        {
            if (State != RequesterState.Erroring) return;

            throw new NotImplementedException();

            //State = RequesterState.RequestRecovering; 
            //PostPendableAction(OnInitRecover, _timeOut, ()=> 
            //{
            //    State = RequesterState.Erroring;
            //    RequestTimeouted?.Invoke();
            //} );
        }
        protected virtual void OnInitRecover()
        {
            ConversationRecoverRequestMessage crrm = new ConversationRecoverRequestMessage();
            PostMessageAsync(crrm);
        }



        /// <summary>
        /// 开始会话后，第一次可以向对方发送消息时候调用
        /// </summary>
        protected internal virtual void OnInitRequest(){}
        //声明为inernal，是为了其他类可以调用这个父类的方法。声明为protected,是为了继承者可以重载。
        protected internal virtual void OnRecoverAgreed(ConversationRecoverAgreedMessage recoverResponseMessage) { }
        protected internal virtual void OnRecoverRejected(ConversationRejectMessage recoverResponseMessage) { }

        protected internal sealed override bool Process(ConversationMessage message)
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
                
                case RequesterState.Requesting:
                    if (message is ConversationAgreeMessage)
                    {
                        WasAgreed = true;
                        State = RequesterState.Running;
                        OnRequestAgreed(message as ConversationAgreeMessage);
                        Agreed?.Invoke();
                        if (!Response.IsEnded())
                        {
                            Response?.SetResult(message);
                        }
                        else
                        {
                            End();
                        }
                    }
                    else if (message is ConversationRejectMessage)
                    {
                        if (!Response.IsEnded()) Response?.SetResult(message);
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

        protected virtual void OnRequestAgreed(ConversationAgreeMessage conversationAgreeMessage){}

        internal override void End()
        {
            base.End();
            //Peer.Connected -= Device_Connected;
            //Peer.Errored -= Device_Errored;
            State = RequesterState.Finished;
            if (IsAutoRecoverable)
            {
                Peer.RemoveAutoConnectRequester(this);
            }
        }

        protected virtual void OnRequestTimeouted()
        {
            RequestTimeouted?.Invoke();
        }
    }
}