using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SuperDrive.Core.Enitity;

namespace SuperDrive.Core.Support
{
    public class Util
    {
        public static void CheckParam(bool v)
        {
            if (!v)
            {
                throw new ArgumentException("Check parameter failed");
            }
        }

        public static byte[] StreamToBytes(Stream stream)
        {
            byte[] bytes = new byte[stream.Length];
            stream.Read(bytes, 0, bytes.Length);
            stream.Seek(0, SeekOrigin.Begin);
            return bytes;
        }

        public static Stream BytesToStream(byte[] bytes)=> new MemoryStream(bytes);

        //这个函数的作用是当instance不等于null的时候，执行一个方法，如果instance==null,抛出异常。 好像没什么用啊？！还不如直接调用呢。不是一样的效果？只是
        //异常是运行期异常。也不对，都是运行期异常啊。
        internal static void IntstanceRun<T>(T instance, Action<T> method)
        {
            CheckParam(instance != null);
            method(instance);
        }
        //如果成功设置了TaskCompletionSource的值，不用管理这个Timeout，因为等检查的时候，TrySetResult会返回false.
        //internal static void SetValueWhenTimeout<T>(TaskCompletionSource<T> tcs, TimeSpan timeSpan,T defaultValue = null) where T:class
        //{
        //    Task.Run(() =>
        //    {
        //        var timeout = Task.Delay(timeSpan);
        //        var result = Task.WhenAny(tcs.Task, timeout);
        //        if (result == timeout) tcs.TrySetResult(defaultValue);
        //    });
        //}


        //internal static string ToMd5Base64(string s)
        //{
        //    using (var md5 = MD5.Create())
        //    {
        //        var md5Bytes = md5.ComputeHash(Encoding.UTF8.GetBytes(s));
        //        var base64 = Convert.ToBase64String(md5Bytes);
        //        return base64;
        //    }
        //}

        /// <summary>
        /// func, 如果这个函数返回true,就不再执行。
        /// </summary>
        /// <param name="func"></param>
        /// <param name="timeSpan"></param>
        /// <param name="interval"></param>
        public static void WaitAnySync(Func<bool> func, TimeSpan timeSpan, int interval)
        {
            SpinWait sw = new SpinWait();
            var start = Environment.TickCount;
            var eplipsed = 0;
            var time = timeSpan.TotalMilliseconds;
            while (eplipsed < time)
            {
                sw.SpinOnce();
                eplipsed = Environment.TickCount - start;
                if (eplipsed % interval == 0)
                {
                    //如果任何一个的状态都不是Connected，就可以退出了。
                    if (func()) break;
                }
            }
        }

        public static void ArgumentNullException( bool expressions )
        {
            if (!expressions) throw new ArgumentNullException();
        }

        public static void ArgumentException( bool expressions)
        {
            if (!expressions) throw new ArgumentException();
        }

        public static void Check( bool expressions, string desc = "exception")
        {
            if (!expressions)
            {
                Exception ex = new Exception(desc);
                throw ex;
            }
        }



        public static void Check<T>(bool expressions, string desc = "exception") where T:Exception,new()
        {
            if (!expressions)
            {
                T ex = new T();
                throw ex;
            }
        }

        public static string AddressToString(IPAddress address)
        {
            return address.ToString();
        }

        public static Timer DoLater(Action action, double delayTimeMilliSeconds)
        {
            Support.Util.Check(action != null, "action can not be null");
            Support.Util.Check(delayTimeMilliSeconds > 0, "delayTimeMilliSeconds should > 0");

            Timer retryTimer = new Timer(_=>action(),null,(int)delayTimeMilliSeconds,0);
            return retryTimer;
        }

        private static readonly double MIN_INTERVAL = 1d;
        public static readonly bool REPEAT = true;
        public static readonly bool STOP_REPEAT = false;

        public static Task EmptyTask=> Task.Delay(1);
            

        /// <summary>
        /// Path.CombinePath会用当前系统的目录分隔符，传到对面就会出错，所以定义了这个方法。
        /// </summary>
        /// <param name="p1"></param>
        /// <param name="p2"></param>
        /// <returns></returns>
        public static string CombinePath(string p1, string p2)
        {
            if(String.IsNullOrEmpty(p1))
            {
                return p2;
            }
            else
            {
                if(String.IsNullOrEmpty(p2))
                {
                    return p1;
                }
                else
                {
                    return p1 + "/" + p2;
                }
            }
        }

        public static string CombinePath(string p1, string p2, string p3)
        {
            return CombinePath(CombinePath(p1, p2), p3);
        }

        public static string ToBase64(string s) => Convert.ToBase64String(Encoding.UTF8.GetBytes(s));
        public static string FromBase64(string itemId) => Encoding.UTF8.GetString(Convert.FromBase64String(itemId));
        
    }
}
