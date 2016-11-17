
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Connect2.Foundation.Security;
using ConnectTo.Foundation.Business;

namespace Connect2.Foundation.Security
{
    public class SecureCrypto : ISecureCrypto
    {
        #region public
        public SecureCrypto()
        {
            Config = new SecureCryptoConfig();

            Config.SaltIterations = defaultSaltIterations;
            Config.Salt = randomSalt;
            Config.InitiaVector = randomInitiaVector;
            Config.SaltedInitialVectorBytesCount = defaultSaltedIvBytesCount;
            Config.MinimumPasswordLength = defaultMinimumPasswordLength;
            Config.SaltedPasswordBytesCount = defaultSaltedPasswordBytesCount;

            Config.Updated += OnConfigUpdated;
        }
        ~SecureCrypto()
        {
            Dispose(false);
        }
        public void Dispose()
        {
            Dispose(true);

            GC.SuppressFinalize(this);
        }

        public ISecureCryptoConfig Config
        {
            get;
            private set;
        }
        
        public byte[] Encrypt(byte[] plainBytes)
        {
            return plainBytes == null ? null : Encrypt(plainBytes, 0, (uint)plainBytes.Length);
        }
        public byte[] Encrypt(byte[] plainBytes, uint offset, uint count)
        {
            byte[] bytes = null;
            try
            {
                byte[] secureBytes = EncryptPlainBytes(plainBytes, offset, count);
                SecureHead secureHead = new SecureHead(
                            Config.Salt,
                            Config.InitiaVector,
                            Config.SaltIterations,
                            Config.SaltedInitialVectorBytesCount,
                            Config.SaltedPasswordBytesCount,
                            (uint)secureBytes.Length);
                byte[] secureHeadBytes = secureHead.Encode();

                bytes = new byte[secureHeadBytes.Length + secureBytes.Length];
                Array.Copy(secureHeadBytes, 0, bytes, 0, secureHeadBytes.Length);
                Array.Copy(secureBytes, 0, bytes, secureHeadBytes.Length, secureBytes.Length);
            }
            catch (Exception e)
            {
                //ignore all exceptions according to requirements
#if DEBUG
                Env.Instance.ShowMessage($"encrypt exception desc={e.Message}  stack trace={e.StackTrace}");
#endif
            }


            return bytes;
        }
        public string Encrypt(string plainString)
        {
            var plainBytes = BytesHelper.GetUTF8StringBytes(plainString);
            var secureHeadAndBytes = Encrypt(plainBytes);

            return secureHeadAndBytes != null ? Convert.ToBase64String(secureHeadAndBytes) : null;
        }
        
        public byte[] Decrypt(byte[] secureBytes, SecureHead secureHead)
        {
            return Decrypt(secureBytes, 0, (uint)secureBytes.Length, secureHead);
        }
        public byte[] Decrypt(byte[] secureBytes, uint offset, uint count, SecureHead secureHead)
        {
            string error = null;
            byte[] bytes = null;

            if (isDisposed == true)
            {
                error = "security manager has been disposed.";
            }
            if (BytesHelper.IsNullOrEmptyArray(secureBytes) == true)
            {
                error = "secureBytes array should NOT be null or empty.";
            }
            if (Config.IsValid == false)
            {
                error = "Config is not valid.";
            }
            if (secureHead == null)
            {
                error = "secureHead should NOT be null.";
            }

            if(error == null)
            {
                try
                {
                    SaltedBytesCache decryptionCache = GetDecryptionSaltedBytesCache(secureHead);
                    bytes = AesDecrypt(
                        secureBytes,
                        offset,
                        count,
                        decryptionCache.SaltedPasswordBytes,
                        decryptionCache.SaltedInitiaVectorBytes);
                }
                catch (Exception e)
                {
#if DEBUG
                    Env.Instance.ShowMessage($"decrypt exception desc={e.Message}  stack trace={e.StackTrace}");
#endif
                }
            }else
            {
#if DEBUG
                Env.Instance.ShowMessage(error);
#endif
            }
            
            

            return bytes;
        }
        public string Decrypt(string secureString)
        {
            string result = null;
            try
            {
                byte[] secureBytes = Convert.FromBase64String(secureString);

                uint saltBytesCount = 0;
                uint initiaVectorBytesCount = 0;
                SecureHead.DecodeByteCounts(secureBytes, 0, out saltBytesCount, out initiaVectorBytesCount);
                SecureHead secureHead = SecureHead.Decode(
                    secureBytes,
                    2 * sizeof(uint),
                    saltBytesCount,
                    initiaVectorBytesCount);

                byte[] plainBytes = Decrypt(secureBytes, secureHead.TotalBytesCount, secureHead.SecureBytesCount, secureHead);

                if (plainBytes != null)
                {
                    result = BytesHelper.GetUTF8String(plainBytes, 0, (uint)plainBytes.Length);
                }
            }
            catch (Exception e)
            {
                //ignore all exceptions according to requirements
            }
            return result;
        }
        #endregion

