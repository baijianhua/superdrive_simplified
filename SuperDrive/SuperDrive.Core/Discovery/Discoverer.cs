using System;
using System.Threading;
using System.Threading.Tasks;
using SuperDrive.Core.Support;

namespace SuperDrive.Core.Discovery
{
    public abstract class Discoverer: IDisposable
    {
        bool CanDiscover { get; }
        //可以只监听，但是不发送广播。这样会应答别人的上线消息，但自己不会主动去告知别人自己上线。机顶盒适用。
        //private CancellationTokenSource _listenCts;
        //private Task _listenTask;
        private TaskWrapper _listenerWrapper;
        public void StartListen()
        {
            if (_listenerWrapper != null) return;

            _listenerWrapper = new TaskWrapper("Discovery.Listen",ListenAction,TaskCreationOptions.LongRunning);
            _listenerWrapper.Start();
            
        }

        public bool IsListening => _listenerWrapper?.IsRunning == true;
        protected abstract Task ListenAction(CancellationToken token);

        public void StopListening()
        {
            if (_listenerWrapper != null)
            {
                Env.Logger.Log($"Try to stop listening islistening={IsListening}");
                _listenerWrapper?.Stop();
                Env.Logger.Log($"after stop listening islistening={IsListening}");
                _listenerWrapper = null;
            }
        }
        

        //如果不调用下面的函数，那别人就发现不了这个设备，但这个设备可以发现别人(通过调用StartListen)
        private TaskWrapper _broadTaskWrapper;

        //这个函数是异步函数，但是别人不需要等待它。所以返回void。
        public void StartBroadcast()
        {
            //停掉上一次广播。这和Listen不一样。Listen停掉上一次是没意义的。但Broadcast有意义。比如更新了IP地址。
            if (_broadTaskWrapper != null)
                StopBroadcasting();

            //开启新的广播
            _broadTaskWrapper = new TaskWrapper("Discovery.Broadcast",BroadCastAction);
            _broadTaskWrapper.Start();
        }

        protected abstract Task BroadCastAction(CancellationToken token);

        public void StopBroadcasting()
        {
            _broadTaskWrapper?.Stop();
            _broadTaskWrapper = null;
        }

        public void Dispose()
        {
            StopBroadcasting();
            StopListening();
        }
    }
}
