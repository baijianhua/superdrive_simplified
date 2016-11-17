using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ConnectTo.Foundation.Helper
{
    public class StringHelper
    {
        public static string ByteArrayToHexString(byte[] bytes, string delimiter = " ")
        {
            return bytes == null ? "null" : BitConverter.ToString(bytes).Replace("-", delimiter);
        }

        public static string NewRandomGUID()
        {
            return Guid.NewGuid().ToString("N");
        }

        public static string NewRandomPassword()
        {
            return NewRandomGUID();//.Substring(0, 16);
        }
    }
}
