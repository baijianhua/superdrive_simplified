using System;

namespace ConnectTo.Foundation.Power
{
    public interface IPowerInfo
    {
        int BatteryLevel { get; }

        bool IsBatteryLow { get; }

        bool IsBatteryCharging { get; }

        event EventHandler PowerInfoChanged;
    }
}
