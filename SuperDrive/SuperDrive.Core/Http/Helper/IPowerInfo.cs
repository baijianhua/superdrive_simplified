using System;

namespace SuperDrive.Core.Helper
{
    public interface IPowerInfo
    {
        int BatteryLevel { get; }

        bool IsBatteryLow { get; }

        bool IsBatteryCharging { get; }

        event EventHandler PowerInfoChanged;
    }
}
