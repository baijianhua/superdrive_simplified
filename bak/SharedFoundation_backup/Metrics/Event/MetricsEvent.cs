using System;

namespace Metrics.Event
{
    /// <summary>
    /// Don't use this class directly, instead derive from this class and encapusulate all the field in it
    /// </summary>
    public class MetricsEvent
    {
        public const string CategoryApp = "app";
        public const string ActionLaunched = "launched";
        public const string ActionTerminated = "terminated";
        public const string ActionInstalled = "installed";
        public const string ActionUninstalled = "uninstalled";
        public const string ParameterSecureModeEnabled = "secureMode";
        public const string ParameterActivityTime = "activityTime";
        public const string ParameterAppSource = "appSource";
        public const string ParameterUpdateSource = "updateSource";
        public const string ParameterCLRVersion = "clrVersion";

        public const string CategoryUpdate = "update";
        public const string ActionUpdated = "updated";
        public const string ParameterVersions = "versions";
        public const string ParameterResult = "result";

        public MetricsEvent(string category, string action, MetricsParameter parameter1)
            : this(category, action, string.Empty, parameter1)
        {

        }

        public MetricsEvent(string category, string action, MetricsParameter parameter1, MetricsParameter parameter2)
            : this(category, action, string.Empty, parameter1, parameter2)
        {

        }

        public MetricsEvent(string category, string action, params MetricsParameter[] parameters)
            : this(category, action, string.Empty, parameters)
        {

        }

        public MetricsEvent(string category, string action, string label)
            : this(category, action, label, null)
        {
            
        }

        public MetricsEvent(string category, string action, string label, MetricsParameter parameter)
        {
            if (string.IsNullOrEmpty(category)) throw new ArgumentException();
            if (string.IsNullOrEmpty(action)) throw new ArgumentException();
            if (label == null) throw new ArgumentNullException();

            Category = category;
            Action = action;
            Label = label;
            Parameters = new[] { parameter };
        }

        public MetricsEvent(
            string category, string action, string label, MetricsParameter parameter1, MetricsParameter parameter2)
        {
            if (string.IsNullOrEmpty(category)) throw new ArgumentException();
            if (string.IsNullOrEmpty(action)) throw new ArgumentException();
            if (string.IsNullOrEmpty(label)) throw new ArgumentException();

            Category = category;
            Action = action;
            Label = label;
            Parameters = new[] { parameter1, parameter2 };
        }

        public MetricsEvent(string category, string action, string label, params MetricsParameter[] parameters)
        {
            if (string.IsNullOrEmpty(category)) throw new ArgumentException();
            if (string.IsNullOrEmpty(action)) throw new ArgumentException();
            if (string.IsNullOrEmpty(label)) throw new ArgumentException();

            Category = category;
            Action = action;
            Label = label;
            Parameters = parameters;
        }

        public string Category { get; }

        public string Action { get; }

        public string Label { get; }

        public MetricsParameter[] Parameters { get; }
    }
}