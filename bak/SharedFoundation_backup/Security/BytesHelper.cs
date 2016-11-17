using System;
using System.Text;

namespace Connect2.Foundation.Security
{
    public static class BytesHelper
    {
        #region public
        //class
        public static byte[] GetUTF8StringBytes(string aString)
        {
            if (string.IsNullOrEmpty(aString) == false)
            {
                return Encoding.UTF8.GetBytes(aString);
            }

            return null;
        }
        public static string GetUTF8String(byte[] bytes, uint beginIndex, uint count)
        {
            string aString = null;

            if (IsNullOrEmptyArray(bytes) == false
                && beginIndex + count <= bytes.Length)
            {
                aString = Encoding.UTF8.GetString(bytes, (int)beginIndex, (int)count);
            }

            return aString;
        }

        public static byte[] GetUInt32Bytes(uint aUInt)
        {
            return BitConverter.GetBytes(aUInt);
        }
        public static uint GetUInt32(byte[] bytes, uint beginIndex)
        {
            uint aUInt = 0;

            if (IsNullOrEmptyArray(bytes) == false
                && beginIndex + sizeof(uint) <= bytes.Length)
            {
                aUInt = BitConverter.ToUInt32(bytes, (int)beginIndex);
            }

            return aUInt;
        }

        public static bool IsNullOrEmptyArray(byte[] bytes)
        {
            return bytes == null || bytes.Length <= 0;
        }
        public static byte[] RemoveArray(byte[] bytes)
        {
            if (IsNullOrEmptyArray(bytes) == false)
            {
                Array.Clear(bytes, 0, bytes.Length);
            }

            return null;
        }
        public static bool AreEqualArrays(byte[] byteArray1, byte[] byteArray2)
        {
            bool isEqual = false;

            if (byteArray1 == null 
                && byteArray2 == null)
            {
                isEqual = true;
            }
            else if (byteArray1 != null
                && byteArray2 != null)
            {
                if (byteArray1.Length == byteArray2.Length)
                {
                    isEqual = true;

                    for (int index = 0; index < byteArray1.Length; ++index)
                    {
                        if (byteArray1[index] != byteArray2[index])
                        {
                            isEqual = false;
                            break;
                        }
                    }
                }
            }

            return isEqual;
        }
        #endregion
    }
}
