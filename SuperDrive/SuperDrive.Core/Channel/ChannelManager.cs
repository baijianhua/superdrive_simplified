using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using SuperDrive.Core.Annotations;
using SuperDrive.Core.Enitity;
using SuperDrive.Core.Support;

namespace SuperDrive.Core.Channel
{
    public class ChannelManager : IDisposable
    {
        private int port;
        internal event Action<IChannel> ChannelCreated;

        internal ChannelManager(int port)
        {
            this.port = port;
            _listener = new TaskWrapper("Channel Manager.Listen",ListenAction);
        }

        private TaskWrapper _listener;
        //private CancellationTokenSource _channelCancellSource;
        //private CancellationTokenSource _source;
        private async Task ListenAction(CancellationToken token)
        {
            TcpListener tcpListener = new TcpListener(IPAddress.Any, port);
            Action cleanup = () =>
            {
                tcpListener?.Stop();
                tcpListener = null;
            };
            token.Register(cleanup);

            try
            {
                tcpListener.Start();
            }
            catch (Exception)
            {
                throw new Exception("Can not start ChannelManager, Port is occupied.");
            }

            while (!token.IsCancellationRequested)
            {
                TcpClient client = await tcpListener.AcceptTcpClientAsync();
#pragma warning disable 4014
                Task.Run(() =>
#pragma warning restore 4014
                {
                    var channel = new TcpChannel(client);
                    channel.IsInitiative = false;
                    //没有发送任何事件，直到收到Connect消息。
                    ChannelCreated?.Invoke(channel);
                },token);
            }
            cleanup();
            //TODO 需要测试！ 如果_listenTask.Wait, 这里把_listenTask设置为null，是不会报错。
            Stop();
        }

        internal void Stop()
        {
            if (!_listener.IsRunning) return;
            
            _listener.Stop();
        }

        

        internal void Start()
        {
            if (_listener?.IsRunning == true) return;
            _listener?.Start();
        }

        
        internal Task<IChannel> CreateChannel(Device device)
        {
            var ips = string.IsNullOrEmpty(device.IpAddress) ? new List<string>() : device.IpAddress.Split(new[] { "#" }, StringSplitOptions.RemoveEmptyEntries).ToList();
            TaskCompletionSource<IChannel> tcs = new TaskCompletionSource<IChannel>();
            if (!string.IsNullOrEmpty(device.DefaultIp))
            {
                //把default ip调整到前面
                ips.RemoveAll(ip => ip == device.DefaultIp);
                ips.Insert(0, device.DefaultIp);
            }
            ips.RemoveAll(o => !IsValidRemoteIP(o, device));
            List<string> candidateIps = new List<string>(ips);
            if (ips.Count == 0)
                tcs.SetException(new Exception("createChannelFailed, no available remote ip address"));
            else
                //启动连接的多个线程。每个线程尝试连接一个ip地址。
                ips.ForEach(ip => ConnectSingleIpAsync(ip, tcs, candidateIps));
            
            return tcs.Task;
        }

        private void ConnectSingleIpAsync(string ip, [NotNull] TaskCompletionSource<IChannel> tcs,ICollection<string> candidateIps)
        {
            Task.Run( async ()=>
            {
                bool socketAdopted = false;
                var client = new TcpClient();
                Action disposeTcpClient = () =>
                {
                    // ReSharper disable once AccessToModifiedClosure
                    if (socketAdopted) return;

                    client?.Dispose(); 
                    client = null;
                    Env.Logger.Log($"clean up [{ip}]", nameof(ChannelManager));
                };

                try
                {
                    Env.Logger.Log($"Try to connect to [{ip}]", nameof(ChannelManager));
                    await client.ConnectAsync(ip, port); //这个耗时的过程不能取消，cancel token就没多大意义了。
                    if (!tcs.IsEnded() && client.Connected)
                    {
                        IPEndPoint remote = client.Client.RemoteEndPoint as IPEndPoint;
                        if (remote != null)
                        {
                            socketAdopted = true;
                            TcpChannel channel = new TcpChannel(client)
                            {
                                IsInitiative = true,
                                RemoteIP = remote.Address.ToString()
                            };
                            socketAdopted = tcs.TrySetResult(channel);
                            if(socketAdopted) Env.Logger.Log($"Connected to [{ip}] successfully", nameof(ChannelManager));
                            
                        }
                    }
                    //如果这个不成功，不要设置result，让别的连接尝试做这个工作。
                }
                catch (Exception e)
                {
                    //如果全都失败应该早点退出。
                    Env.Logger.Log($"Create TCP Client failed {ip} reason {e.Message}", nameof(ChannelManager));
                }
                finally
                {
                    if (!socketAdopted)
                    {
                        candidateIps.Remove(ip);
                        if (candidateIps.Count == 0) tcs.TrySetResult(null);

                        disposeTcpClient();
                    }
                }
            });
        }
        const string DefaultIp = @"192.168.173.1";
        //private TcpListener _tcpListener;

        public bool IsListening => _listener.IsRunning;

        private bool IsValidRemoteIP( string ip, Device remoteDevice)
        {
            if (ip != DefaultIp) return true;
            
            if (remoteDevice.DeviceType == DeviceType.PC
                && SuperDriveCore.LocalDevice.DeviceType == DeviceType.PC
                && SuperDriveCore.LocalDevice.IpAddress.Contains(DefaultIp))
                return false;

            return true;
        }
        public void Dispose()
        {
            Stop();
            _listener = null;
        }
    }
}
