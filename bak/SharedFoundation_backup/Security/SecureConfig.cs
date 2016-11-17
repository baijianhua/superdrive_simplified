using System;

namespace Connect2.Foundation.Security
{
    public class SecureCryptoConfig : ISecureCryptoConfig
    {
        #region public
        public SecureCryptoConfig()
        {
        }
        ~SecureCryptoConfig()
        {
            Dispose(false);
        }
        public void Dispose()
        {
            Dispose(true);

            GC.SuppressFinalize(this);
        }

        public bool IsValid
        {
            get
            {
                return String.IsNullOrEmpty(Salt) == false
                    && String.IsNullOrEmpty(InitiaVector) == false
                    && Password != null
                    && Password.Length >= MinimumPasswordLength;
            }
        }
        public uint SaltIterations
        {
            get { return saltIterations; }
            set
            {
                if (value != saltIterations)
                {
                    saltIterations = value;
                    NotifyRegistersWhenUpdated();
                }
            }
        }
        public string Salt
        {
            get { return salt; }
            set
            {
                if (value != salt)
                {
                    salt = value;
                    NotifyRegistersWhenUpdated();
                }
            }
        }
        public SecurePassword Password
        {
            get { return password; }
            set
            {
                if (value != password)
                {
                    if (password != null)
                    {
                        password.Dispose();
                        password = null;
                    }
                    password = value;
                    NotifyRegistersWhenUpdated();
                }
            }
        }
        public string InitiaVector
        {
            get { return initiaVector; }
            set
            {
                if (value != initiaVector)
                {
                    initiaVector = value;
                    NotifyRegistersWhenUpdated();
                }
            }
        }
        public uint SaltedInitialVectorBytesCount
        {
            get { return saltedInitiaVectorBytesCount; }
            set
            {
                if (value != saltedInitiaVectorBytesCount)
                {
                    saltedInitiaVectorBytesCount = value;
                    NotifyRegistersWhenUpdated();
                }
            }
        }
        public uint MinimumPasswordLength
        {
            get;
            set;
        }
        public uint SaltedPasswordBytesCount
        {
            get { return saltedPasswordBytesCount; }
            set
            {
                if (value != saltedPasswordBytesCount)
                {
                    saltedPasswordBytesCount = value;
                    NotifyRegistersWhenUpdated();
                }
            }
        }

        public event EventHandler Updated;
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
        private void Cleanup()
        {
            if (password != null)
            {
                password.Dispose();
                password = null;
            }
        }
        private void NotifyRegistersWhenUpdated()
        {
            Updated?.Invoke(this, null);
        }

        private uint saltIterations = 0;
        private uint saltedInitiaVectorBytesCount = 0;
        private uint saltedPasswordBytesCount = 0;
        private string salt = null;
        private string initiaVector = null;
        private SecurePassword password = null;
        private bool isDisposed = false;
        #endregion
    }
}
