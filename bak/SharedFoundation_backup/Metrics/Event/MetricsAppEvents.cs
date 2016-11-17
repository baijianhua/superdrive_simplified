using System;

namespace Metrics.Event
{
    public class MetricsAppLaunchedEvent : MetricsEvent
    {
        public MetricsAppLaunchedEvent(bool secureModeEnabled) 
            : base(CategoryApp,
                   ActionLaunched,
                   new MetricsParameter(ParameterSecureModeEnabled, FormatSecureModeString(secureModeEnabled)))
        {
        }

        private static string FormatSecureModeString(bool secureModeEnabled)
        {
            return secureModeEnabled ? "secure" : "easy";
        }
    }

    public class MetricsAppTerminatedEvent : MetricsEvent
    {
        public MetricsAppTerminatedEvent(TimeSpan activityTime) 
            : base(CategoryApp,
                   ActionTerminated,
                   new MetricsParameter(ParameterActivityTime, activityTime.Seconds))
        {
        }
    }

    public class MetricsAppInstalledEvent : MetricsEvent
    {
        public MetricsAppInstalledEvent(string appSource, Version clrVersion) 
            : base(CategoryApp,
                   ActionInstalled,
                   new MetricsParameter(ParameterAppSource, appSource),
                   new MetricsParameter(ParameterCLRVersion, clrVersion))
        {
        }
    }
}
