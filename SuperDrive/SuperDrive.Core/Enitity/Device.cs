using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SuperDrive.Core.Business;
using SuperDrive.Core.Channel;
using SuperDrive.Core.Channel.Protocol;
using SuperDrive.Core.Discovery;
using SuperDrive.Core.Messages;
using SuperDrive.Core.Support;

namespace SuperDrive.Core.Enitity
{
        [JsonObject(MemberSerialization.OptIn)]
        public class Device : ICloneable
        {
                public int ErrorCount { get; private set; }
                [JsonProperty(PropertyName = "Id")]
                public string Id { get; set; }
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
                public string SessionCode { get; set; }
                //除非在二维码，这个值不可暴露。


                [JsonProperty(PropertyName = "ip_address")]
                public string IpAddress { get; set; }
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
                internal string Os { get; set; }
                [JsonProperty(PropertyName = "os_version")]
                internal Version OsVersion { get; set; }
                //设备名称，如A530, iphone 6s
                [JsonProperty(PropertyName = "device_name")]
                public string DeviceName { get; set; }
                /// <summary>
                /// 设备是PC还是手机？
                /// </summary>
                [JsonProperty(PropertyName = "device_type")]
                public DeviceType DeviceType { get; set; }
                //这个不需要序列化，是动态变化的，根据收到的广播信息，或者当前连接进来的信息。
                public string DefaultIp { get; set; }
                public bool IsRequester { get; private set; }
                internal DiscoveryType DiscoveryType { get; set; }
                Timer ConnectingOutTimer { get; set; }
                internal bool IsOnline { get; set; }

                internal bool IsValid
                {
                        get
                        {
                                return !string.IsNullOrEmpty(Id) && !string.IsNullOrEmpty(Name);
                        }
                }

                private DeviceState _state;
                //private bool wasAcceptted = false;
                //同一个Device，可能会有多个Channel，例如同时存在的wifi,蓝牙,LAN,Internet通道。不仅在发现的时候要考虑，通讯的时候也要考虑。
                private bool _wasConnectedAtLeastOnce;
                private readonly object _deviceLocker = new object();

                public event Action<Device> Connected = delegate { };
                public event Action<Device> Errored = delegate { };
                public event Action<Device, bool> ProtocolVersionError = delegate { };
                public event Action<Device> Disconnected = delegate { };
                public event Action<Device> Offlined = delegate { };
                public event Action<Device> ConnectingIn = delegate { };
                public event Action<Device> BeingRejected = delegate { };
                public event Action<Device> OnLined = delegate { };

                //对方主动发了一个刷新消息。本端收到了这个消息。
                public event Action<Device> Refreshed = delegate { };
                /// <summary>
                /// 这是异步操作。不会导致阻塞。如果Channel有效，会马上发送这个消息，如果无效，则什么也不会做。
                /// 调用这个函数要小心。因为它会检查是否已经连接，状态是Connected才会实际发送，否则会发起一个连接动作。
                /// </summary>
                /// <param name="message"></param>
                public async Task<bool> Post(Message message)
                {
                        //Env.Logger.Log($"Post Message {message}");
                        return await Post(_ => SendMessage(message)).ConfigureAwait(false);
                }

                public static bool operator ==(Device rec1, Device rec2)
                {
                        return Equals(rec1, rec2);
                }

                public static bool operator !=(Device rec1, Device rec2)
                {
                        return !(rec1 == rec2);
                }

                /// <summary>
                /// 尝试执行一段代码，如果设备已经连接，直接执行，如果没有，启动连接过程。
                /// 调用这个方法要慎重！不要通过这个函数发送可以导致Device状态变化的消息。因为这个函数会导致连接状态变化！！！！！！！！！
                /// </summary>
                /// <param name="action"></param>
                /// <returns></returns>
                internal async Task<bool> Post(Action<Device> action)
                {
                        try
                        {
                                if (State != DeviceState.Connected)
                                {
                                        Env.Logger.Log("Need a connect");
                                        var result = await Connect().ConfigureAwait(false);
                                        Env.Logger.Log($"Connect result {result}");
                                        if (!result) return false;

                                        //如果出现了异常，就不要走到下面了。
                                        //connectedTimer?.ReStart();
                                        action?.Invoke(this);
                                        return true;
                                }

                                action?.Invoke(this);
                                return true;
                        }
                        catch (Exception e)
                        {
                                //这个应该是要通知上层。如果有任何的消息错误，不要再次发送消息。需要终止。
                                OnError(e);
                                Env.Logger.Log("Failed to post action", stackTrace: e.StackTrace, level: LogLevel.Error);
                                return false;
                        }
                }

