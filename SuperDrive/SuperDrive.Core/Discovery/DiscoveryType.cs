using System;

namespace SuperDrive.Core.Discovery
{
    [Flags]
    internal enum DiscoveryType
    {
        LAN,
        HOTSPOT,
        BLUETOOTH,
        INTERNET
    }
}