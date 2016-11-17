
using System;

namespace Connect2.Foundation.Security
{
    public class SecureHead
    {
        #region public
        //instance
        public SecureHead(
            string salt, 
            string initiaVector,
            uint saltIterations,
            uint saltedInitiaVectorBytesCount,
            uint saltedPasswordBytesCount,
            uint secureBytesCount)
        {
            if (string.IsNullOrEmpty(initiaVector) == true)
            {
                throw new ArgumentException("initiaVector should NOT be null or empty.");
            }
            if (string.IsNullOrEmpty(salt) == true)
            {
                throw new ArgumentException("salt should NOT be null or empty.");
            }
            if (saltIterations == 0)
            {
                throw new ArgumentException("saltIterations should NOT zero.");
            }
            if (saltedInitiaVectorBytesCount == 0)
            {
                throw new ArgumentException("saltedInitiaVectorBytesCount should NOT zero.");
            }
            if (saltedPasswordBytesCount == 0)
            {
                throw new ArgumentException("saltedPasswordBytesCount should NOT zero.");
            }
            
            saltBytes = BytesHelper.GetUTF8StringBytes(salt);
            initiaVectorBytes = BytesHelper.GetUTF8StringBytes(initiaVector);

            SaltBytesCount = (saltBytes != null) ? (uint)saltBytes.Length : 0;
            InitiaVectorBytesCount = (initiaVectorBytes != null) ? (uint)initiaVectorBytes.Length : 0;
            Salt = salt;
            InitiaVector = initiaVector;
            SaltIterations = saltIterations;
            SaltedInitialVectorBytesCount = saltedInitiaVectorBytesCount;
            SaltedPasswordBytesCount = saltedPasswordBytesCount;
            SecureBytesCount = secureBytesCount;

            TotalBytesCount = (uint)(GetPart1BytesCount() + GetPart2BytesCount() + SaltBytesCount + InitiaVectorBytesCount);
        }

        public string Salt
        {
            get;
            private set;
        }
        public string InitiaVector
        {
            get;
            private set;
        }
        public uint SaltIterations
        {
            get;
            private set;
        }
        public uint SaltedInitialVectorBytesCount
        {
            get;
            private set;
        }
        public uint SaltedPasswordBytesCount
        {
            get;
            private set;
        }
        public uint SecureBytesCount
        {
            get;
            private set;
        }

        public uint SaltBytesCount
        {
            get;
            private set;
        }
        public uint InitiaVectorBytesCount
        {
            get;
            private set;
        }
        public uint TotalBytesCount
        {
            get;
            private set;
        }

        public byte[] Encode()
        {
            byte[] bytes = new byte[TotalBytesCount];
            
            //part 1 (2 units):
            //SaltedBytesCount+InitiaVectorBytesCount
            uint beginIndex = 0;
            uint length = sizeof(uint);
            Array.Copy(BytesHelper.GetUInt32Bytes(SaltBytesCount), 0, bytes, beginIndex, length);
            beginIndex += length;
            length = sizeof(uint);
            Array.Copy(BytesHelper.GetUInt32Bytes(InitiaVectorBytesCount), 0, bytes, beginIndex, length);

            //part 2 (4 units):
            //+SaltIterations +SaltedInitialVectorBytesCount+SaltedPasswordBytesCount+SecureBytesCount
            beginIndex += length;
            length = sizeof(uint);
            Array.Copy(BytesHelper.GetUInt32Bytes(SaltIterations), 0, bytes, beginIndex, length);
            beginIndex += length;
            length = sizeof(uint);
            Array.Copy(BytesHelper.GetUInt32Bytes(SaltedInitialVectorBytesCount), 0, bytes, beginIndex, length);
            beginIndex += length;
            length = sizeof(uint);
            Array.Copy(BytesHelper.GetUInt32Bytes(SaltedPasswordBytesCount), 0, bytes, beginIndex, length);
            beginIndex += length;
            length = sizeof(uint);
            Array.Copy(BytesHelper.GetUInt32Bytes(SecureBytesCount), 0, bytes, beginIndex, length);

            //part 3 (2 byte arraies):
            //saltBytes+initiaVectorBytes
            beginIndex += length;
            length = SaltBytesCount;
            Array.Copy(saltBytes, 0, bytes, beginIndex, length);
            beginIndex += length;
            length = InitiaVectorBytesCount;
            Array.Copy(initiaVectorBytes, 0, bytes, beginIndex, length);

            return bytes;
        }

        public override string ToString()
        {
            return string.Format("{0}{1}{2}{3}{4}", 
                Salt, 
                InitiaVector, 
                SaltIterations, 
                SaltedPasswordBytesCount, 
                SaltedInitialVectorBytesCount);
        }

        //class
        public static void DecodeByteCounts(byte[] bytes, uint offset, out uint saltBytesCount, out uint initiaVectorBytesCount)
        {
            if (BytesHelper.IsNullOrEmptyArray(bytes) == true
                || bytes.Length - offset < 2 * sizeof(uint))
            {
                throw new ArgumentException(
                    string.Format("bytes array should have {0} bytes at least.",
                    2*sizeof(uint)));
            }

            saltBytesCount = BytesHelper.GetUInt32(bytes, offset);
            initiaVectorBytesCount = BytesHelper.GetUInt32(bytes, offset + sizeof(uint));
        }
        public static SecureHead Decode(byte[] bytes, uint offset, uint saltBytesCount, uint initiaVectorBytesCount)
        {
            uint totalBytesCount = GetPart2BytesCount() + saltBytesCount + initiaVectorBytesCount;
            if (BytesHelper.IsNullOrEmptyArray(bytes) == true
                || bytes.Length - offset < totalBytesCount)
            {
                throw new ArgumentException(
                    string.Format("bytes array should have {0} bytes at least.", 
                    totalBytesCount));
            }

            //part 2 (4 uints):
            uint beginIndex = offset;
            uint length = sizeof(uint);
            uint saltIterations = BytesHelper.GetUInt32(bytes, beginIndex);
            beginIndex += length;
            length = sizeof(uint);
            uint saltedInitiaVectorBytesCount = BytesHelper.GetUInt32(bytes, beginIndex);
            beginIndex += length;
            length = sizeof(uint);
            uint saltedPasswordBytesCount = BytesHelper.GetUInt32(bytes, beginIndex);
            beginIndex += length;
            length = sizeof(uint);
            uint secureDataBytes = BytesHelper.GetUInt32(bytes, beginIndex);

            //part 3 (2 byte arraries):
            beginIndex += length;
            length = saltBytesCount;
            string salt = BytesHelper.GetUTF8String(bytes, beginIndex, length);
            beginIndex += length;
            length = initiaVectorBytesCount;
            string initiaVector = BytesHelper.GetUTF8String(bytes, beginIndex, length);

            return new SecureHead(
                salt, 
                initiaVector, 
                saltIterations,
                saltedInitiaVectorBytesCount,
                saltedPasswordBytesCount,
                secureDataBytes);
        }
        public static uint SaltAndInitiaVectorSizeUnitBytesCount
        {
            get
            {
                return GetPart1BytesCount();
            }
        }
        public static uint FixedUnitFieldsBytesCount
        {
            get
            {
                return GetPart2BytesCount();
            }
        }
        #endregion

        #region private
        //instance
        private byte[] saltBytes = null;
        private byte[] initiaVectorBytes = null;

        //class
        private static uint GetPart1BytesCount()
        {
            return 2 * sizeof(uint);
        }
        private static uint GetPart2BytesCount()
        {
            return 4 * sizeof(uint);
        }
        #endregion
    }
}
