using System;
using System.Runtime.InteropServices;

namespace Metrics
{
    public static class MetricsWrapper
    {
        private const string MetricsDll = "reaper.dll";

        [DllImport(MetricsDll, EntryPoint = "disableTrack", CallingConvention = CallingConvention.Cdecl)]
        public static extern int DisableTrack();

        [DllImport(MetricsDll, EntryPoint = "enableTrack", CallingConvention = CallingConvention.Cdecl)]
        public static extern int EnableTrack();

        [DllImport(MetricsDll, EntryPoint = "finish", CallingConvention = CallingConvention.Cdecl)]
        public static extern int Finish();

        [DllImport(MetricsDll, EntryPoint = "initialize3", CallingConvention = CallingConvention.Cdecl)]
        public static extern int Initialize3(
            IntPtr hWnd,
            [MarshalAs(UnmanagedType.LPTStr)] string sLogDir,
            [MarshalAs(UnmanagedType.LPTStr)] string sCfgFilename,
            [MarshalAs(UnmanagedType.LPTStr)] string sCfgDir);
            
        [DllImport(MetricsDll, EntryPoint = "setUserId", CallingConvention = CallingConvention.Cdecl)]
        public static extern int SetUserId(
            [MarshalAs(UnmanagedType.LPTStr)] string sUserId,
            [MarshalAs(UnmanagedType.LPTStr)] string sUserIdClass);

        [DllImport(MetricsDll, EntryPoint = "setChannel", CallingConvention = CallingConvention.Cdecl)]
        public static extern void SetChannel([MarshalAs(UnmanagedType.LPTStr)] string channel);

        [DllImport(MetricsDll, EntryPoint = "setAppVersion", CallingConvention = CallingConvention.Cdecl)]
        public static extern void SetAppVersion([MarshalAs(UnmanagedType.LPTStr)] string version);

        [DllImport(MetricsDll, EntryPoint = "setVersionCode", CallingConvention = CallingConvention.Cdecl)]
        public static extern void SetVersionCode(uint versionCode);

        [DllImport(MetricsDll, EntryPoint = "trackEvent2", CallingConvention = CallingConvention.Cdecl)]
        public static extern void TrackEvent2(
            [MarshalAs(UnmanagedType.LPTStr)] string category,
            [MarshalAs(UnmanagedType.LPTStr)] string action,
            [MarshalAs(UnmanagedType.LPTStr)] string label,
            int value,
            int pairCount,
            [MarshalAs(UnmanagedType.LPArray)] MetricsParameter[] pairs);
    }
}
