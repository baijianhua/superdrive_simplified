using ConnectTo.Foundation.Channel;
using ConnectTo.Foundation.Common;
using ConnectTo.Foundation.Messages;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using ConnectTo.Foundation.Business;
using ConnectTo.Foundation.Protocol;
using System.Timers;
using ConnectTo.Foundation.Helper;

namespace ConnectTo.Foundation.Core
{
    public class Connection: IDisposable
    {
        private readonly IChannel channel;
        private ConnectionState _state;
        private readonly BackgroundWorker sendWorker;
        private object locker = new object();
        //TODO 这个地方可不可以用ListSequenceSendable实现？现在看不行。ListSendable没有实现Add,Remove,Count等。
        //也用不着实现,因为ListSendable的预期是生成快照，然后完成序列化，不能动态调整它的内容。
        private List<ISendable> sendableList = new List<ISendable>();
        private Queue<Message> messageList = new Queue<Message>();
        private AutoResetEvent notEmptyWaiter = new AutoResetEvent(false); //如果设置为true,第一次队列空的时候会执行。
        private AppModel appModel;
        private System.Timers.Timer timeoutTimer;
        private const double TimeoutInterval = 300000D;

        internal event Action<ConversationMessage,Connection> ConversationMessageReceived;
        internal event Action<Connection> ConnectMessageReceived;
        internal Connection(AppModel appModel, IChannel channel)  
        {
            Preconditions.ArgumentNullException(channel != null && appModel != null);
            //TODO 这里的appModel是被当成Environment用的，只是用到了LocalDevice,应该把AppModel拆成两个东西。
            this.appModel = appModel;
            this.channel = channel;
            channel.PacketReceived += (Packet packet) =>
            {
                Message message = Message.FromPacket(packet);
                if (message != null)
                {
                    OnMessageReceived(message);
                }
                    
            };
            channel.ErrorHappened += (type) => 
            {
                State = ConnectionState.Error;
            };
            _state = ConnectionState.Connecting;
            sendWorker = new BackgroundWorker { WorkerSupportsCancellation = true };
            sendWorker.DoWork += SendWorker_DoWork;
            //TODO应该在第一次Post东西的时候才启动。
            sendWorker.RunWorkerAsync();

            timeoutTimer = new System.Timers.Timer(TimeoutInterval);
            timeoutTimer.Elapsed += OnTimeout;
            //创建之后即开始记录超时。发送消息重置超时。连接完成之后，收到消息也会重置超时。连接被同意之前，收掉的其他消息不会重置超时。
            //不要使用心跳包。如果一个连接长时间不用，就让它关掉。上层如果需要这个connection，会重新创建。
            timeoutTimer.Start();
        }

        internal event Action<ConnectionState> StateChanged;

        public DeviceInfo Peer { get; internal set; }

        internal ConnectionState State
        {
            get { return _state; }
            set
            {

                //对于被动等来的Connection,因为是在Channel创建好之后就创建了，但直到收到ConnectMessage之后才算完整
                //所以这之前收到的东西，全部忽略。
                if (Peer == null) return;
                

                if (value != _state)
                {
                    _state = value;
                    switch (_state)
                    {
                        //这些地方都没有尝试重新连接。
                        case ConnectionState.Disconnected:
                        case ConnectionState.Error:
                        case ConnectionState.Rejected:
                        case ConnectionState.Timeout:
                            //进入这些状态之后，不需要再监视计时。
                            timeoutTimer.Stop();
                            break;
                        default:
                            break;
                    }
                    StateChanged?.Invoke(_state);
                }
            }
        }

        private void OnMessageReceived(Message message)
        {
            if (State == ConnectionState.Connected)
            {
                //只有在连接状态，才重置超时。如果对方在我这里同意连接之前不停的发其它消息，忽略。
                if (message is ConversationMessage)
                {
                    timeoutTimer.ReStart();
                    ConversationMessageReceived?.Invoke((ConversationMessage)message,this);
                }
                else
                {
                    if (message is DisconnectMessage)
                    {
                        State = ConnectionState.Disconnected;
                    }
                }

            }
            else
            {
                if (message is ConnectMessage)
                {
                    timeoutTimer.ReStart();
                    ConnectMessage connetMsg = (ConnectMessage)message;
                    Peer = connetMsg.Device;
                    //TODO 这个地方应该直接打到最上层。甚至高于AppModel
                    ConnectMessageReceived?.Invoke(this);
                }
                else if(message is RejectMessage)
                {
                    if (channel.IsInitiative)
                    {
                        //TODO Trigger connectio rejected event.
                        State = ConnectionState.Rejected;
                    }
                }
                else if(message is AcceptMessage)
                {
                    if (channel.IsInitiative)//不能我是被连接的一方，对方还发来一个Accept消息。
                    {
                        //TODO Trigger connection agreed event.
                        State = ConnectionState.Connected;
                        timeoutTimer.ReStart();
                    }
                    
                }
                else if(message is CancelMessage)
                {
                    //TODO ？？
                }
            }
        }

