using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using SuperDrive.Core.Annotations;
using SuperDrive.Core.Channel.Protocol;
using SuperDrive.Core.Enitity;
using SuperDrive.Core.Messages;
using SuperDrive.Core.Support;

namespace SuperDrive.Core.Discovery
{

        public class LanDiscoverer : Discoverer, IDisposable
        {
                private readonly int[] _intervals = { 1 };
                private readonly int _broadcastPort;
                public UdpClient ListenClient { get; private set; }
                private readonly IPEndPoint _broadcastEndpoint;

                internal LanDiscoverer([NotNull] Device localInfo, int port)
                {
                        Support.Util.Check<ArgumentException>(port > -1);
                        _broadcastPort = port;
                        _broadcastEndpoint = new IPEndPoint(IPAddress.Broadcast, port);
                }

                public bool CanDiscover => Env.Network.CanDiscover;

                //别人await这个函数，这个函数是async的，并且返回Task，就会被包装成一个Task,独立的运行。
                //这个函数体本身已经是在一个独立的Task里面了，或者说，这个函数体是构造Task的原料。

                //如果别人await，取到的是运行结果（等到Cancel或者SetResult），不await，取到的就是这个
                //自动生成的Task。       
                protected override async Task ListenAction(CancellationToken token)
                {
                        ListenClient = new UdpClient { ExclusiveAddressUse = false, MulticastLoopback = false, EnableBroadcast = true };
                        //Token Cancel时，将调用这个函数。
                        Action cleanup = () =>
                        {
                                ListenClient?.Dispose();
                                ListenClient = null;
                        };
                        token.Register(cleanup);

                        //Bind,表示从特定ip进行收发，Bind到IPAddress.Any，表示任何网卡的数据都接收。
                        //而且Send也确实能Send到单一网卡，但问题是Broadcast不行。
                        ListenClient.Client.Bind(new IPEndPoint(IPAddress.Any, _broadcastPort));
                        Env.Logger.Log("============Listen Started============", nameof(LanDiscoverer));

                        try
                        {
                                while (!token.IsCancellationRequested)
                                {
                                        Env.Logger.Log("Before await receive async", nameof(LanDiscoverer));
                                        //不需要送回原始上下文。随便在哪个线程完成都行。
                                        var result = await ListenClient.ReceiveAsync().ConfigureAwait(false); //一旦到了await这里，这个函数就返回了，所以调用者不会阻塞在这里。
                                        Env.Logger.Log("after await receive async", nameof(LanDiscoverer));
                                        var remoteIp = result.RemoteEndPoint.Address.ToString();
                                        var ips = Env.Network.GetIpAddresses();
                                        var s = ips.Contains(remoteIp) ? "localhost" : remoteIp;
                                        //Env.Logger.Log($"Receive udp message from [{s}] ", nameof(LANDiscoverer));
                                        if (!token.IsCancellationRequested && !ips.Contains(remoteIp))
                                        {
                                                Env.PostSequencialTask(
                                                    () => ProcessUdpBytesAsync(result.Buffer, result.RemoteEndPoint),
                                                    isValid: () => true,
                                                    toStringImpl: () => $"Process udp message from [{s}]"
                                                    );
                                        }
                                }
                        }
                        catch (Exception ex)
                        {
                                //有一种情况，整个函数已经执行完毕了，但因为整个函数是异步的，这个函数派发出去的线程又回来了，
                                //并且继续执行下面这句话
                                if (IsListening)
                                {
                                        Env.Logger.Log("Listen Udp Client Exception", nameof(LanDiscoverer), stackTrace: ex.StackTrace, level: LogLevel.Error);
                                }
                        }

                        if (IsListening)
                        {
                                cleanup();
                                Env.Logger.Log("============Listen Stopped============", nameof(LanDiscoverer));
                                //如果有任何异常，需要清理，但如果是别人主动调用StopListen,这就又把StopListen调用了一遍。
                                //如果发生了异常，不清理的话，下次的状态又不对。
                                //如果直接运行StopListening，有死锁的可能。因为StopListening 通过listenerWrapper等待这个函数结束。
#pragma warning disable 4014
                                Task.Run(() => StopListening());
#pragma warning restore 4014
                        }

                }


                protected override async Task BroadCastAction(CancellationToken token)
                {
                        try
                        {
                                foreach (var interval in _intervals)
                                {
                                        await Task.Delay(TimeSpan.FromSeconds(interval), token);
                                        var message = new OnlineMessage(SuperDriveCore.LocalDevice);
                                        var bytes = message.ToPacket().ToBytes();
                                        if (token.IsCancellationRequested) break;
                                        Env.Logger.Log("+++Broadcast out" + message, nameof(LanDiscoverer));
                                        SendUdp(bytes, _broadcastEndpoint);
                                }
                        }
                        catch (Exception e)
                        {
                                Env.Logger.Log("Broadcast exception", stackTrace: e.StackTrace);
                        }
                        //直接运行可能死锁。
#pragma warning disable 4014
                        // ReSharper disable once MethodSupportsCancellation
                        Task.Run(() => StopBroadcasting());
#pragma warning restore 4014
                }

                Task ProcessUdpBytesAsync(byte[] bytes, IPEndPoint remoteEndPoint)
                {
                        //为了避免处理时间过程，影响接收下一个udp消息，启动到一个新的线程中。这里如果再用await呢？
                        return Task.Run(() =>
                        {
                                try
                                {
                                        if (bytes != null && bytes.Length >= PacketHeader.DefaultLength)
                                        {
                                                var header = PacketHeader.FromBytes(bytes, 0, PacketHeader.DefaultLength);

                                                if (header != null)
                                                {
                                                        var body = new byte[header.BodyLength];
                                                        Buffer.BlockCopy(bytes, PacketHeader.DefaultLength, body, 0, body.Length);
                                                        var packet = new Packet(header, body);
                                                        OnPacketReceived(packet, remoteEndPoint);
                                                }
                                        }
                                }
                                catch (Exception ex)
                                {
                                        Env.Logger.Log("Process udp bytes error.", stackTrace: ex.StackTrace);
                                }
                        });
                }



                private void OnPacketReceived(Packet packet, IPEndPoint remote)
                {
                        //排队处理收到的消息。如果不排队，一个消息会发回来很多次，多线程调试很麻烦。
                        var message = Message.FromPacket(packet);
                        var msg = message as AbstractDeviceMessage;
                        var peer = msg?.Device;
                        if (peer == null || !peer.IsValid || peer.Id == SuperDriveCore.LocalDevice.Id) return;

                        Env.Logger.Log(message.ToString(), nameof(LanDiscoverer));
                        //AppModel就是神... 没必要再把这些东西中转一下在AppModel里面处理。
                        //如果有精力，可以把发现和Channel的部分再整理一下。
                        if (msg is OfflineMessage)
                        {
                                var existingDevice = SuperDriveCore.Devices.FirstOrDefault(d => d.Id == peer.Id);
                                existingDevice?.Offline();
                                return;
                        }
                        peer.DefaultIp = Util.AddressToString(remote.Address);
                        peer.DiscoveryType = DiscoveryType.LAN;

                        peer = SuperDriveCore.AddOrUpdateDevice(peer);

                        if (peer == null) return;

                        if (!peer.IsFirstSeen) peer.Refresh();
                        if (msg is OnlineMessage && SuperDriveCore.LocalDevice.State != DeviceState.OffLine)
                                peer.Acknowledge();
                }
                public void SendUdp(byte[] bytes, IPEndPoint endpoit) => ListenClient.SendAsync(bytes, bytes.Length, endpoit);
        }
}
