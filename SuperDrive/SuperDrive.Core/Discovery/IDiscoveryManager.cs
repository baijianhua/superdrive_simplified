using System;
using System.Collections.Generic;
using SuperDrive.Core.Enitity;

namespace SuperDrive.Core.Discovery
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
