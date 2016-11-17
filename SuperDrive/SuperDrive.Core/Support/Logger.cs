using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace SuperDrive.Core.Support
{
    public enum LogLevel
    {
        Info,
        Trace,
        Debug,
        Warn,
        Error,
    }

    internal class LogItem
    {
        internal LogLevel LogLevel { get; set; }
        internal int ThreadId { get; set; }
        internal DateTime Time { get; set; } = DateTime.Now;
        internal string Message { get; set; }
        internal string StackTrace { get; set; }
        internal string Tag { get; set; } = "Tag";
    }
    public class Logger:IDisposable
    {
        private FileStream logStream;
        private StreamWriter sw;

        public Logger()
        {
            var date = string.Format("{0:yyyy-MM-dd}", DateTime.Now);
            var name = $"{date}.log";
            var path = Path.Combine(Env.FileSystem.DefaultDir, name);
            logStream = new FileStream(path,FileMode.Append,FileAccess.Write);
            sw = new StreamWriter(logStream, Encoding.UTF8);
        }
        void Log(LogItem li)
        {
            var threadId = li.ThreadId == default(int) ? "" : $"TID={li.ThreadId}";
            var stackTrace = string.IsNullOrEmpty(li.StackTrace) ? "" : $"\nStackTrace={li.StackTrace}";
            var s = $"{li.LogLevel} {li.Time} {li.Tag} | {threadId} {li.Message} {stackTrace}";
            //if (li.Tag == "Env.TaskPool" || li.Tag == "Http")
            //{
            //    System.Diagnostics.Debug.WriteLine(s);
            //}
            System.Diagnostics.Debug.WriteLine(s);

            //TODO 如果这里发生异常，那很奇怪，需要检查一下，为什么会发生异常。因为正常写日志的操作应该是在Log关闭之前发生的。
            sw?.WriteLine(s);
            sw?.Flush();
        }

        public void Log(string message, [CallerMemberName] string tag = "", string stackTrace = "",LogLevel level=LogLevel.Info)
        {
            Log(new LogItem {Message = message,Tag=tag,StackTrace = stackTrace,LogLevel = level});
        }


        public void Dispose()
        {
            sw?.Flush();
            sw?.Dispose();
            logStream?.Dispose();
            sw = null;
            logStream = null;
            GC.SuppressFinalize(this);
        }

        internal void Flush()
        {
            sw?.Flush();
        }
    }
}