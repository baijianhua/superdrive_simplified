using System;

using ConnectTo.Foundation.Core;
using ConnectTo.Foundation.Messages;

namespace ConnectTo.Foundation.Discovery
{
    public interface IDiscoverer: IDisposable
    {
        bool CanDiscover { get; }
        //可以只监听，但是不发送广播。这样会应答别人的上线消息，但自己不会主动去告知别人自己上线。机顶盒适用。
        void StartListen();
        void StopListening();
        //如果不调用下面的函数，那别人就发现不了这个设备，但这个设备可以发现别人(通过调用StartListen)
        void StartBroadcast();
        void StopBroadcasting();
    }
}
