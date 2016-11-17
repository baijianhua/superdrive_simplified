using System;

namespace SuperDrive.Core.Support
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
