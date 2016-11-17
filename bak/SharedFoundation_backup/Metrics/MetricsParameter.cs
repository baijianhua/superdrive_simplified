using System;
using System.Runtime.InteropServices;

namespace Metrics
{
    [StructLayout(LayoutKind.Sequential)]
    public struct MetricsParameter
    {
        public MetricsParameter(string key, object obj)
        {
            if (string.IsNullOrEmpty(key)) throw new ArgumentException();
            if (obj == null) throw new ArgumentNullException(nameof(obj));

            Key = key;
            Value = obj.ToString();
        }

        public MetricsParameter(string key, string value)
        {
            if (string.IsNullOrEmpty(key)) throw new ArgumentException();
            if (string.IsNullOrEmpty(value)) throw new ArgumentException();

            Key = key;
            Value = value;
        }

        [MarshalAs(UnmanagedType.LPTStr)]
        public string Key;
        [MarshalAs(UnmanagedType.LPTStr)]
        public string Value;
    }
}
