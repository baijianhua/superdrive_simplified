using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ConnectTo.Foundation.Common
{
    public static class Preconditions
    {
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
    }
}
