﻿using ConnectTo.Foundation.Core;
using System;
using ConnectTo.Foundation.Messages;
using ConnectTo.Foundation.Helper;
using SuperDrive.Core.Library;
using SuperDrive.Library;
using SuperDrive.Util;

namespace ConnectTo.Foundation.Business
{
    public abstract class Conversation
    {
        private Timer timer;
        internal bool IsStarted { get; set; }
        public string ID { get; set; }
        internal AppModel AppModel { get; set; }
        
        public virtual Device Peer { get; set; }
        public event Action<Conversation> Terminated;
        
        protected bool PostMessage(Message sendable)
        {
            attachConversationID(sendable);
            return Peer.Post(sendable);
        }
        public void Terminate()
        {
            Terminated?.Invoke(this);
            End();
        }
        /// <summary>
        /// 放到低优先级队列
        /// </summary>

        protected bool PostAsSendable(ISendable sendable)
        {
            attachConversationID(sendable);
            return Peer.Post(sendable);
        }
        private void attachConversationID(ISendable sendable)
        {
            if (sendable is IConversational)
            {
                ((IConversational)sendable).ConversationID = ID;
            }
        }
        private int _seconds = 5*60; //默认5分钟超时
        
        //internal ConversationState State { get; set; }
        public event Action<Conversation> Timeout;
        public Action<Conversation> Errored { get; set; }

        internal int TimeoutSeconds
        {
            get
            {
                return _seconds;
            }
            set
            {
                if(value <= 0)
                {
                    throw new ArgumentOutOfRangeException("TimeoutSeconds", "must > 0");
                }

                _seconds = value;
                if (IsStarted) //如果会话已经开始，重新设定计时器。
                {
                    timer = new Timer(TimeSpan.FromSeconds(30),OnTimeoutImpl);
                }
            }
        }
        /// <summary>
        /// 注意，这个方法切勿滥用！！！这个方法的初衷，是为了能够让Conversation中发生的子文件夹中的文件，或者其他子项目，本身没有
        /// 被Conversation直接管理，而当发生问题或者状态变化的时候，有需要让Conversation知道。
        /// 所以任何一个东西，只要让Conversation知道一次就可以了。其他事件变化，应该通过事件来追踪。
        /// 
        /// 如果滥用了这个方法，如果在其中给对象绑定了事件，会被多次绑定，出现莫名其妙的错误。
        /// </summary>
        /// <param name="obj"></param>
        internal virtual void InternalWormHole(object obj) {}
        
        void OnTimeoutImpl()
        {
            //会话超时意味着什么？如果用户想要恢复，可以重新调用Start
            End();
            Timeout?.Invoke(this);
        }
        

        protected internal virtual bool Process(ConversationMessage message)
        {
            //收到一次应答
            timer?.ReStart();
            OnMessageReceived(message);
            return true;
        }

        protected internal virtual void OnMessageReceived(ConversationMessage message)
        {

        }

        

        internal virtual void End()
        {
            timer?.Stop();
            AppModel.RemoveConversation(this);
            
        }

        public void Cancel()
        {
            PostMessage(new CancelConversationMessage());
        }
    }

    public class DummyConversation:Requester
    {
    }

    internal class PendableAction
    {
        private MTask mtask;
        private object locker = new object();
        private Device device;
        private Action action;
        private double timeout;
        private Action timeoutAction;

        
        public PendableAction(Device device, Action action, double timeout, Action timeoutAction)
        {
            this.device = device;
            this.action = action;
            //TODO 是否存在不设置timeout和timeoutAction的情况？
            this.timeout = timeout;
            this.timeoutAction = timeoutAction;
            mtask = new MTask(() =>
            {
                lock (locker)
                {
                    timeoutAction?.Invoke();
                    action = null;
                }
            },null);
            Env.DoLater(mtask,TimeSpan.FromMilliseconds(timeout));
        }

        internal void Cancel()
        {
            mtask?.Cancel();
        }
        internal void Invoke()
        {
            //避免发生超时的回调代码与这个方法同时执行。这里表示要做这件事，超时的则在说不要做了。
            lock (locker)
            {
                mtask.Cancel();
                action?.Invoke();
            }
        }
    }
}
