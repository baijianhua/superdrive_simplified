using ConnectTo.Foundation.Common;
using ConnectTo.Foundation.Core;
using NLog;
using System;
using System.ComponentModel;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using ConnectTo.Foundation.Protocol;
using ConnectTo.Foundation.Messages;
using ConnectTo.Foundation.Business;
using ConnectTo.Foundation.Helper;
using System.Diagnostics;
using System.Linq;
using System.Collections.Generic;

namespace ConnectTo.Foundation.Channel
{
    internal class ChannelManager : IDisposable
    {
        private int port;
        private BackgroundWorker listenerWorker;
        private AutoResetEvent listenWaiter;

        internal event Action<int> PortOccupiedError;
        internal event Action<IChannel> ChannelCreated;




        internal ChannelManager(int port = 51689)
        {
            this.port = port;
        }

        private void StartListen()
        {
            listenWaiter = new AutoResetEvent(false);
            listenerWorker = new BackgroundWorker { WorkerSupportsCancellation = true };
            listenerWorker.DoWork += (sender, args) =>

            {
                var worker = sender as BackgroundWorker;
                if (worker == null) return;

                var listener = new TcpListener(IPAddress.Any, port);
                try
                {
                    listener.Start();
                }
                catch (System.Exception ex)
                {
                    PortOccupiedError?.Invoke(port);
                    return;
                }

                while (!worker.CancellationPending)
                {
                    listener.BeginAcceptTcpClient(OnSocketReceived, listener);
                    listenWaiter.WaitOne();
                }
                listener.Stop();
            };
            listenerWorker.RunWorkerAsync();
        }

        private bool isRunning = false;

        internal void Stop()
        {
            if (!isRunning) return;
            isRunning = false;
        }


        internal void Start()
        {
            if (isRunning) return;
            isRunning = true;
            StartListen();
        }

        private void OnSocketReceived(IAsyncResult ar)
        {
            var listener = (TcpListener)ar.AsyncState;
            TcpClient client;

            try
            {
                client = listener.EndAcceptTcpClient(ar);
                listenWaiter.Set();
                var channel = new TcpChannel(client);
                channel.IsInitiative = false;
                Env.Instance.Logger.Trace("HandleIncomingUnsecureRequest: New command channel accepted from: {0}", client.Client.RemoteEndPoint);
                //没有发送任何事件，直到收到Connect消息。
                ChannelCreated?.Invoke(channel);
            }
            catch (SocketException e)
            {
                //TODO 要不要在这里结束worker?
                listenWaiter.Set();
                return;
            }
            catch (ObjectDisposedException e)
            {
                //TODO 谁抛出这个异常的？
                Debug.WriteLine("Exception happened  OnSocketReceived"+e.StackTrace);

                return;
            }
        }


