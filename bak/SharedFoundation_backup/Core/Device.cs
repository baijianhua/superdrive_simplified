using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using ConnectTo.Foundation.Business;
using ConnectTo.Foundation.Channel;
using ConnectTo.Foundation.Helper;
using ConnectTo.Foundation.Messages;
using Newtonsoft.Json;
using ConnectTo.Foundation.Protocol;
using System.Diagnostics;
using NLog;
using System.Linq;
using ConnectTo.Foundation.Common;
using System.Net.Sockets;
using ConnectTo.Foundation.Discovery;
using System.Net;
using Connect2.Foundation.Security;


namespace ConnectTo.Foundation.Core
{
    [JsonObject(MemberSerialization.OptIn)]
    public class Device : ICloneable
    {
        private const double RETRY_INTERVAL = 2000d;
        private const double MAX_CONNECT_WAIT_TIME = 5000d;
        string tmpIDForDebug = StringHelper.NewRandomGUID();
        public int ErrorCount { get; private set; }
        [JsonProperty(PropertyName = "id")]
        public string ID { get; set; }
        //用户名。如Wallace.
        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }
        [JsonProperty(PropertyName = "avatar")]
        public Avatar Avatar { get; set; }
        [JsonProperty(PropertyName = "is_secured")]
        internal bool IsSecured { get; set; }
        [JsonProperty(PropertyName = "bluetooth_mac")]
        public string BluetoothMac { get; set; }
        public bool IsFirstSeen { get; internal set; }
        //随机生成一个ChallengeCode,并且发送给对方，要求对方加密
        public string ChallengeRequest { get; set; }
        /// <summary>
        /// 在接收端，如果对方发的ConnectMessage里面，含有ConnectCode,并且这个ConnectCode和LocalDevice的ConnectCode相同，则自动接受这个链接请求。
        /// </summary>
        [JsonProperty]
        public string ConnectCode { get; set; } = null;

        //TODO IMPROVE SessionCode和ChallengeRequest能不能用同一个值？
        [JsonProperty]
        public string SessionCode { get; set; } = null;
        //除非在二维码，这个值不可暴露。
        SecurePassword _channelCode;

        public SecurePassword ChannelCode
        {

            get
            {
                return _channelCode;
            }
            set
            {
                if (value != null)
                    Crypto = Env.Instance.SecurityManager.CreateCrypto(value);
                _channelCode = value;
            }
        }

        private ISecureCrypto Crypto { get; set; }

        [JsonProperty(PropertyName = "ip_address")]
        public string IPAddress { get; set; }
        /// <summary>
        /// 这个是设备APP的版本
        /// </summary>
        [JsonProperty(PropertyName = "version")]
        internal string Version { get; set; }
        /// <summary>
        /// 设备的当前工作语言
        /// </summary>
        [JsonProperty(PropertyName = "language")]
        internal string Language { get; set; }
        [JsonProperty(PropertyName = "os")]
        internal string OS { get; set; }
        [JsonProperty(PropertyName = "os_version")]
        internal System.Version OSVersion { get; set; }
        //设备名称，如A530, iphone 6s
        [JsonProperty(PropertyName = "device_name")]
        public string DeviceName { get; set; }
        /// <summary>
        /// 设备是PC还是手机？
        /// </summary>
        [JsonProperty(PropertyName = "device_type")]
        public DeviceType DeviceType { get; set; }
        private UdpClient udpclient;


