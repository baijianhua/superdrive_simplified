using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Reflection;
using SuperDrive.Core.Business;
using SuperDrive.Core.Discovery;
using SuperDrive.Core.Enitity;
using SuperDrive.Core.Channel;
using SuperDrive.Core.Channel.Protocol;
using SuperDrive.Core.Messages;
using SuperDrive.Core.Support;
using Util = SuperDrive.Core.Support.Util;

namespace SuperDrive.Core
{
        public class SuperDriveCore
        {
                ///还不能删除这个定义，如果删了它，就得记录所有的watcher。
                private event Action<Device> DeviceDiscovered;
                public static event Action<Responder> ConversationReceived = delegate { };
                public static event Action<Conversation> ConversationRecovered = delegate { };
                //public static event Action<Error> ErrorHappened = delegate { };

                public static bool IsInitialized => _instance != null;
                public static Device LocalDevice => _instance._localDevice;
                public static Dictionary<string, Conversation> Conversations => _instance._conversations;
                public static ChannelManager ChannelManager => _instance._channelManager;
                public static Discoverer Discoverer => _instance._discoverer;
                internal ObservableCollection<Device> DevicesInternal = new ObservableCollection<Device>();
                private readonly ReadOnlyObservableCollection<Device> _roDevices;
                public static ReadOnlyObservableCollection<Device> Devices => _instance._roDevices;

                private readonly ChannelManager _channelManager;
                private readonly Dictionary<string, Conversation> _conversations;
                private readonly Device _localDevice;

                private static SuperDriveCore _instance;
                private readonly Discoverer _discoverer;
                internal static SuperDriveCore InstanceInternal => _instance;

                public static void Init(Env env)
                {
                        _instance = new SuperDriveCore();
                }

                public static void AddDeviceEventWatcher(IDeviceEventWatcher watcher)
                {
                        //已有设备和新设备都会关心这些事件。
                        foreach (Device d in Devices)
                        {
                                watcher.Watch(d);
                        }
                        _instance.DeviceDiscovered += watcher.Watch;
                }

                public static void RemoveDeviceEventWatcher(IDeviceEventWatcher watcher)
                {
                        foreach (Device d in Devices)
                        {
                                watcher.UnWatch(d);
                        }
                        _instance.DeviceDiscovered -= watcher.Watch;
                }




                public List<Requester> SavedRequesters => new List<Requester>();

                internal static void AttachConversation(Conversation conversation)
                {
                        if (conversation == null) return;
                        //TODO bug 怎么会出现重复的Conversation?

                        Conversations.Add(conversation.Id, conversation);

                }

                internal static void RemoveConversation(Conversation conversation)
                {
                        if (conversation == null) return;

                        Conversations.Remove(conversation.Id);
                }

                internal static void ProcessConversationMessage(Device device, ConversationMessage cm)
                {
                        var conversation = Conversations.GetByKey(cm.ConversationID);
                        if (conversation == null)
                        {
                                if (cm is ConversationRequestMessage)
                                {
                                        var crm = cm as ConversationRequestMessage;
                                        Responder responder = crm.CreateResponder();
                                        responder.Id = crm.ConversationID;
                                        responder.Peer = device;
                                        responder.RequestMessage = crm;
                                        AttachConversation(responder);
                                        ConversationReceived?.Invoke(responder);
                                }
                                else if (cm is ConversationRecoverRequestMessage)
                                {
                                        //TODO 反序列化并且提示用户是否同意恢复会话。
                                        //如果程序异常退出，对方发来的recoverrequest，这一端并不知情。不会有任何响应。
                                        conversation = DeserializeConversation(cm as ConversationRecoverRequestMessage);
                                        if (conversation != null)
                                        {
                                                ConversationRecovered?.Invoke(conversation);
                                        }
                                        else
                                        {
                                                var msg = new ConversationRejectMessage(RejectCode.DeSerializeFailed)
                                                {
                                                        ConversationID = cm.ConversationID
                                                };
#pragma warning disable CS4014 // 由于此调用不会等待，因此在调用完成前将继续执行当前方法
                                                device.Post(msg);
#pragma warning restore CS4014 // 由于此调用不会等待，因此在调用完成前将继续执行当前方法
                                        }
                                }
                        }
                        else
                        {
                                conversation.Process(cm);
                        }
                }
                private static Conversation DeserializeConversation(ConversationRecoverRequestMessage conversationRecoverMessage)
                {
                        //TODO 新特性， 反序列化会话。
                        return null;
                }

