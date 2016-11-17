using System;

namespace Metrics.Event
{
    public class MetricsUpdateEvent : MetricsEvent
    {
        public MetricsUpdateEvent(string oldVersion, string newVersion) 
            : base(CategoryUpdate,
                   ActionUpdated,
                   new MetricsParameter(ParameterVersions, FormatVersionString(oldVersion, newVersion)))
        {
        }

        private static string FormatVersionString(string oldVersion, string newVersion)
        {
            return $"{oldVersion} -> {newVersion}";
        }
    }
}