        //这个不需要序列化，是动态变化的，根据收到的广播信息，或者当前连接进来的信息。
        private string _defaultIP = "";
        public string DefaultIP
        {
            get
            {
                return _defaultIP;
            }
            set
            {
                if (value != _defaultIP)
                {
                    _defaultIP = value;
                    if (!string.IsNullOrEmpty(value))
                    {
                        if (udpclient != null)
                        {
                            return;
                        }
                        try
                        {
                            udpclient = new UdpClient();
                            udpclient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, 1);
                            udpclient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, 1);
                            udpclient.Client.Bind(new IPEndPoint(System.Net.IPAddress.Any, LANDiscoverer.DefaultOutPort));
                        }
                        catch (Exception e)
                        {
                            Env.Instance.Logger.Error(e, "Can not create udp client for device" + Name);
                        }
                    }
                }
            }
        }
        public bool IsRequester { get; private set; }
        internal DiscoveryType DiscoveryType { get; set; }
        internal System.Timers.Timer ConnectingOutTimer { get; set; }
        internal bool IsOnline { get; set; }

        internal bool IsValid
        {
            get
            {
                return !string.IsNullOrEmpty(ID) && !string.IsNullOrEmpty(Name);
            }
        }

        private DeviceState _state;
        //private bool wasAcceptted = false;
        //同一个Device，可能会有多个Channel，例如同时存在的wifi,蓝牙,LAN,Internet通道。不仅在发现的时候要考虑，通讯的时候也要考虑。
        private ChannelWrapper channelWrapper;
        private System.Timers.Timer connectedTimer;
        private bool wasConnectedAtLeastOnce = false;
        private object deviceLocker = new object();

        public event Action<Device> Connected = delegate { };
        public event Action<Device> Errored = delegate { };
        public event Action<Device, bool> ProtocolVersionError = delegate { };
        public event Action<Device> Disconnected = delegate { };
        public event Action<Device> Offlined = delegate { };
        public event Action<Device> ConnectTimeouted = delegate { };
        public event Action<Device> ConnectingIn = delegate { };
        public event Action<Device> BeingRejected = delegate { };
        public event Action<Device> ConnectFailed = delegate { };
        public event Action<Device> OnLined = delegate { };
        public event Action<Device> RemoteInputWrongPassword = delegate { };
        //对方主动发了一个刷新消息。本端收到了这个消息。
        public event Action<Device> Refreshed = delegate { };
        /// <summary>
        /// 这是异步操作。不会导致阻塞。如果Channel有效，会马上发送这个消息，如果无效，则什么也不会做。
        /// 调用这个函数要小心。因为它会检查是否已经连接，状态是Connected才会实际发送，否则会发起一个连接动作。
        /// </summary>
        /// <param name="message"></param>
        public bool Post(Message message)
        {
            return Post( _ => channelWrapper.Post(message));
        }
        public bool Post(ISendable sendable)
        {
            return Post( _ => channelWrapper.Post(sendable));
        }

        /// <summary>
        /// 尝试执行一段代码，如果设备已经连接，直接执行，如果没有，启动连接过程。
        /// 调用这个方法要慎重！不要通过这个函数发送可以导致Device状态变化的消息。因为这个函数会导致连接状态变化！！！！！！！！！
        /// </summary>
        /// <param name="action"></param>
        /// <returns></returns>
        internal bool Post(Action<Device> action)
        {
            if (State == DeviceState.Connected)
            {
                connectedTimer?.ReStart();
                action?.Invoke(this);
                return true;
            }
            else
            {
                Connect();
                return false;
            }
        }

        public DeviceState State
        {
            get
            {
                lock (deviceLocker)
                {
                    return _state;
                }
            }
            set
            {
                //确实可能多个线程进来更改这个状态。
                lock (deviceLocker)
                {
                    if (_state != value)
                    {
                        //
                        ConnectingOutTimer?.Stop();
                        ConnectingOutTimer = null;
                        if (value == DeviceState.Error)
                        {
                            if (_state != DeviceState.Connected)
                            {
                                //只有在Connected状态下才需要报错。其他的都不需要。ConnectingOut会在超时的时候报其他错误。所以也不需要。
                                return;
                            }
                        }

                        var prevState = _state;
                        _state = value;
                        switch (_state)
                        {
                            case DeviceState.Connected:
                                //一旦连接成功，启动connectedTimer。发送或者收到消息，都重置这个timer。
                                ErrorCount = 0;
                                connectedTimer = new System.Timers.Timer();
                                Connected.Invoke(this);
                                //channel.StartHeartbeat();
                                wasConnectedAtLeastOnce = true;
                                break;
                            case DeviceState.Error:
                                //TODO 这里有点不好。连接出错后，会把状态改成Error,然后自动尝试Connect，会把状态改成ConnectingOut,超时之后，又会回到
                                //这里。也就是说Device.Errored事件会被反复调用，这究竟是不是想要的结果呢？我怎么能区分这次出错，是在连接超时出错，还是已经过了很久，发生的错误？
                                ++ErrorCount;
                                connectedTimer?.Stop();
                                Errored?.Invoke(this);
                                break;
                            case DeviceState.Disconnected:
                                //TODO 有没有这种状况，切换到这个状态的时候，刚好reconnect的timer被调用了？改如何锁定？
                                OnDisconnected();
                                //TODO bug 下个版本。还少了一个Unpair消息... UnPair仅从这一端的信任设备列表中移除，并没有从另一端移除。另一端还记着这个设备。但这个事情好像确实不关对方什么事情。
                                Disconnected.Invoke(this);
                                break;
                            case DeviceState.ConnectingIn:
                                ConnectingIn.Invoke(this);
                                break;
                            case DeviceState.BeingRejected:
                                BeingRejected.Invoke(this);
                                break;
                            case DeviceState.Rejected:
                                //我拒绝了这个设备。
                                break;
                            case DeviceState.OffLine:
                                Offlined?.Invoke(this);
                                break;
                            default:
                                break;
                        }
                    }
                }//end lock
            }
        }

        internal void Refresh()
        {
            LastUpdateTime = Environment.TickCount;
            Refreshed.Invoke(this);
        }

        internal void Online()
        {
            IsOnline = true;
            State = DeviceState.OnLine;
            OnLined.Invoke(this);
        }

        private void OnDisconnected()
        {
            SessionCode = null;
            //TODO 新特性。断开连接的时候，应该保存这些状态？清理掉所有与这个设备相关的会话。
            var convs = AppModel.Instance.Conversations.Where(kv => kv.Value.Peer.ID == this.ID).Select(kv => kv.Value).ToList();
            convs.ForEach(c => c.Terminate());
        }

        /// <summary>
        /// 这个设备如果断开，是否需要自动重连？有两个条件才能自动重连，1，曾经成功连接过；2，有Requester要求自动重连。例如SendFile.
        /// 如果没有Requester要求重连，那么当创建新的Requester并发送任何消息的时候，会触发连接的动作。
        /// </summary>
        public bool ShouldAutoReconnect
        {
            get
            {
                return autoConnectRequesters.Count > 0 && wasConnectedAtLeastOnce;
            }
        }

        public int LastUpdateTime { get; set; }
        public System.Timers.Timer ConnectingOutSocketTimer { get; private set; }
        public object ExtraData { get; set; }

        //是单纯增加一个引用计数就行了，还是说要保存会话？保存会话可能会有其他作用。现在还没想到。
        //private int autoConnectRequesterCount = 0;
        List<Requester> autoConnectRequesters = new List<Requester>();
        internal void AddAutoConnectRequester(Requester autoConnectRequester)
        {
            autoConnectRequesters.Add(autoConnectRequester);
        }
        internal void RemoveAutoConnectRequester(Requester autoConnectRequester)
        {
            autoConnectRequesters.Remove(autoConnectRequester);
        }
        //device本身不会直接收到connect消息，一个socket都是处理完connect消息，才和Device建立关联。
        internal void OnConnectMessageReceived(IChannel channel, ConnectMessage cm)
        {
            
            var sessionCode = cm.SessionCode;

            //对方发了一个SessionCode,表示以前可能连接过？
            if (!string.IsNullOrEmpty(sessionCode))
            {
                if (Crypto != null)
                {
                    sessionCode = Crypto.Decrypt(sessionCode);
                }

                //确实连接过。而且还没过期。自动同意这个设备。
                if (sessionCode == SessionCode)
                {
                    ConnectingOutTimer?.Stop();
                    //原来的channel应该是坏掉了，不然对方为什么要发Connect消息呢？所以换成对方创建的channel.
                    IsRequester = false;
                    SwitchChannel(channel);
                    //先换channel才能成功发送Accept消息。
                    Accept();
                    return;
                }
            }

            //如果没有SessionCode或者SessionCode不等于当前SessionCode.          
            //启动全新连接流程。
            if (!string.IsNullOrEmpty(cm.ChallengeString)) 
            {
                //对方要求加密
                cm.ExtractToDevice(this);
                //其实验证还是可以在B这一端完成。因为A可以用它自己的密码加密它的密码。如果B输入的密码和解出来的密码一致，就认为认证成功，
                //并发送Accept消息给A, 最终还是要由A确认是否进入可通讯状态。所以改成那个方案意义不太大。
                //还是要有这么个字段，因为ChallengeRequest这个字段，在ConnectingIn的事件处理中，在Accept的时候要用。
                ChallengeRequest = cm.ChallengeString;
                IsRequester = false;
                SwitchChannel(channel);
                bool shouldAskUserInput = true;

                //对方是否知道我的ConnectCode？
                if (!string.IsNullOrEmpty(cm.ConnectCode))
                {
                    //对方发来的ConnectCode是加密的
                    //我看一下是不是用我的密码加密的？我能判断是因为EncedConnectCode的原值就是ConnectCode（原始内容其实并不一定是ConnectCode,现在用了，只是为了方便）。
                    var ld = AppModel.Instance.LocalDevice;

                    string connectCode = null;
                    using (var sp = new SecurePassword(ld.ConnectCode))
                    using (var crypto = Env.Instance.SecurityManager.CreateCrypto(sp))
                    {
                        connectCode = crypto.Decrypt(cm.ConnectCode);
                    }

                    if (connectCode != null
                        && connectCode == ld.ConnectCode)
                    {
                        if (Env.Instance.Config.PairedDevice != null
                            && Env.Instance.Config.PairedDevice.ID != ID)
                        {
                            Reject(RejectMessage.ALLOW_ONLY_ONE_PAIR_DEVICE);
                        }
                        //对方知道ChannelCode,并正确的用这个ChannelCode给它自己的ID加了密。
                        ChannelCode = new SecurePassword(connectCode);
                        Accept();
                        Env.Instance.Config.PairedDevice = this;
                        Env.Instance.Config.Save();
                        shouldAskUserInput = false;
                    }
                    else
                    {
                        Reject(RejectMessage.CONNECT_CODE_INCORRECT);
                        return;
                    }
                }

                if (shouldAskUserInput)
                {
                    State = DeviceState.ConnectingIn;
                }
                return;
            }
            else
            {
                //对方不要求加密。
                this.ChallengeRequest = null;
                if (State == DeviceState.ConnectingOut)
                {
                    //如果是我去连接别人了，别人同时，或者稍后，给我发了ConnectMessage。
                    cm.ExtractToDevice(this);
                    TryAutoAccept(channel);
                }
                else if (State == DeviceState.ConnectingIn)
                {
                    //如果刚刚已经连接进来，并等待批准，为什么还要再连接？
                }
                else
                {
                    //可能是通过广播搜到的，也可能是别人拍了我的二维码主动连过来的,现在开始发起实际连接了,从前没有成功连接过。
                    cm.ExtractToDevice(this);//如果没有发现别人，别人直接连过来了，这个动作做了两次。没办法。本来在这里做最合适。但那里要汇报deviceDiscovered。
                    IsRequester = false;
                    SwitchChannel(channel);

                    var app = AppModel.Instance;
                    var config = Env.Instance.Config;
                    //如果这个设备在信任设备列表中。
                    if (!string.IsNullOrEmpty(cm.ConnectCode) && cm.ConnectCode == app.LocalDevice.ConnectCode)
                    {
                        //如果这个设备所代表的对方发送了一个ConnectCode,而且这个ConnectCode与本机的ConnectCode一致，说明对方知道我的口令，自动同意连接。而且添加为信任设备
                        //TODO bug 这样还不够，应该从AppModel里面去取Device,然后再add或者replace，让所有的地方都只保留一个Device的引用。

                        //不过Device这个类应该是保证了它一定是AppModel里面的。因为在Connect之前已经检查了。
                        config.TrustDevices.AddOrReplace(this);
                        config.Save();
                    }

                    //如果上面成功添加了可信设备，这里就会自动接受了。
                    if (config.TrustDevices.FirstOrDefault(d => d.ID == this.ID) != null)
                    {
                        Accept();
                    }
                    else
                    {
                        //这会触发ConnectingIn事件，用户可以决定是否同意这个连接。
                        State = DeviceState.ConnectingIn;
                    }
                }
            }
        }


        System.Timers.Timer ensureExistTimer = null;
        const double MAX_ALLOW_NOTSEEN_TIME = 30000d;
        /// <summary>
        /// 确保这个设备在线。一旦启动了这个检查，只有当设备下线，或者变成Disconnected状态的时候，才会停止检查。
        /// 一对一，10秒一次
        /// 
        /// 虽然在线，但并不一定保证对方与自己连接。所以有的时候可能会出现，断开连接，但这里显示还是连接的状态。
        /// 
        /// 其实真正要保证的，只是对方在线，并不需要保证对方与自己连接。
        /// 
        /// 如果在网络断开的情况下，对方断开，再马上恢复网络，另一端会一直显示连接状态，因为这个函数只检查设备在不在线，不检查对方还是不是和我连接。
        /// </summary>
        public void EnsureOnline()
        {
            //先主动发一个Ping消息。
            Ping();
            //稍后检查Ping消息对方是否应答。
            ensureExistTimer?.Stop();
            ensureExistTimer = Util.DoLater(
                ()=>
                {
                    //从ping收到应答算起，这个值一定小于MAX_ALLOW_NOTSEEN_TIME
                    var interval = Environment.TickCount - LastUpdateTime;
                    if ( interval > MAX_ALLOW_NOTSEEN_TIME)
                    {
                        Offline();
                    }else
                    {
                        if (State != DeviceState.OffLine && State != DeviceState.Disconnected)
                        {
                            EnsureOnline();
                        }
                    }
                }
                , MAX_ALLOW_NOTSEEN_TIME);
        }

        private void TryAutoAccept(IChannel channel)
        {

            if (AppModel.Instance.LocalDevice.ID.CompareTo(ID) > 0)
            {
                ConnectingOutTimer?.Stop();
                //使用对方创建的channel.我自己创建的channel不要了。
                IsRequester = false; //需要设置为false，以后如果万一连接断开，总是由对方发起请求。
                SwitchChannel(channel);
                Accept();
            }
            else
            {
                //等待对方accept即可。如果两个人同时accept，就不知道该用哪个channel了。
            }
        }

        private IChannel channel;


        //TODO future 也许可以是AddChannel，如果一个Device同时支持多个Channel的话。
        //有一个地方不太清楚，切换channel是收到这个申请就执行呢，还是要做一些判断？怎么就相信我自己的channe不能用了？
        internal void SwitchChannel(IChannel channel)
        {
            UnbindChannel();
            this.channel = channel;
            channelWrapper = new ChannelWrapper(this, channel);
            channel.ErrorHappened += OnChannelError;
            channel.PacketReceived += OnPacketReceived;
            //因为这次成功连接了对端，所以记录对端的IP.
            DefaultIP = channel.RemoteIP;
        }
        void UnbindChannel()
        {
            if (channel != null)
            {
                channel.ErrorHappened -= OnChannelError;
                channel.PacketReceived -= OnPacketReceived;
            }
        }
        void OnChannelError(ErrorType e)
        {
            lock (deviceLocker)
            {
                //一旦出错，不再监听这个Channel的事件。
                UnbindChannel();
                State = e == ErrorType.Timeout ? DeviceState.HeartbeatTimeout : DeviceState.Error;
            }
        }

        public void CallPacketProtocolVersionError(bool ishiger)
        {
            ProtocolVersionError?.Invoke(this, ishiger);
        }

        void OnPacketReceived(Packet packet)
        {
            LastUpdateTime = Environment.TickCount;
            var app = AppModel.Instance;
            Message message = Message.FromPacket(packet);

            if (message is ConversationMessage)
            {
                connectedTimer?.ReStart();
                //这部分代码适合放在AppModel里面。Device和Conversation并没有直接联系。Conversation可以不关心Device的状态。
                var cm = message as ConversationMessage;
                app.ProcessConversationMessage(this, cm);
            }
            else
            {
                if (message is DisconnectMessage)
                {
                    var dm = message as DisconnectMessage;
                    this.ExtraData = dm;
                    
                    State = DeviceState.Disconnected;
                }
                else if (message is RejectMessage)
                {
                    if (IsRequester)
                    {
                        ExtraData = message;
                        State = DeviceState.BeingRejected;
                    }
                }
                else if (message is AcceptMessage)
                {
                    if (IsRequester)
                    {
                        var am = message as AcceptMessage;
                        SessionCode = am.SessionCode;

                        if (ID == null)
                        {
                            //如果我是通过IP地址去主动连接别人的，那直到别人回复我这个消息，我没有对方的完整信息。这个分支在Release版里面应该不存在。
                            am.ExtractToDevice(this);
                            AppModel.Instance.AddOrUpdateDevice(this);
                        }
                        if (ChannelCode != null) //这一端设置了密码。
                        {
                            if (!string.IsNullOrEmpty(am.ChallengeResponse))
                            {
                                string s = Crypto.Decrypt(am.ChallengeResponse);
                                if (s == ChallengeRequest)
                                {
                                    var msg = new ChannelReadyMessage();
                                    msg.SendCompleted += (_) =>
                                    {
                                        channel.Crypto = Crypto;
                                        State = DeviceState.Connected;
                                    };
                                    channelWrapper.Post(msg);
                                }
                                else
                                {
                                    //在事件中调用SendConnectMessage()，会重新发起连接。
                                    //上层需要决定是否调用。不能无限制的重连。如果重连测试多了，需要告知用户。
                                    RemoteInputWrongPassword?.Invoke(this);   
                                }
                            }
                        }
                        else
                        {
                            State = DeviceState.Connected;
                        }

                    }
                }
                else if (message is ChannelReadyMessage)
                {
                    if (State == DeviceState.WaitSecureInitiatorConfirm)
                    {
                        State = DeviceState.Connected;
                        channel.Crypto = Crypto;
                    }
                }
            }
        }



        public void CopyFrom(Device device)
        {
            IPAddress = device.IPAddress;
            DefaultIP = device.DefaultIP;
            DeviceName = device.DeviceName;
            DeviceType = device.DeviceType;
            LastUpdateTime = device.LastUpdateTime;
            Name = device.Name;
            ID = device.ID;
            BluetoothMac = device.BluetoothMac;

            if (State == DeviceState.Disconnected || State == DeviceState.OffLine)
            {
                State = device.State;
            }
        }


        /// <summary>
        /// 像目标设备发送一个udp数据包。对方收到后，回复一个数据包，发送者收到这个数据包之后更新device的最新在线时间。
        /// </summary>
        public void Ping()
        {
            var m = new OnlineMessage(AppModel.Instance.LocalDevice);
            SendUdpMessage(m);
        }
        public void Acknowledge()
        {
            var m = new AcknowledgeMessage(AppModel.Instance.LocalDevice);
            SendUdpMessage(m);
        }
        public void NotifyImOffline()
        {
            var m = new OfflineMessage(AppModel.Instance.LocalDevice);
            SendUdpMessage(m);
        }
        private void SendUdpMessage(Message message)
        {
            try
            {
                if (string.IsNullOrEmpty(DefaultIP)) return;

                byte[] data = message.ToPacket().ToBytes();
                //如何确定udpclient是否可用？是不是总能够保证正确的初始化？
                if (data != null)
                {
                    udpclient.Send(data, data.Length, _defaultIP, LANDiscoverer.DefaultBroadCastPort);
                    //单点发送不出去是防火墙的问题，并不需要这样发多次。没什么意义。
                    //Thread.Sleep(10);
                    //udpclient.Send(data, data.Length);
                }
            }
            catch (Exception ex)
            {
                Env.Instance.Logger.Error(ex, "Send Udp message exception" + message.ToString());
            }

        }
        //当设备应该下线时（对方通知，它自己要下线了），完成这个代理本身的清理工作。
        internal void Offline()
        {
            AppModel.Instance.RemoveDevice(this);
            State = DeviceState.OffLine;
            try
            {
                udpclient?.Close();
            }
            catch (Exception ex) { }
        }

        private bool socketCreated = false;
        private void OnConnectintOutTimeout(Action timeoutAction)
        {
            lock(deviceLocker)
            {
                //如果中间已经连接上了，又到了这里，就不要执行了。
                if (State == DeviceState.Connected || State == DeviceState.Error) return;
                State = DeviceState.Error;
                ConnectingOutTimer?.Stop();
                ConnectingOutSocketTimer?.Stop();

                if (timeoutAction == null)
                {
                    //如果用户没有指定超时的行为，调用Device的默认的超时事件。
                    ConnectTimeouted?.Invoke(this);
                }
                else
                {
                    //如果有自定义超时，调用自定义超时。
                    timeoutAction();
                }
            }
        }
        /// <summary>
        /// 在限定的时间内，每隔两秒钟，不停的尝试链接，直到连接成功。如果超时，调用timeoutAction. 在给定时间内，不限次数的连接。
        /// 
        /// 注意！！ 调用Connect之前，如果Device的状态已经是ConnectingOut，这个函数会直接返回。如果想要强制重连，需要先将Device状态设置成Idle.
        /// </summary>
        /// <param name="timeoutMilliSeconds">允许尝试连接的时间（包括等待对方应答的时间）。</param>
        /// <param name="socketTimeoutMilliSeconds">建立socket最大等待时间。默认值=timeoutMilliSeconds。如果在这个事件范围内未成功建立连接，则报错</param>
        public void Connect(double timeoutMilliSeconds = 10000d, Action timeoutAction = null, double? socketTimeoutMilliSeconds = null)
        {
            if (State == DeviceState.ConnectingOut) return;  //不要重复的发连接消息。一个连接失败后，状态会改变。但在尝试期间，不得重复。
#if DEBUG
            //在测试情况下输入IP地址，或者通过扫描二维码，得到了这个设备，去主动连接这个设备时，底层的设备列表中并没有发现这个设备。
            if (ID == null)
            {
                ID = IPAddress;
            }
#endif
            Preconditions.Check(timeoutMilliSeconds > 0, "允许连接尝试时间必须大于0");
            Preconditions.Check(ID != null);

            //如果对方也在尝试连接我。
            //TODO BUG？ 这个地方和安全冲突。如果对方要求安全密码在连接我，我同时点击它，就不会要求输入密码了，再另一方会认证失败。这样倒也能接受。
            if (State == DeviceState.ConnectingIn)
            {
                //TODO 这样做是否合适？
                Accept();
                return;
            }

            State = DeviceState.ConnectingOut;
            socketCreated = false;
            double _socketTimeoutMilliSeconds = socketTimeoutMilliSeconds ?? timeoutMilliSeconds;
            ConnectingOutSocketTimer?.Stop();
            if (_socketTimeoutMilliSeconds != timeoutMilliSeconds)
            {
                ConnectingOutSocketTimer = Util.DoLater(() => 
                {
                    OnConnectintOutTimeout(timeoutAction);
                }, _socketTimeoutMilliSeconds);
            }

            //一旦启动连接后，启动一个计时器。如果中间连接成功，则清除这个计时器。
            ConnectingOutTimer?.Stop(); //如果以前曾经尝试连接，忽略以前的结果
            ConnectingOutTimer = Util.DoLater(() =>
            {
                OnConnectintOutTimeout(timeoutAction);
            }, timeoutMilliSeconds);

            IsRequester = true;
            //超时时间与上面的计时器时间相同，所以不在设置超时行为。
            AppModel.Instance.ChannelManager.CreateChannel(
                this
                , (channel) =>
                {
                    SwitchChannel(channel);
                    //TODO 为什么要延时发送？对方执行ChannelCreated, 并为Channel设置PacketReceivedEvent好像需要执行时间
                    //如果在这期间发送了Connect消息，会被对方忽略。
                    //TODO 重构 如果这个问题真的存在，可以让对方准备好之后回一个消息，这边收到消息之后再发送connect消息
                    Util.RunLater(
                         () =>
                         {
                             //如果是主动连接别人，发送一个连接消息。不要调用Device的Post函数。
                             //如果这里给别人发了链接消息，但是对方一直没理你，还是会导致超时。
                             SendConnectMessage();
                         }
                     , 100d);
                }
                , null
                , timeoutMilliSeconds
                , (int)RETRY_INTERVAL
            );
        }
        public void SendConnectMessage()
        {
            var cm = new ConnectMessage(AppModel.Instance.LocalDevice);
            cm.SessionCode = SessionCode;
            if (ChannelCode != null)
            {
#if DEBUG
                if (ChallengeRequest == null)
                {
                    try
                    {
                        throw new Exception("Should set challenge request before send connect request if you set Channel code");
                    }
                    catch(Exception ex)
                    {
                        Env.Instance.ShowMessage(ex.Message+".\n\n"+ex.StackTrace);
                    }
                }
#endif
                cm.ChallengeString = ChallengeRequest ;
                if (!string.IsNullOrEmpty(ConnectCode))
                    cm.ConnectCode = Crypto.Encrypt(ConnectCode);
                if(!string.IsNullOrEmpty(SessionCode))
                    cm.SessionCode = Crypto.Encrypt(SessionCode);
            }
            channelWrapper.Post(cm);
        }
        /// <summary>
        /// 
        /// 
        /// </summary>
        /// <param name="unpair">是否在Disconnect消息中增加unpair标记，如果unpair，表示对方不再记录这个设备</param>
        public void Disconnect(bool unpair = false)
        {
            var msg = new DisconnectMessage();
            msg.UnpairMark = unpair;
            State = DeviceState.Disconnected;
            if(channelWrapper != null)  channelWrapper.Post(msg);
        }

        public void Reject(int? rejectCode = null)
        {
            if (IsRequester)
            {
                throw new Exception("Reject is only available when the others connect to you.");
            }
            else
            {
                //设备列表中显示的设备，处于被拒绝的状态，如果我不解除这个状态，那么对方没机会在发请求过来。
                State = DeviceState.Rejected;
                var msg = new RejectMessage();
                msg.RejectCode = rejectCode;
                //不要直接调用Post,因为那个会检查是否已经连接，没连接，会调用Connect.
                channelWrapper.Post(msg);
            }
        }
        public void Accept()
        {
            var am = new AcceptMessage(AppModel.Instance.LocalDevice);
            if(string.IsNullOrEmpty( SessionCode))
            {
                SessionCode = StringHelper.NewRandomGUID();
                
            }
            am.SessionCode = SessionCode;

            if (ChannelCode != null)
            {
                Crypto = Env.Instance.SecurityManager.CreateCrypto(ChannelCode);
                am.ChallengeResponse = Crypto.Encrypt(ChallengeRequest);
                am.SendCompleted += _ =>
                {
                    State = DeviceState.WaitSecureInitiatorConfirm;
                };
            }
            else
            {
                am.SendCompleted += sendable => State = DeviceState.Connected;
                //不要直接调用Post,因为那个会 检查是否已经连接，没连接，会调用Connect.
                State = DeviceState.Connected;
            }
            channelWrapper.Post(am);
        }
        //ServerSocket监听到一个Socket之后，就会调用这个构造函数。
        public Device(IChannel channel)
        {
            //TODO 应该启动一个超时，对方连接我，但很久不发Connect消息，应该关掉它。
            //直到收到Connect消息，其实没人引用这个Device。GC会怎么处理它？因为Channel中的线程，会保证这两个东西存在？
            IsRequester = false;
            SwitchChannel(channel);
        }
        //是我发现的设备，会在设备发现过程中（或者通过扫描二维码得到），初始化必要的各种信息
        //用户想连接到那个Device的时候，会调用Connect函数,创建Channel,并在创建成功后，调用OnChannelCreated,并发送Connect消息。
        public Device()
        {
        }
        public override bool Equals(object obj)
        {
            if (obj is Device)
            {
                return (((Device)obj).ID == this.ID);
            }
            else
            {
                return false;
            }

        }
        public override int GetHashCode()
        {
            return ID.GetHashCode();
        }

        public object Clone()
        {
            throw new NotImplementedException();
        }
    }



    /// <summary>
    /// 负责Post消息，并与Channel的生命周期保持一致。Channel在，他就工作，Channel失效，他就停止
    /// </summary>
    internal class ChannelWrapper
    {
        private IChannel channel;
        private readonly BackgroundWorker sendWorker;
        private List<ISendable> sendableList = new List<ISendable>();
        private Queue<Message> messageList = new Queue<Message>();
        private object locker = new object();
        private AutoResetEvent notEmptyWaiter = new AutoResetEvent(false); //如果设置为true,第一次队列空的时候会执行。
        public bool IsWorking = false;
        private Device device = null;
        //private Device device;
        internal ChannelWrapper(Device device, IChannel channel)
        {
            this.channel = channel;
            this.device = device;

            //监控channel的状态，channel失效后，将自己设置为无效。取消背景线程。
            sendWorker = new BackgroundWorker { WorkerSupportsCancellation = true };
            sendWorker.DoWork += SendWorker_DoWork;
            sendWorker.RunWorkerAsync();
            IsWorking = true;
            channel.ErrorHappened += Channel_ErrorHappened;
        }

        private void Channel_ErrorHappened(ErrorType obj)
        {
            lock (locker)
            {
                channel.ErrorHappened -= Channel_ErrorHappened;
                sendWorker.CancelAsync();
            }
        }

        private void SendWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            var work = sender as BackgroundWorker;
            if (work == null) return;
            while (!work.CancellationPending)
            {
                if (messageList.Count == 0 && sendableList.Count == 0)
                {
                    notEmptyWaiter.WaitOne();
                }

                Message message = null;
                if (messageList.Count > 0)
                {
                    lock (locker) message = messageList.Dequeue();
                    SendImpl(message);
                }
                else
                {
                    //这个要在count判断之前做，因为remove之后count可能=0了
                    sendableList.RemoveAll(sendable => sendable == null || sendable.IsPostCompleted || sendable.TransferState == TransferState.Canceled || sendable.TransferState == TransferState.Error);
                    if (sendableList.Count > 0)
                    {
                        //如果在两次循环之间，一个sendable的状态被改变了，不需要再发送（例如取消，或者对方发现这个文件已经存在)
                        ISendable selectedItem = null;
                        lock (locker)
                        {
                            //计算优先级，除了根据splittable的Priority之外，还要根据上次的处理时间，
                            //这样的话，才能做到如果有几个任务同时进行（发文件，收文件，浏览，同步)的时候，每个任务都能得到响应。
                            //但一个splittable内部不考虑上次处理时间。因为同一批次的文件，并行发送没有意义
                            sendableList.Sort(SendableComparer<ISendable>.Instance);
                            selectedItem = sendableList[0];
                        }
                        try
                        {
                            message = selectedItem.GetNextMessage();
                        }
                        catch (Exception ex)
                        {
                            //产生了一个错误消息。稍后会处理。
                        }
                        //ToMessage或者GetNextMessage可能发生异常。例如文件读取错误
                        if (message != null)
                        {
#if DEBUG
                            if (message is ThumbnailResponseMessage)
                            {
                                var trm = selectedItem as ThumbnailResponseMessage;
                                if (trm != null)
                                {
                                    Trace.WriteLine("name=" + trm.Name + " id=" + trm.ID + " len=" + trm.Length);
                                }
                            }
                            if (message is ConversationMessage)
                            {
                                var cm = message as ConversationMessage;
                                if (cm == null || cm.ConversationID == null)
                                {
                                    throw new Exception("Conversation Message的会话ID没有设置");
                                }
                            }
#endif
                            SendImpl(message);
                        }
                        else
                        {
#if DEBUG
                            var itemState = selectedItem.TransferState;
                            if (itemState != TransferState.Error && !selectedItem.IsPostCompleted)
                            {
                                var msg = "只有在Item状态为Error或IsPostCompleted=true时GetNextMessage才允许返回null,否则请在GetNextMessage中递归调用";
                                try
                                {
                                    throw new Exception(msg);
                                }
                                catch (Exception ex)
                                {
                                    //这个奇怪的自己throw 自己catch，是为了在调试模式下，停在上面位置，点击继续后又不会导致程序停止工作。
                                }

                            }
#endif
                            Env.Instance.Logger.Log(LogLevel.Warn, "A null message was created by" + selectedItem);
                            lock (locker) sendableList.Remove(selectedItem);
                        }
                    }
                }
                //传送文件的过程中，如果点击Browse响应会很慢，这样能解决吗？
                //Thread.Sleep(2);
            }//end while
            IsWorking = false;
        }
        private void SendImpl(Message message)
        {
            //TODO 这样修改仍然不够好，因为这与IsPostCompleted的初衷不一致。PostComplete，是指把一个对象丢到Post队列就算完毕，现在是发送之前才算Post完毕。
            channel.Send(message.ToPacket());
            message.TransferState = TransferState.SentCompleted;
        }
        internal void Post(Message message)
        {
#if DEBUG
            if (message is ConversationMessage)
            {
                var cm = message as ConversationMessage;
                if (cm.ConversationID == null)
                {
                    throw new Exception("Conversation Message的会话ID没有设置");
                }
            }
#endif
            if (IsWorking)
            {
                lock (locker) messageList.Enqueue(message);
                notEmptyWaiter.Set();
            }
            else
            {
                PostWhenNotWorking();
            }
        }
        void PostWhenNotWorking()
        {
            device.State = DeviceState.Error;
#if DEBUG
            throw new Exception("Device状态错误，channel已经停止工作，以前有错误，但没有更新device状态，请检查");
#endif
        }
        internal void Post(ISendable sendable)
        {
            if (IsWorking)
            {
                lock (locker) sendableList.Add(sendable);
                notEmptyWaiter.Set();
            }
            else
            {
                PostWhenNotWorking();
            }

        }
    }
    public enum DeviceState
    {
        OnLine,

        ConnectingOut,
        ConnectingIn,
        Connected,
        WaitSecureInitiatorConfirm,

        Rejected,
        BeingRejected,
        HeartbeatTimeout,
        //Timeout,注意，不需要这个状态，如果首次连接，传入一个timeout回调，如果是在传输过程中的连接超时，则直接进入错误状态，程序可以决定下一步的行为是自动重连路程什么。
        Error,
        Disconnected,
        OffLine,
        Idle,
    }
}
