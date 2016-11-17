using ConnectTo.Foundation.Core;
using System;
using ConnectTo.Foundation.Business;

namespace ConnectTo.Foundation.Channel
{
    internal abstract class ChannelManager : IDisposable
    {
        protected int port;

        public int Port { get; internal set; }

        public event Action<int> PortOccupiedError;
        public event Action<IChannel> ChannelCreated;




        internal ChannelManager(int port = 51689)
        {
            this.port = port;
        }

        public abstract void Stop();


        public abstract void Start();

        /// <summary>
        ///
        /// </summary>
        /// <param name="device"></param>
        /// <param name="channelCreated">连接成功回调</param>
        /// <param name="createChannelFailed">连接失败回调</param>
        /// <param name="timeout">最长连接时间</param>
        /// <param name="interval">若连接出现失败，多久后重试？现在不用这个值了</param>
        /// <param name="maxRetryCount">若连接失败，最多允许重试几次？</param>
        public abstract void CreateChannel(Device device, Action<IChannel> channelCreated, Action createChannelFailed,
            double timeout, int interval);
        

        public abstract void Dispose();
    }
}
