using System;
using System.Collections.Generic;

using ConnectTo.Foundation.Core;

namespace ConnectTo.Foundation.Discovery
{
    public interface IDiscoveryManager
    {
        bool CanDiscover { get; }

        void StartDiscover();

        IEnumerable<Device> Peers { get; }

        event Action<Device> PeerChanged; 

        void StopDiscover();
    }
}
