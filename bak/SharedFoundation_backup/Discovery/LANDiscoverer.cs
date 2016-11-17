using System;

using ConnectTo.Foundation.Core;
using ConnectTo.Foundation.Messages;
using ConnectTo.Foundation.Protocol;
using ConnectTo.Foundation.Common;
using System.Net.Sockets;
using System.Net;
using System.Timers;
using ConnectTo.Foundation.Business;
using Connect2.Foundation.Security;
using System.Linq;
using ConnectTo.Foundation.Helper;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Diagnostics;

namespace ConnectTo.Foundation.Discovery
{

    internal class LANDiscoverer : IDiscoverer, IDisposable
    {
        internal const string DefaultBroadcastAddress = "255.255.255.255";
        private const int MAX_BROADCAST_TIME = 3;
        private readonly Device localInfo;
        private UDP udp;
        private System.Timers.Timer broadcastTimer;
        private readonly int[] intervals = { 1, 3000, 6000, 9000 };
        private int broadCastCount = 0;
        public const int DefaultBroadCastPort = 55638;
        public const int DefaultOutPort = 55255;
        private int BroadcastPort { get; set; }
        IPAddress[] localIPs;

        internal LANDiscoverer(Device localInfo, int port = DefaultBroadCastPort)
        {
            Preconditions.Check(localInfo != null);
            Preconditions.Check<ArgumentException>(port > -1);

            this.localInfo = localInfo;
            BroadcastPort = port;
            udp = UDP.Prepare(DefaultBroadcastAddress, port);
            //udp.OnPacketReceived += OnPacketReceived;
        }
        //public bool CanDiscover =>Env.Instance.CanDiscover; 这是怎么等价于下面的？ 如果需要set怎么弄？
        public bool CanDiscover { get { return Env.Instance.CanDiscover; } }
        UdpClient listenClient;
        //private bool isListening;

        public void StartListen()
        {
            try
            {
                listenClient = new UdpClient { ExclusiveAddressUse = false, MulticastLoopback = false };
                listenClient.Client.Bind(new IPEndPoint(IPAddress.Any, BroadcastPort));
                var ar = listenClient.BeginReceive(UdpDataReceived, listenClient);
            }
            catch (Exception ex)
            {
                Env.Instance.Logger.Error(ex, "Can not start listener");
            }

        }

        private void UdpDataReceived(IAsyncResult ar)
        {
            try
            {
                var remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
                UdpClient client = (UdpClient)ar.AsyncState;
                byte[] bytes = client.EndReceive(ar, ref remoteEndPoint);
                //很奇怪，为什么收到的这个remoteEndPoint与自己创建的UdpClient不是等效的？用这个udpClient发数据出去，对方就收不到。
                Console.WriteLine("receive a udp packet from " + Util.AddressToString(remoteEndPoint.Address));
                ar = client.BeginReceive(UdpDataReceived, client);
                ProcessUdpBytes(bytes, remoteEndPoint, client);
            }
            catch (Exception ex)
            {
                Env.Instance.Logger.Error(ex, "Receive data failed.");
            }

        }

        private void ProcessUdpBytes(byte[] bytes, IPEndPoint remoteEndPoint, UdpClient client)
        {
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    if (!localIPs.Any(addr => addr.Equals(remoteEndPoint.Address)))
                    {
                        if (bytes != null && bytes.Length >= PacketHeader.DefaultLength)
                        {
                            var header = PacketHeader.FromBytes(bytes, 0, PacketHeader.DefaultLength);

                            if (header != null)
                            {
                                var body = new byte[header.BodyLength];
                                Buffer.BlockCopy(bytes, PacketHeader.DefaultLength, body, 0, body.Length);
                                var packet = new Packet(header, body);
                                OnPacketReceived(packet, remoteEndPoint, client);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Env.Instance.Logger.Error(ex, "Process udp bytes error.");
                }

            });
        }

        public void StopListening()
        {
            listenClient.Close();
            listenClient = null;
        }
        public void StopBroadcasting()
        {
            broadcastTimer?.Stop();
        }
        public void StartBroadcast()
        {
            if (CanDiscover)
            {
                //重复调用也没关系。
                localIPs = Env.Instance.GetIPAddresses();
                udp?.InitSendClients(BroadcastPort);
                broadcastTimer?.Stop();

                if (broadcastTimer == null)
                {
                    broadcastTimer = new System.Timers.Timer();
                    broadcastTimer.AutoReset = false;
                    broadcastTimer.Elapsed += BroadcastTimer_Elapsed;
                }

                broadCastCount = 0;
                broadcastTimer.Interval = intervals[broadCastCount]; //立即开始广播一次，不能==0
                broadcastTimer.Start();
            }
        }
        private void BroadcastTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            var idx = broadCastCount >= intervals.Length ? intervals.Length - 1 : broadCastCount;
            if (broadCastCount++ > MAX_BROADCAST_TIME) //有限次数广播。如果是无限次数的，当broadcastCount>设定值，idx = intervals.Length-1
            {
                broadcastTimer.Stop();
            }
            else
            {
                var message = new OnlineMessage(localInfo);
                var bytes = message.ToPacket().ToBytes();
                udp.Send(null, bytes, udp.broadcastEndpoint);


                var interval = intervals[idx];
                broadcastTimer.Interval = interval;
                broadcastTimer.Start();
            }
        }