        private void SendWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            var work = sender as BackgroundWorker;
            if (work == null) return;
            try
            {
                while (!work.CancellationPending)
                {
                    if (messageList.Count == 0 && sendableList.Count == 0)
                    {
                        notEmptyWaiter.WaitOne();
                    }

                    Message message = null;
                    if (messageList.Count > 0)
                    {
                        lock(locker)  message = messageList.Dequeue();
                        SendMessageByChannel(message);
                    }
                    else if (sendableList.Count > 0)
                    {
                        ISendable selectedItem = null;
                        if (sendableList.Count > 0)
                        {
                            lock (locker)
                            {
                                //计算优先级，除了根据splittable的Priority之外，还要根据上次的处理时间，
                                //这样的话，才能做到如果有几个任务同时进行（发文件，收文件，浏览，同步)的时候，每个任务都能得到响应。
                                //但一个splittable内部不考虑上次处理时间。因为同一批次的文件，并行发送没有意义
                                sendableList.Sort(SendableComparer<ISendable>.Instance);
                                selectedItem = sendableList[0];
                            }
                            if (selectedItem is SequencableItem)
                            {
                                var seq = selectedItem as SequencableItem;
                                //上来就尝试读取消息。如果是空文件，返回null。
                                message = seq.GetNextMessage();
                                if (!seq.HasNext || message == null)
                                {
                                    //如果没有内容了，或者读取过程中出错了，都不再尝试继续发消息。
                                    lock (locker) sendableList.Remove(message);
                                }
                                if (!seq.HasNext && message != null)
                                {
                                    message.TransferCompleted.Observers += (msg) => seq.TransferCompleted.Emit(seq);
                                    seq.PostCompleted.Emit(seq);
                                }
                            }
                            else
                            {
                                //不管出不出错，都移除了。
                                message = selectedItem.ToMessage();
                                lock (locker) sendableList.Remove(message);
                            }
                        }
                        //ToMessage或者GetNextMessage可能发生异常。例如文件读取错误
                        if (message != null) 
                        {
                            SendMessageByChannel(message);
                        }

                    }
                }
            }
            finally
            {
                //notEmptyWaiter.Close();
            }           
        }

        private void PostedCompleted(SequencableItem sendable)
        {
            lock (locker) sendableList.Remove(sendable);
            sendable.PostCompleted.Observers -= PostedCompleted;
        }

        //TODO Message队列和Sendable队列其实是可以合并的，但那样的话每次Message也会参与排序。有点浪费。
        internal void Post(ISendable sendable)
        {
            if (sendable == null) return;

            if (sendable is SequencableItem)
            {
                ((SequencableItem)sendable).PostCompleted.Observers += PostedCompleted;
            }
            lock (locker) sendableList.Add(sendable);
            notEmptyWaiter.Set();
        }
        
        internal void Post(Message msg)
        {
            if (msg == null) return;
            //TODO FileDataMessage也是Message,现在这样的话会被优先处理了。
            lock (locker) messageList.Enqueue(msg);
            notEmptyWaiter.Set();
        }

        public void CancelConnect()
        {
            if (channel.IsInitiative && _state == ConnectionState.Connecting)
            {
                CancelMessage msg = new CancelMessage();
                msg.TransferCompleted.Observers += (sendable) => { State = ConnectionState.Canceled; };
                //更应该在Send里面增加一个参数，参数是delegate吧？但message是先被放到队列的，第二个参数该如何处理？所以不如在Message上面增加回调。
                Post(msg);
            }
        }

        public void Accept()
        {
            if (!channel.IsInitiative && _state == ConnectionState.Connecting)
            {
                AcceptMessage msg = new AcceptMessage();
                msg.TransferCompleted.Observers += (sendable) => State = ConnectionState.Connected;
                Post(msg);
            }
        }

        public void Reject()
        {
            if (!channel.IsInitiative && _state == ConnectionState.Connecting)
            {
                RejectMessage msg = new RejectMessage();
                msg.TransferCompleted.Observers += (sendable) => State = ConnectionState.Rejected;
                Post(msg);
            }
        }

        internal void Connect()
        {
            if (_state != ConnectionState.Error && _state != ConnectionState.Disconnected)
            {
                State = ConnectionState.Connecting;
                Post(new ConnectMessage(appModel.LocalDeviceInfo));
            }
        }

        
        internal void Disconnect()
        {
            if (_state == ConnectionState.Connected)
            {
                DisconnectMessage msg = new DisconnectMessage();
                msg.TransferCompleted.Observers += (sendable) => State = ConnectionState.Disconnected;
                Post(msg);
            }
        }
        

        public void Dispose()
        {
            sendWorker.CancelAsync();
            notEmptyWaiter.Set(); //TODO close之前需要set吗？
            //notEmptyWaiter.Close();
        }

        private void SendMessageByChannel(Message message)
        {
            //将消息转化成packet.
            Packet packet = message.ToPacket();
            channel.Send(packet);
            message.TransferCompleted.Emit(null);
        }

        private void OnTimeout(object send, ElapsedEventArgs args)
        {
            State = ConnectionState.Timeout;
        }

        
    }
}