                private void OnError(Exception e)
                {
                        Env.Logger.Log("Device error", stackTrace: e.StackTrace);
                        State = DeviceState.Error;
                }

                public override string ToString()
                {
                        return $"Device:name={Name} defaultIp {DefaultIp}";
                }

                public DeviceState State
                {
                        get
                        {
                                lock (_deviceLocker) return _state;
                        }
                        set
                        {
                                //确实可能多个线程进来更改这个状态。
                                lock (_deviceLocker)
                                {
                                        if (_state == value) return;

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

                                        _state = value;
                                        switch (_state)
                                        {
                                                case DeviceState.Connected:
                                                        //如果任何一条通道建立了，就发出这个信号，取消其它正在进行的连接，放弃以后连接成功的socket.
                                                        _connectCompletionSource?.TrySetResult(true);
                                                        _connectCompletionSource = null;
                                                        ErrorCount = 0;
                                                        Connected.Invoke(this);
                                                        _wasConnectedAtLeastOnce = true;
                                                        break;
                                                case DeviceState.Error:
                                                        //TODO 这里有点不好。连接出错后，会把状态改成Error,然后自动尝试Connect，会把状态改成ConnectingOut,超时之后，又会回到
                                                        //这里。也就是说Device.Errored事件会被反复调用，这究竟是不是想要的结果呢？我怎么能区分这次出错，是在连接超时出错，还是已经过了很久，发生的错误？
                                                        ++ErrorCount;
                                                        //connectedTimer?.Stop();
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
                                                case DeviceState.Idle:
                                                        _connectCompletionSource?.TrySetResult(false);
                                                        _connectCompletionSource = null;
                                                        break;
                                        }
                                }//end lock
                        }
                }

                public void CancelConnect()
                {
                        if (State == DeviceState.ConnectingOut) State = DeviceState.Idle;
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
                        var convs = SuperDriveCore.Conversations.Where(kv => kv.Value.Peer.Id == Id).Select(kv => kv.Value).ToList();
                        convs.ForEach(c => c.Terminate());
                }

                /// <summary>
                /// 这个设备如果断开，是否需要自动重连？有两个条件才能自动重连，1，曾经成功连接过；2，有Requester要求自动重连。例如SendFile.
                /// 如果没有Requester要求重连，那么当创建新的Requester并发送任何消息的时候，会触发连接的动作。
                /// </summary>
                public bool ShouldAutoReconnect => _autoConnectRequesters.Count > 0 && _wasConnectedAtLeastOnce;

                public int LastUpdateTime { get; set; }
                public Timer ConnectingOutSocketTimer { get; private set; }
                public object ExtraData { get; set; }

