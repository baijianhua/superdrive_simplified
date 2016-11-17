using ConnectTo.Foundation.Channel;
using ConnectTo.Foundation.Core;
using ConnectTo.Foundation.Helper;
using ConnectTo.Foundation.Messages;
using ConnectTo.Foundation.Common;
using System;
using System.Collections.Generic;
using ConnectTo.Foundation.Discovery;
using ConnectTo.Foundation.Protocol;
using System.Linq;
using System.Threading;
using Connect2.Foundation.Security;

namespace ConnectTo.Foundation.Business
{
    public interface IDeviceEventBinder
    {
        void BindEventsToDevice(Device device);
        void UnBindEventsFromDevice(Device device);
    }

    public class AppModel : IDisposable
    {
        event Action<Device> _deviceDiscovered;
        public event Action<Device> DeviceDiscovered
        {
            add
            {
                if (value == null) return;
                //如果这里传进来一个IDeviceEventBinder.BindEventsToDevice会怎么样？
                _deviceDiscovered += value;
                foreach (Device d in Devices)
                {
                    value.Invoke(d);
                }
            }
            remove
            {
                _deviceDiscovered -= value;
            }
        }
        public event Action<Responder> ConversationReceived = delegate { };
        public event Action<Conversation> ConversationRecovered = delegate { };
        public event Action<Error> ErrorHappened = delegate { };
        public void AddEventsForDevices(IDeviceEventBinder binder)
        {
            //已有设备和新设备都会关心这些事件。
            foreach (Device d in Devices)
            {
                binder.BindEventsToDevice(d);
            }
            //不要写DeviceDiscovered += binder.BindEventsToDevice; 那里面的value.Invoke会导致binder.BindEventsToDevice针对每个设备再被调用一遍。
            _deviceDiscovered += binder.BindEventsToDevice;
        }

        internal List<Device> GetDevices()
        {
            return Devices.ToList();
        }

        public void RemoveEventsForDevices(IDeviceEventBinder binder)
        {
            foreach (Device d in Devices)
            {
                binder.UnBindEventsFromDevice(d);
            }
            //NewDeviceEvent -= binder.BindEventsToDevice;
            _deviceDiscovered -= binder.BindEventsToDevice;
        }

        //TODO 重构，这里不该直接提供集合不能让别人修改这个集合。添加和移除的操作都应该更严谨。
        private HashSet<Device> Devices { get; set; }
        internal ChannelManager ChannelManager { get; set; }
        public Device LocalDevice { get; private set; }
        public Dictionary<string, Conversation> Conversations { get; internal set; }
        public List<Requester> SavedRequesters
        {
            get
            {
                //TODO 程序启动时，可以调用这个，从持久化容器中加载这些Requester.
                return new List<Requester>();
            }
        }
        private static AppModel _instance;
        public static AppModel Instance
        {
            get
            {
                Preconditions.Check(_instance != null, "AppModel must be Initialized before get instance");
                return _instance;
            }
        }
        public static bool IsInitialized { get { return _instance != null; } }
        public IDiscoverer Discoverer = null;
        private int DiscoverPort = 55638;
        private int ChannelPort = 51689;


        public static AppModel Init(Env container)
        {
            _instance = new AppModel(container);
            return _instance;

        }
        
        internal void AttachConversation(Conversation conversation)
        {
            if (conversation == null) return;
            //TODO bug 怎么会出现重复的Conversation?
            Conversations.Add(conversation.ID, conversation);
        }

        internal void RemoveConversation(Conversation conversation)
        {
            if (conversation == null) return;

            Conversations.Remove(conversation.ID);
        }

        internal void ProcessConversationMessage(Device device, ConversationMessage cm)
        {
            var conversation = Conversations.GetByID(cm.ConversationID);
            if (conversation == null)
            {
                if (cm is ConversationRequestMessage)
                {
                    var crm = cm as ConversationRequestMessage;
                    Responder responder = crm.CreateResponder();
                    responder.AppModel = this;
                    responder.ID = crm.ConversationID;
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
                    }else
                    {
                        var msg = new ConversationRejectMessage(RejectCode.DeSerializeFailed);
                        msg.ConversationID = cm.ConversationID;
                        device.Post(msg);
                    }
                }
            }
            else
            {
                conversation.Process(cm);
            }
        }
        private Conversation DeserializeConversation(ConversationRecoverRequestMessage conversationRecoverMessage)
        {
            //TODO 新特性， 反序列化会话。
            return null;
        }

        //Mutex appModelMutex;

        private AppModel(Env container)
        {
            //container.Logger.Error("Test Log");
            var cfg = Env.Instance.Config;
            Devices = new HashSet<Device>();
            Conversations = new Dictionary<string, Conversation>();
            LocalDevice = cfg.LocalDevice;
            var sm = Env.Instance.SecurityManager;
            var localConnectCode = sm.LoadString(SecurityManager.LOCAL_CONNECT_CODE);
            
            if(localConnectCode == null)
            {
                localConnectCode = StringHelper.NewRandomPassword();
                sm.SaveString(SecurityManager.LOCAL_CONNECT_CODE, localConnectCode);
            }
            LocalDevice.ConnectCode = localConnectCode;
            LocalDevice.DeviceType = container.DeviceType;
            container.InitLocalDeviceIPAdress(LocalDevice);

            
            
            ChannelManager = new ChannelManager(ChannelPort);
            ChannelManager.ChannelCreated += (channel) =>
            {
                Action<Packet> packetReceiver = null;
                packetReceiver = (Packet packet) =>
                {
                    Message message = Message.FromPacket(packet);
                    ConnectMessage cm = message as ConnectMessage;
                    //一个新连接的socket，在发送ConnectMessage之前发送其它Message，是不会理会的。
                    if (cm != null)
                    {
                        channel.PacketReceived -= packetReceiver;
                        var device = AddOrUpdateDevice(cm.Device);
                        if(device != null)
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
                    };
                };
                channel.PacketReceived += packetReceiver;
            };
            ChannelManager.PortOccupiedError += (int port) =>
            {
                Error error = new Error(Error.PortOccupied);
                error.AddError("port", port);
                ErrorHappened?.Invoke(error);
            };

            Discoverer = new LANDiscoverer(LocalDevice, DiscoverPort);
            container.NetworkChanged += (ips) =>
            {
                if (!string.IsNullOrEmpty(ips))
                {
                    container.InitLocalDeviceIPAdress(LocalDevice);
                    Discoverer.StartBroadcast();
                }               
            };
        }
        
