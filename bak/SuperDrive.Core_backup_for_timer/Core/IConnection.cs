using ConnectTo.Foundation.Messages;
using System;
using System.Collections.Generic;

namespace ConnectTo.Foundation.Core
{
    public interface IConnection
    {
        DeviceInfo Peer { get; }
        
        void CancelConnect();

        void Accept();

        void Reject();

        void Disconnect();
    }
}