                string GetIpAddressListString()
                {
                        return string.Join("#", Env.Network.GetIpAddresses().Select(i => i.ToString()).ToArray());
                }
                //Mutex appModelMutex;

                private SuperDriveCore()
                {
                        //container.Logger.Error("Test Log");
                        var cfg = Env.Config;
                        _roDevices = new ReadOnlyObservableCollection<Device>(DevicesInternal);
                        _conversations = new Dictionary<string, Conversation>();
                        _localDevice = cfg.LocalDevice;
                        _localDevice.ConnectCode = null;
                        _localDevice.DeviceType = Env.DeviceType;
                        _localDevice.IpAddress = GetIpAddressListString();
                        _channelManager = new ChannelManager(Consts.ChannelPort);
                        _instance = this;

                        _channelManager.ChannelCreated += channel =>
                        {
                                Action<Packet> packetReceiver = null;
                                packetReceiver = packet =>
                    {
                            Message message = Message.FromPacket(packet);
                            ConnectMessage cm = message as ConnectMessage;
                            //一个新连接的socket，在发送ConnectMessage之前发送其它Message，是不会理会的。
                            if (cm != null)
                            {
                                    channel.PacketReceived -= packetReceiver;
                                    var device = AddOrUpdateDevice(cm.Device);
                                    if (device != null)
                                    {
                                            if (PacketHeader.DefaultVersion == packet.Header.version)
                                            {
                                                    device.OnConnectMessageReceived(channel, cm);
                                            }
                                            else
                                            {
                                                    device.CallPacketProtocolVersionError(PacketHeader.DefaultVersion > packet.Header.version);
                                            }
                                    }
                            }
                    };
                                channel.PacketReceived += packetReceiver;
                        };

                        _discoverer = new LanDiscoverer(LocalDevice, Consts.DiscoverPort);
                        _httpServer = new HttpListener(IPAddress.Any, Consts.HttpPort);
                        _httpServer.Start();
                        //SuperDriveCore是集大成者，作用就是承上启下。它无所不知。所以，既能访问上层，又能访问下层。
                        _httpServer.Request += HttpRequestDispatcher.Dispatch;

                        Env.Network.NetworkChanged += ips =>
                        {
                                if (!string.IsNullOrEmpty(ips))
                                {
                                        LocalDevice.IpAddress = GetIpAddressListString();
                                        _discoverer.StartBroadcast();
                                }
                        };
                }



                /// <summary>
                /// 如果已经知道一个设备，则更新已有设备的信息。如果是一个新设备，把它添加到列表中。
                /// 
                /// 改这个要慎重，它被LANDiscoverer调用，还被Device的Connect调用。
                /// </summary>
                /// <param name="device"></param>
                /// <param name="needMerge"></param>
                /// <returns></returns>