        #region protected
        protected virtual void Dispose(bool isDisposing)
        {
            if (isDisposed == false)
            {
                if (isDisposing == true)
                {
                    Cleanup();
                }

                isDisposed = true; 
            }
        }
        #endregion

        #region private
        //instance
        private void Cleanup()
        {
            CleanupSaltedBytesCache(encryptionCache);
            if (decryptionCaches != null)
            {
                foreach (var saltedBytesCache in decryptionCaches.Values)
                {
                    CleanupSaltedBytesCache(saltedBytesCache);
                }
            }

            Config.Updated -= OnConfigUpdated;
            Config.Dispose();
            Config = null;
        }
        private void CleanupSaltedBytesCache(SaltedBytesCache saltedBytesCache)
        {
            if (saltedBytesCache != null)
            {
                BytesHelper.RemoveArray(saltedBytesCache.SaltedPasswordBytes);
                BytesHelper.RemoveArray(saltedBytesCache.SaltedInitiaVectorBytes);
            }
        }
        private void OnConfigUpdated(object sender, EventArgs e)
        {
            CleanupSaltedBytesCache(encryptionCache);
        }
        private SaltedBytesCache GetDecryptionSaltedBytesCache(SecureHead secureHead)
        {
            if (decryptionCaches == null)
            {
                decryptionCaches = new Dictionary<string, SaltedBytesCache>();
            }

            string cacheKey = secureHead.ToString();
            if (decryptionCaches.ContainsKey(cacheKey) == false)
            {
                decryptionCaches.Add(
                    cacheKey,
                    new SaltedBytesCache(
                        secureHead,
                        Config.Password));
            }

            return decryptionCaches[cacheKey];
        }
        private byte[] EncryptPlainBytes(byte[] plainBytes, uint offset, uint count)
        {
            if (isDisposed == true)
            {
                throw new ArgumentException("security manager has been disposed.");
            }

            if (BytesHelper.IsNullOrEmptyArray(plainBytes) == true)
            {
                throw new ArgumentException("plainBytes array should NOT be null or empty.");
            }

            if (Config.IsValid == false)
            {
                throw new ArgumentException("Config is not valid.");
            }

            if (encryptionCache == null)
            {
                encryptionCache = new SaltedBytesCache(
                    new SecureHead(
                        Config.Salt,
                        Config.InitiaVector,
                        Config.SaltIterations,
                        Config.SaltedInitialVectorBytesCount,
                        Config.SaltedPasswordBytesCount,
                        0),
                    Config.Password);
            }
            if (encryptionCache == null)
            {
                throw new ArgumentException("encryptionCache array should NOT be null.");
            }

            return AesEncrypt(
                plainBytes,
                offset,
                count,
                encryptionCache.SaltedPasswordBytes,
                encryptionCache.SaltedInitiaVectorBytes);
        }

        private SaltedBytesCache encryptionCache = null;
        private IDictionary<string, SaltedBytesCache> decryptionCaches = null;
        private bool isDisposed = false;