                //是单纯增加一个引用计数就行了，还是说要保存会话？保存会话可能会有其他作用。现在还没想到。
                //private int autoConnectRequesterCount = 0;
                readonly List<Requester> _autoConnectRequesters = new List<Requester>();
                internal void AddAutoConnectRequester(Requester autoConnectRequester)
                {
                        _autoConnectRequesters.Add(autoConnectRequester);
                }
                internal void RemoveAutoConnectRequester(Requester autoConnectRequester)
                {
                        _autoConnectRequesters.Remove(autoConnectRequester);
                }
                //device本身不会直接收到connect消息，一个socket都是处理完connect消息，才和Device建立关联。
                internal void OnConnectMessageReceived(IChannel channel, ConnectMessage cm)
                {

                        var sessionCode = cm.SessionCode;

                        //对方发了一个SessionCode,表示以前可能连接过？
                        if (!string.IsNullOrEmpty(sessionCode))
                        {
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
                                        var ld = SuperDriveCore.LocalDevice;

                                        string connectCode = cm.ConnectCode;


                                        if (connectCode != null
                                            && connectCode == ld.ConnectCode)
                                        {
                                                if (Env.Config.PairedDevice != null
                                                    && Env.Config.PairedDevice.Id != Id)
                                                {
                                                        Reject(RejectMessage.ALLOW_ONLY_ONE_PAIR_DEVICE);
                                                }
                                                //对方知道ChannelCode,并正确的用这个ChannelCode给它自己的Id加了密。
                                                Accept();
                                                Env.Config.PairedDevice = this;
                                                Env.Config.Save();
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

                        //对方不要求加密。
                        ChallengeRequest = null;
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


                                var config = Env.Config;
                                //如果这个设备在信任设备列表中。
                                if (!string.IsNullOrEmpty(cm.ConnectCode) && cm.ConnectCode == SuperDriveCore.LocalDevice.ConnectCode)
                                {
                                        //如果这个设备所代表的对方发送了一个ConnectCode,而且这个ConnectCode与本机的ConnectCode一致，说明对方知道我的口令，自动同意连接。而且添加为信任设备
                                        //TODO bug 这样还不够，应该从AppModel里面去取Device,然后再add或者replace，让所有的地方都只保留一个Device的引用。

                                        //不过Device这个类应该是保证了它一定是AppModel里面的。因为在Connect之前已经检查了。
                                        config.TrustDevices.AddOrReplace(this);
                                        config.Save();
                                }

                                //如果上面成功添加了可信设备，这里就会自动接受了。
                                if (config.TrustDevices.FirstOrDefault(d => d.Id == Id) != null)
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


                Timer _ensureExistTimer;
                const double MaxAllowNotseenTime = 30000d;
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
                        _ensureExistTimer?.Stop();
                        _ensureExistTimer = Util.DoLater(
                            () =>
                            {
                                    //从ping收到应答算起，这个值一定小于MAX_ALLOW_NOTSEEN_TIME
                                    var interval = Environment.TickCount - LastUpdateTime;
                                    if (interval > MaxAllowNotseenTime)
                                    {
                                            Offline();
                                    }
                                    else
                                    {
                                            if (State != DeviceState.OffLine && State != DeviceState.Disconnected)
                                            {
                                                    EnsureOnline();
                                            }
                                    }
                            }
                            , MaxAllowNotseenTime);
                }

                private void TryAutoAccept(IChannel channel)
                {

                        // ReSharper disable once StringCompareToIsCultureSpecific
                        if (SuperDriveCore.LocalDevice.Id.CompareTo(Id) > 0)
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

                private IChannel _channel;


                //TODO future 也许可以是AddChannel，如果一个Device同时支持多个Channel的话。
                //有一个地方不太清楚，切换channel是收到这个申请就执行呢，还是要做一些判断？怎么就相信我自己的channe不能用了？
                internal void SwitchChannel(IChannel channel)
                {
                        UnbindChannel();
                        _channel = channel;
                        channel.ErrorHappened += OnChannelError;
                        channel.PacketReceived += OnPacketReceived;
                        //因为这次成功连接了对端，所以记录对端的IP.
                        DefaultIp = channel.RemoteIP;
                }
                void UnbindChannel()
                {
                        if (_channel != null)
                        {
                                _channel.ErrorHappened -= OnChannelError;
                                _channel.PacketReceived -= OnPacketReceived;
                                try
                                {
                                        _channel.Dispose();
                                }
                                catch (Exception e)
                                {
                                        Env.Logger.Log("dispose channel exception", stackTrace: e.StackTrace);
                                }

                                _channel = null;
                        }
                }
                void OnChannelError(ErrorType e)
                {
                        //如何防止这个函数被多次调用？确实在不同地方会触发多次。
                        if (State == DeviceState.Error) return;

                        State = DeviceState.Error;
                        //一旦出错，不再监听这个Channel的事件。
                        UnbindChannel();

                }

                public void CallPacketProtocolVersionError(bool ishiger)
                {
                        ProtocolVersionError?.Invoke(this, ishiger);
                }

                void OnPacketReceived(Packet packet)
                {
                        LastUpdateTime = Environment.TickCount;
                        var message = Message.FromPacket(packet);
                        //Env.Logger.Log($"Message received from device{this} {message}");
                        if (message is ConversationMessage)
                        {
                                //connectedTimer?.ReStart();
                                //这部分代码适合放在AppModel里面。Device和Conversation并没有直接联系。Conversation可以不关心Device的状态。
                                var cm = message as ConversationMessage;
                                SuperDriveCore.ProcessConversationMessage(this, cm);
                        }
                        else
                        {
                                if (message is DisconnectMessage)
                                {
                                        var dm = message as DisconnectMessage;
                                        ExtraData = dm;

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

                                                if (Id == null)
                                                {
                                                        //如果我是通过IP地址去主动连接别人的，那直到别人回复我这个消息，我没有对方的完整信息。这个分支在Release版里面应该不存在。
                                                        am.ExtractToDevice(this);
                                                        SuperDriveCore.AddOrUpdateDevice(this);
                                                }
                                                State = DeviceState.Connected;
                                        }
                                }
                                else if (message is ChannelReadyMessage)
                                {
                                        if (State == DeviceState.WaitSecureInitiatorConfirm)
                                        {
                                                State = DeviceState.Connected;
                                        }
                                }
                        }
                }



                public void CopyFrom(Device device)
                {
                        IpAddress = device.IpAddress;
                        DefaultIp = device.DefaultIp;
                        DeviceName = device.DeviceName;
                        DeviceType = device.DeviceType;
                        LastUpdateTime = device.LastUpdateTime;
                        Name = device.Name;
                        Id = device.Id;
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
                        var m = new OnlineMessage(SuperDriveCore.LocalDevice);
                        SendUdpMessage(m);
                }
                public void Acknowledge()
                {
                        var m = new AcknowledgeMessage(SuperDriveCore.LocalDevice);
                        SendUdpMessage(m);
                }
                public void NotifyImOffline()
                {
                        var m = new OfflineMessage(SuperDriveCore.LocalDevice);
                        SendUdpMessage(m);
                }
                private void SendUdpMessage(Message message)
                {
                        try
                        {
                                if (string.IsNullOrEmpty(DefaultIp)) return;

                                byte[] data = message.ToPacket().ToBytes();
                                //如何确定udpclient是否可用？是不是总能够保证正确的初始化？
                                if (data != null)
                                {
                                        var discoverer = SuperDriveCore.Discoverer as LanDiscoverer;
                                        var endPoint = new IPEndPoint(IPAddress.Parse(DefaultIp), Consts.DiscoverPort);
                                        discoverer?.SendUdp(data, endPoint);
                                        //单点发送不出去是防火墙的问题，并不需要这样发多次。没什么意义。
                                        //Thread.Sleep(10);
                                        //discoverer?.ListenClient.SendAsync(data, data.Length, DefaultIP, SuperDriveCore.DiscoverPort);
                                }
                        }
                        catch (Exception ex)
                        {
                                Env.Logger.Log("Send Udp message exception" + message, stackTrace: ex.StackTrace);
                        }

                }
                //当设备应该下线时（对方通知，它自己要下线了），完成这个代理本身的清理工作。
                internal void Offline()
                {
                        SuperDriveCore.InstanceInternal.DevicesInternal.Remove(this);
                        State = DeviceState.OffLine;
                }
                //private CancellationTokenSource _connectCancelSource;
                private TaskCompletionSource<bool> _connectCompletionSource;



                //TODO  注意！！ 调用Connect之前，如果Device的状态已经是ConnectingOut，这个函数会直接返回。如果想要强制重连，需要先将Device状态设置成Idle.

                //连接过程其实是异步的，可以被await。启动连接后，尝试建立socket,并发送connect消息，
                //当收到对方的回复，会返回true.否则返回false。同时还会触发被拒绝或同意的事件。

                //这个看起来有点怪，既有事件，又有返回值。不过也可以接受吧？事件算是用来广播的了。返回值则用于这一次的处理逻辑
                public Task<bool> Connect(TimeSpan timeSpan = default(TimeSpan))
                {
                        Util.Check(Id != null);
                        //如果有正在连接的过程，就使用那个连接过程。不在重新发起连接，如果确实想要重新发起连接，需要把connectCompletionSource.SetCancelled.
                        if (_connectCompletionSource != null) return _connectCompletionSource.Task;


                        _connectCompletionSource = new TaskCompletionSource<bool>();
                        if (timeSpan == default(TimeSpan)) timeSpan = TimeSpan.FromSeconds(Consts.DefaultConnectTimeoutSeconds);
                        var localCts = _connectCompletionSource;
                        localCts.SetValueWhenTimeout(timeSpan, false);

                        //如果对方也在尝试连接我。
                        if (State == DeviceState.ConnectingIn)
                        {
                                //TODO 这个地方需要做更多检查。在特定条件下才Accept。
                                Accept();
                                //返回即可，Accept后会改变状态。
                                localCts.TrySetResult(true);
                                return localCts.Task;
                        }

                        State = DeviceState.ConnectingOut;
                        localCts.Task.ConfigureAwait(false);

                        Task.Run(async () =>
                        {
                                IChannel c = await SuperDriveCore.ChannelManager.CreateChannel(this);
                                if (c == null)
                                {
                                        //如果设置不成功，直接就返回了。
                                        localCts.TrySetResult(false);
                                }
                                else
                                {

                                        IsRequester = true;
                                        SwitchChannel(c);
                                        //当Channel创建成功后，发送一个ConnectMessage. 然后等待对方同意或者拒绝
                                        //await Task.Delay(200);
                                        SendConnectMessage();
                                        //不要做这个事情！继续等待对方发来Accept或者Reject Message，才会设置connectCompletionSource的结果。
                                        //localCts.TrySetResult(true);
                                }
                        });
                        return localCts.Task;
                }

                public void SendConnectMessage()
                {
                        var cm = new ConnectMessage(SuperDriveCore.LocalDevice) { SessionCode = SessionCode };
                        SendMessage(cm);
                }

                void SendMessage(Message msg)
                {
                        //Env.Logger.Log($"Before send message { msg }");
                        _channel.Send(msg.ToPacket());
                        msg.TransferState = TransferState.SentCompleted;
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
                        SendMessage(msg);
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
                                SendMessage(msg);
                        }
                }
                public void Accept()
                {
                        var am = new AcceptMessage(SuperDriveCore.LocalDevice);
                        if (string.IsNullOrEmpty(SessionCode))
                        {
                                SessionCode = StringHelper.NewRandomGUID();

                        }
                        am.SessionCode = SessionCode;

                        am.SendCompleted += sendable =>
                        {
                                State = DeviceState.Connected;
                        };

                        //Env.Logger.Log($"Send accept message to device{this}");
                        SendMessage(am);
                }

                public Device()
                {

                }
                //ServerSocket监听到一个Socket之后，就会调用这个构造函数。
                public Device(IChannel channel, Timer connectingOutSocketTimer)
                {
                        ConnectingOutSocketTimer = connectingOutSocketTimer;
                        //TODO 应该启动一个超时，对方连接我，但很久不发Connect消息，应该关掉它。
                        //直到收到Connect消息，其实没人引用这个Device。GC会怎么处理它？因为Channel中的线程，会保证这两个东西存在？
                        IsRequester = false;
                        SwitchChannel(channel);
                }
                //是我发现的设备，会在设备发现过程中（或者通过扫描二维码得到），初始化必要的各种信息
                //用户想连接到那个Device的时候，会调用Connect函数,创建Channel,并在创建成功后，调用OnChannelCreated,并发送Connect消息。
                public Device(Timer connectingOutSocketTimer)
                {
                        ConnectingOutSocketTimer = connectingOutSocketTimer;
                }

                public override bool Equals(object obj)
                {
                        var d1 = obj as Device;
                        return d1?.Id == Id;
                }

                public override int GetHashCode()
                {
                        // ReSharper disable once NonReadonlyMemberInGetHashCode
                        return Id.GetHashCode();
                }

                public object Clone()
                {
                        throw new NotImplementedException();
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
                //HeartbeatTimeout,
                //Timeout,注意，不需要这个状态，如果首次连接，传入一个timeout回调，如果是在传输过程中的连接超时，则直接进入错误状态，程序可以决定下一步的行为是自动重连路程什么。
                Error,
                Disconnected,
                OffLine,
                Idle,
        }
}
