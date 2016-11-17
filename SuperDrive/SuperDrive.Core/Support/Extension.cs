using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SuperDrive.Core.Support
{
    public static class Extentions
    {
        public static void ReStart(this Timer t)
        {

        }
        public static void Stop(this Timer t)
        {

        }
        public static void SetValueWhenTimeout<T>(this TaskCompletionSource<T> tcs, TimeSpan timeSpan, T defaultValue) 
        {
            Task.Run(() =>
            {
                var timeout = Task.Delay(timeSpan);
                var result = Task.WhenAny(tcs.Task, timeout);
                if (result == timeout) tcs.TrySetResult(defaultValue);
            });
        }

        public static bool IsEnded<T>(this TaskCompletionSource<T> tcs)
        {
            return tcs.Task.IsCompleted || tcs.Task.IsCanceled || tcs.Task.IsFaulted;
        }
        public static void AddIfNotContains<T>(this HashSet<T> set, T val) where T : class
        {
            if (!set.Contains(val)) set.Add(val);
        }
        public static TVal GetByKey<TKey, TVal>(this IDictionary<TKey, TVal> dic, TKey key) where TVal : class
        {
            if (key == null) return null;

            return dic.ContainsKey(key) ? dic[key] : null;
        }

        
    }
}
