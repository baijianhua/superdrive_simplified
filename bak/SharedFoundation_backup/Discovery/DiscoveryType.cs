using System;

namespace ConnectTo.Foundation.Core
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