using System;
using System.IO;

using Metrics.Event;

namespace Metrics
{
    public interface IMetricsManager : IDisposable
    {
        bool IsEnabled { get; set; }

        string Channel { set; }

        string AppVersion { set; }

        int VersionCode { set; }

        void RaiseEvent(MetricsEvent MetricsEvent);
    }
}
