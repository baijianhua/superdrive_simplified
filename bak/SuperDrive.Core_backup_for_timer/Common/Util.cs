using System;
using System.Collections.Generic;
using ConnectTo.Foundation.Common;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace ConnectTo.Foundation.Helper
{
    
    public class Util
    {
        
        private static readonly double MIN_INTERVAL = 1d;
        public static readonly bool REPEAT = true;
        public static readonly bool STOP_REPEAT = false;

        

        /// <summary>
        /// Path.CombinePath会用当前系统的目录分隔符，传到对面就会出错，所以定义了这个方法。
        /// </summary>
        /// <param name="p1"></param>
        /// <param name="p2"></param>
        /// <returns></returns>
        public static string CombinePath(string p1, string p2)
        {
            if(string.IsNullOrEmpty(p1))
            {
                return p2;
            }
            else
            {
                if(string.IsNullOrEmpty(p2))
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
    }
}