        private void OnPacketReceived(Packet packet, IPEndPoint remote, UdpClient client)
        {
            //排队处理收到的消息。如果不排队，一个消息会发回来很多次，多线程调试很麻烦。
            var message = Message.FromPacket(packet);

            if (message is AbstractDeviceMessage)
            {
                var msg = message as AbstractDeviceMessage;
                var peer = msg.Device;

                if (peer != null && peer.IsValid && peer.ID != localInfo.ID) //不是本机发来的。
                {
                    //AppModel就是神... 没必要再把这些东西中转一下在AppModel里面处理。
                    //如果有精力，可以把发现和Channel的部分再整理一下。
                    if (msg is OfflineMessage)
                    {
                        var existingDevice = AppModel.Instance.FindDevice(peer);
                        existingDevice?.Offline();
                        return;
                    }

                    peer = AppModel.Instance.AddOrUpdateDevice(peer);

                    if (peer != null)
                    {
                        peer.DefaultIP = Util.AddressToString(remote.Address);
                        peer.DiscoveryType = DiscoveryType.LAN;

                        if (!peer.IsFirstSeen)
                        {
                            peer.Refresh();
                        }

                        if (msg is OnlineMessage && localInfo.State != DeviceState.OffLine)
                        {
                            peer.Acknowledge();
                        }
                    }
                }
            }
        }

        public void Dispose()
        {
            udp?.Dispose();
        }
    }
    /// <summary>
    /// 对底层封装，为跨平台服务。
    /// </summary>
    class UDP : IDisposable
    {
        internal event Action<Packet, IPEndPoint, UdpClient> OnPacketReceived;
        internal IPEndPoint broadcastEndpoint;
        private List<UdpClient> clients;
        private UdpClient listenClient;
        private object obj = new object();
        private BackgroundWorker listenerWorker;
        private int broadcastPort = 0;
        private IPAddress[] localIPs;
        private bool isListening = false;

        UDP(int port)
        {
            broadcastEndpoint = new IPEndPoint(IPAddress.Broadcast, port);
            this.broadcastPort = port;
        }

        internal void InitSendClients(int port)
        {
            //如果这个改成55638，反倒不行。防火墙的进出规则是什么？多个udpclient绑定到同一个端口又会是什么行为？
            //其实还有个解决方案，其实更好，本来就没必要创建重复的UDPClient。就是设置Device的DefaultIP的时候，把这个IP对应的UDPClient也给传进去,复用这个UDPClient。
            //有点麻烦的是，不知道是哪个udpClient能对应到远端的device。收到UDP消息的时候，应该可以查到本地的ip地址吧？


            localIPs = Env.Instance.GetIPAddresses();
            if (clients != null)
            {
                clients.ForEach(c =>
                {
                    try
                    {
                        c.Close();
                    }
                    catch (Exception e) { }
                });
            }
            clients = new List<UdpClient>();
            foreach (IPAddress ip in localIPs)
            {
                try
                {
                    var udpclient = new UdpClient();
                    udpclient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, 1);
                    udpclient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, 1);

                    udpclient.Client.Bind(new IPEndPoint(ip, LANDiscoverer.DefaultOutPort));

                    clients.Add(udpclient);
                }
                catch (Exception e)
                {
                    Env.Instance.Logger.Error(e, "can not initialize upd broadcaster on " + ip.ToString());
                }
            }
        }

        internal static UDP Prepare(string defaultBroadcastAddress, int defaultPort)
        {
            UDP udp = new UDP(defaultPort);
            //TODO 此处需要怎么设置？研究一下组播，应该可以满足需求。
            //udp.udpClient.JoinMulticastGroup(ipaddress, defaultPort);
            return udp;
        }


        internal void Send(UdpClient client, byte[] data, IPEndPoint remote = null)
        {
            if (data != null && data.Length > 0)
            {
                if (remote == null) remote = broadcastEndpoint;

                // var dataBytes = data;
                //
                // if (dataBytes == null) return;

                if (client != null)
                {
                    udpSend(client, data, remote);
                }
                else if (clients != null)
                {
                    clients.ForEach(c =>
                    {
                        udpSend(c, data, remote);
                    });
                }
            }
        }

        void udpSend(UdpClient client, byte[] dataBytes, IPEndPoint remote)
        {
            try
            {
                client.Send(dataBytes, dataBytes.Length, remote);
            }
            catch (Exception e)
            {
                //ignore.
            }
        }


        public void Dispose()
        {
            lock (obj)
            {
                listenClient?.Close();
                listenClient = null;

                clients.ForEach(c =>
                {
                    try
                    {
                        c.Close();
                    }
                    catch (Exception e)
                    {
                    }
                });
                clients = null;
            }
        }
    }
}