        /// <summary>
        ///
        /// </summary>
        /// <param name="device"></param>
        /// <param name="channelCreated">连接成功回调</param>
        /// <param name="createChannelFailed">连接失败回调</param>
        /// <param name="timeout">最长连接时间</param>
        /// <param name="interval">若连接出现失败，多久后重试？现在不用这个值了</param>
        /// <param name="maxRetryCount">若连接失败，最多允许重试几次？</param>
        internal void CreateChannel(Device device, Action<IChannel> channelCreated, Action createChannelFailed, double timeout, int interval)
        {
            Preconditions.Check(interval > 1000);

            var ips = string.IsNullOrEmpty(device.IPAddress) ? new List<string>() : device.IPAddress.Split(new string[] { "#" }, StringSplitOptions.RemoveEmptyEntries).ToList();
            if (!string.IsNullOrEmpty(device.DefaultIP))
            {
                //把default ip调整到前面
                if (ips.Any(ip => ip == device.DefaultIP))
                {
                    ips.Remove(device.DefaultIP);
                }
                ips.Insert(0, device.DefaultIP);
            }
            ips.RemoveAll(o => !IsValidRemoteIP(o, device));

            if (ips.Count == 0)
            {
                Env.Instance.Logger.Error("createChannelFailed, no available remote ip address");
                createChannelFailed?.Invoke();
                return;
            }




            ips.ForEach(ip =>
            {
                try {
#if WIN
                    var client = new TcpClient(new IPEndPoint(IPAddress.Any, TCPPortPool.Connect2TCPPortPool.GetPort()));
#else
                    var client = new TcpClient();

                    //TODO BUG 这样做之后，连接就变得特别困难，为什么？是出栈端口在一段时间之内不能复用吗？
                    //for (var i = 50001; i <= 51000; i++)
                    //{
                    //    try
                    //    {
                    //        client = new TcpClient(new IPEndPoint(IPAddress.Any, i));

                    //        break;
                    //    }
                    //    catch
                    //    {
                    //        // ignored
                    //    }
                    //}
#endif
                    var connectHolder = new ConnectHolder
                    {
                        Device = device,
                        ChannelCreated = channelCreated,
                        TimeoutAction = createChannelFailed
                    };
                    Util.RunLater(() => connectHolder.Timeout(), timeout);

                    client.BeginConnect(ip, port, connectHolder.BeginConnectCallback, client);
                }
                catch (Exception e)
                {
                    Env.Instance.Logger.Warn("ConnectSocket Error:" + e.StackTrace);
                }
            });
        }
        class ConnectHolder
        {
            internal Device Device;
            internal Action<IChannel> ChannelCreated;
            internal Action TimeoutAction;
            internal bool IsTimeout = false;
            private object locker = new object();
            private bool IsConnected = false;
            internal void BeginConnectCallback(IAsyncResult ar)
            {
                lock (locker)
                {
                    //锁定，防止同时连接两个ip地址，一前一后的返回。后返回的结果冲掉了前面的。
                    //未来如果实现同时支持多个channel，可以考虑换个实现。

                    if (Device.State == DeviceState.Connected || IsTimeout) return;

                    try
                    {
                        TcpClient client = (TcpClient)ar.AsyncState;
                        client.EndConnect(ar);
                        IPEndPoint remote = client.Client.RemoteEndPoint as IPEndPoint;
                        if (remote != null)
                        {
                            var ip = remote.Address.ToString();
                            if (client.Connected)
                            {
                                IsConnected = true;
                                Env.Instance.Logger.Info("ConnectSocket: Connected!");
                                TcpChannel channel = new TcpChannel(client);
                                channel.IsInitiative = true;
                                channel.RemoteIP = ip;
                                Device.DefaultIP = ip;
                                ChannelCreated?.Invoke(channel);
                            }
                            else
                            {
                                Env.Instance.Logger.Warn("ConnectSocket[" + ip + "] : Timed out...");
                                client.Close();
                            }
                        }
                        else
                        {
                            Env.Instance.Logger.Warn("ConnectSocket failed");
                            client?.Close();
                        }
                    }
                    catch(Exception e)
                    {
                        Env.Instance.Logger.Error(e,"ConnectSocket failed");
                    }
                }
            }

            internal void Timeout()
            {
                if (IsConnected) return;
                lock (locker)
                {
                    IsTimeout = true;
                    TimeoutAction?.Invoke();
                }
            }
        }

        private readonly string _DefaultIP = @"192.168.173.1";
        private bool IsValidRemoteIP( string ip, Device remoteDevice)
        {
            if (ip != _DefaultIP)
            {
                return true;
            }
            else
            {
                if (remoteDevice.DeviceType == DeviceType.PC
                    && AppModel.Instance.LocalDevice.DeviceType == DeviceType.PC
                    && AppModel.Instance.LocalDevice.IPAddress.Contains(_DefaultIP))
                    return false;
                else
                    return true;
            }
        }
        public void Dispose()
        {
            listenerWorker?.CancelAsync();
            listenWaiter?.Set();
            //listenWaiter.Close();
        }
    }
}