                public static Device AddOrUpdateDevice(Device device, bool needMerge = true)
                {
                        //检查这个Device是否已知。
                        if (device == null) return null;

                        Util.Check(!string.IsNullOrEmpty(device.Id));
                        if (device.Id == LocalDevice.Id) return null;

                        var existingDevice = Devices.FirstOrDefault(o => o.Id == device.Id);
                        if (existingDevice == null)
                        {
                                _instance.DevicesInternal.Add(device);
                                device.IsFirstSeen = true;
                                _instance.DeviceDiscovered?.Invoke(device);
                        }
                        else
                        {
                                //如果这个设备原来已经存在了，那么其实应该返回原来的设备，只是需要将新发现的不同的地方复制到原来的设备上
                                //各事件都是绑定到原来的设备的。
                                if (needMerge)
                                        existingDevice.CopyFrom(device);
                                //不管是否Merge,都要修改IP。
                                existingDevice.DefaultIp = device.DefaultIp;
                                existingDevice.DiscoveryType = device.DiscoveryType; //是只记录一种发现方式，还是可以记录多种？
                                device = existingDevice;
                                device.IsFirstSeen = false;
                        }
                        device.LastUpdateTime = Environment.TickCount;
                        return device;
                }
                //可以被启动停止多次，但只能release一次。
                private bool _isRunning;
                private HttpListener _httpServer;
                //必须设置LocalDevice之后才能运行
                public static void Start() => _instance.StartImpl();
                public void StartImpl()
                {
                        Util.Check(LocalDevice != null, "Local Device Must be set before start");
                        if (_isRunning) return;

                        _isRunning = true;
                        _discoverer.StartListen();
                        _discoverer.StartBroadcast();
                        _channelManager.Start();


                        Env.Logger.Log("SuperDriveCore Started", nameof(SuperDriveCore));
                }

                public static void Stop() => _instance.StopImpl();
                public void StopImpl()
                {
                        if (!_isRunning) return;

                        MyStopWatch w = new MyStopWatch();
                        _isRunning = false;
                        _channelManager.Stop();
                        _discoverer.StopBroadcasting();
                        _discoverer.StopListening();
                        Env.Logger.Log($"Stop {nameof(SuperDriveCore)} in {w.Elipsed}ms. Discoverer.IsListening={_instance._discoverer.IsListening}", nameof(SuperDriveCore));
                }


                public static T CreateConversation<T>(Device devInfo) where T : Requester //没有使用 new()约束，是因为那样要求T具有public 无参数构造函数。但我只想要internal的。
                {
                        var ci = typeof(T).GetTypeInfo().DeclaredConstructors.FirstOrDefault(c =>
                       {
                               var ps = c.GetParameters();
                                return ps.Length == 2
                                && ps[0].ParameterType == typeof(Device)
                                && ps[1].ParameterType == typeof(string);
                       });

                        if (ci == null)
                                throw new Exception($"Requester must have a constructor of ({nameof(Device)},{nameof(String)})");

                        var t = (T)ci.Invoke(new object[] { devInfo, StringHelper.NewRandomGUID() });

                        return t;
                }
                //这必须是个同步函数，因为他将被不能定义为异步函数的地方调用。
                public static void Dispose(TimeSpan timeSpan = default(TimeSpan))
                {
                        //正常退出时，如果还有正在进行的会话，则将这些会话序列化起来。
                        foreach (Conversation conv in Conversations.Values)
                        {
                                //TODO 新特性 每个会话还要设置超时。而且信息是明文的。这需要认真考虑。
                        }

                        //给每个已经连接的设备发Offline消息，广播下线消息不一定正确。因为不能保证所有人都能收到广播，建立连接的方式不一定只是局域网。
                        foreach (var device in Devices)
                        {
                                if (device.State == DeviceState.Connected)
                                {
                                        device.Disconnect();
                                }
                                device.NotifyImOffline();
                        }

                        Util.WaitAnySync(
                            () => Devices.Any(d => d.State != DeviceState.Connected),
                            TimeSpan.FromMilliseconds(300), 50);


                        Stop();
                        _instance._discoverer.Dispose();
                        _instance._channelManager.Dispose();
                        _instance._httpServer.Dispose();
                        _instance._httpServer = null;
                }
        }

        public interface IDeviceEventWatcher
        {
                void Watch(Device device);
                void UnWatch(Device device);
        }
}