        //private string GetConnectCodeFromSecureStorage()
        //{
        //    string connectCodeKey = "Connect2_ConnectCode";

        //    SecurePassword secureConnectCode = Env.Instance.SecurityManager.LoadPassword(connectCodeKey);
        //    string connectCode = (secureConnectCode != null && secureConnectCode.IsValid == true)
        //        ? SecurePassword.GetStringFromSecureString(secureConnectCode.SecureString)
        //        : null;

        //    if (string.IsNullOrEmpty(connectCode) == true)
        //    {
        //        connectCode = StringHelper.NewRandomPassword();
        //        Env.Instance.SecurityManager.SavePassword(connectCodeKey, new SecurePassword(connectCode));
        //    }

        //    if (secureConnectCode != null)
        //    {
        //        secureConnectCode.Dispose();
        //        secureConnectCode = null;
        //    }

        //    return connectCode;
        //}

        internal void RemoveDevice(Device device)
        {
            Preconditions.Check(device != null && !string.IsNullOrEmpty(device.ID));
            Devices.RemoveWhere(d => d.ID == device.ID);
        }

        public Device FindDevice(Device device)
        {
            Preconditions.Check(device != null && !string.IsNullOrEmpty(device.ID));
            return Devices.FirstOrDefault(d => d.ID == device.ID);
        }

        object locker = new object();
        /// <summary>
        /// 如果已经知道一个设备，则更新已有设备的信息。如果是一个新设备，把它添加到列表中。
        /// 
        /// 改这个要慎重，它被LANDiscoverer调用，还被Device的Connect调用。
        /// </summary>
        /// <param name="device"></param>
        /// <param name="needMerge"></param>
        /// <returns></returns>
        
        public Device AddOrUpdateDevice(Device device, bool needMerge = true)
        {
            //检查这个Device是否已知。
            lock(locker)
            {
                Preconditions.Check(device != null && !string.IsNullOrEmpty(device.ID));
                if (device.ID == LocalDevice.ID) return null;

                var existingDevice = Devices.FirstOrDefault(o => o.ID == device.ID);
                if (existingDevice == null)
                {
                    Devices.Add(device);
                    device.IsFirstSeen = true;
                    _deviceDiscovered?.Invoke(device);
                }
                else
                {
                    if (needMerge)
                    {
                        //如果这个设备原来已经存在了，那么其实应该返回原来的设备，只是需要将新发现的不同的地方复制到原来的设备上
                        //各事件都是绑定到原来的设备的。
                        existingDevice.CopyFrom(device);
                    }
                    //不管是否Merge,都要修改IP。
                    existingDevice.DefaultIP = device.DefaultIP;
                    device = existingDevice;
                    device.IsFirstSeen = false;
                }
                device.LastUpdateTime = Environment.TickCount;
                return device;
            }
        }

        



        //可以被启动停止多次，但只能release一次。
        private bool isRunning = false;
        //必须设置LocalDevice之后才能运行
        public void Start()
        {
            Preconditions.Check(LocalDevice != null, "Local Device Must be set before start");
            if (isRunning) return; //TODO 是否需要同步锁定？有没有别的实现？

            isRunning = true;

            Discoverer.StartBroadcast();
            Discoverer.StartListen();
            ChannelManager.Start();
        }
        public void Stop()
        {
            if (!isRunning) return;
            isRunning = false;
            ChannelManager.Stop();
            Discoverer.StopBroadcasting();
            Discoverer.StopListening();
        }


        public T CreateConversation<T>(Device devInfo) where T : Requester, new()
        {
            T t = new T();
            t.Peer = devInfo;
            t.ID = StringHelper.NewRandomGUID();
            t.AppModel = this;
            return t;
        }

        public void Dispose()
        {
            //正常退出时，如果还有正在进行的会话，则将这些会话序列化起来。
            foreach(Conversation conv in Conversations.Values)
            {
                //TODO 新特性 每个会话还要设置超时。而且信息是明文的。这需要认真考虑。
            }

            //给每个已经连接的设备发Offline消息，广播下线消息不一定正确。因为不能保证所有人都能收到广播，建立连接的方式不一定只是局域网。
            foreach (var device in Devices)
            {
                if(device.State == DeviceState.Connected)
                {
                    device.Disconnect();
                }
                device.NotifyImOffline();
            }
            //等待上面的数据包发送完毕。要不要等待全部回复，或者超时两个条件？
            Thread.Sleep(500);
            Stop();
            Discoverer.Dispose();
            ChannelManager.Dispose();
            //appModelMutex.ReleaseMutex();
        }
    }
}