        //class
        private static byte[] AesEncrypt(byte[] plainBytes, uint offset, uint count, byte[] saltedPassword, byte[] saltedIV)
        {
            byte[] encryptedBytes = null;

            using (var provider = new AesCryptoServiceProvider())
            {
                using (var memory = new MemoryStream())
                {
                    using (var encryptor = provider.CreateEncryptor(saltedPassword, saltedIV))
                    {
                        using (var writer = new CryptoStream(memory, encryptor, CryptoStreamMode.Write))
                        {
                            writer.Write(plainBytes, (int)offset, (int)count);
                            writer.FlushFinalBlock();
                        }
                        encryptedBytes = memory.ToArray();
                    }
                }
            }

            return encryptedBytes;
        }
        private static byte[] AesDecrypt(byte[] secureBytes, uint offset, uint count, byte[] saltedPassword, byte[] saltedIV)
        {
            byte[] decryptedBytes = null;

            using (var provider = new AesCryptoServiceProvider())
            {
                using (var memory = new MemoryStream())
                {
                    using (var decryptor = provider.CreateDecryptor(saltedPassword, saltedIV))
                    {
                        using (var writer = new CryptoStream(memory, decryptor, CryptoStreamMode.Write))
                        {
                            writer.Write(secureBytes, (int)offset, (int)count);
                            writer.FlushFinalBlock();
                        }
                        decryptedBytes = memory.ToArray();
                    }
                }
            }

            return decryptedBytes;
        }

        private static string randomSalt = Guid.NewGuid().ToString();
        private static string randomInitiaVector = Guid.NewGuid().ToString();
        private static uint defaultSaltIterations = 10000;
        private static uint defaultMinimumPasswordLength = 8;
        private static uint defaultSaltedIvBytesCount = 16;
        private static uint defaultSaltedPasswordBytesCount = 32;
        #endregion
    }

    internal class SaltedBytesCache
    {
        #region public
        public SaltedBytesCache(
            SecureHead secureHead,
            SecurePassword securePassword)
        {
            if (secureHead == null)
            {
                throw new ArgumentException("secureHead should NOT be null.");
            }
            if (securePassword == null)
            {
                throw new ArgumentException("securePassword should NOT be null.");
            }
            byte[] saltedPasswordBytes = GetSaltedPassowrdBytes(secureHead, securePassword);
            if (BytesHelper.IsNullOrEmptyArray(saltedPasswordBytes) == true)
            {
                throw new ArgumentException("saltedPasswordBytes should NOT be null.");
            }
            byte[] saltedInitialVectorBytes = GetSaltedInitialVectorBytes(secureHead);
            if (BytesHelper.IsNullOrEmptyArray(saltedInitialVectorBytes) == true)
            {
                throw new ArgumentException("saltedInitialVectorBytes should NOT be null.");
            }

            SecureHead = secureHead;
            SaltedPasswordBytes = saltedPasswordBytes;
            SaltedInitiaVectorBytes = saltedInitialVectorBytes;
        }

        public SecureHead SecureHead
        {
            get;
            private set;
        }        
        public byte[] SaltedPasswordBytes
        {
            get;
            private set;
        }
        public byte[] SaltedInitiaVectorBytes
        {
            get;
            private set;
        }

        public override string ToString()
        {
            return SecureHead.ToString();
        }
        #endregion

        #region private
        //instance
        private byte[] GetSaltedPassowrdBytes(SecureHead secureHead, SecurePassword securePassword)
        {
            string plainPassword = securePassword.GetString();
            byte[] saltedPasswordBytes = SaltString(
                plainPassword,
                secureHead.Salt,
                secureHead.SaltIterations,
                secureHead.SaltedPasswordBytesCount);
            plainPassword = SecurePassword.DestroyString(plainPassword);

            return saltedPasswordBytes;
        }
        private byte[] GetSaltedInitialVectorBytes(SecureHead secureHead)
        {
            return SaltString(
                secureHead.InitiaVector,
                secureHead.Salt,
                secureHead.SaltIterations,
                secureHead.SaltedInitialVectorBytesCount);
        }

        //class
        private static byte[] SaltString(string data, string salt, uint saltIterations, uint bytesCount)
        {
            byte[] saltedBytes = null;
            Rfc2898DeriveBytes deriveBytes = null;
            try
            {
                deriveBytes = new Rfc2898DeriveBytes(data, BytesHelper.GetUTF8StringBytes(salt), (int)saltIterations);
                saltedBytes = deriveBytes.GetBytes((int)bytesCount);
            }
            catch (Exception)
            {
                saltedBytes = null;
            }
            finally
            {
                if (deriveBytes is IDisposable
                    && deriveBytes != null)
                {
                    ((IDisposable)deriveBytes).Dispose();
                    deriveBytes = null;
                }
            }

            return saltedBytes;
        }
        #endregion
    }
}
