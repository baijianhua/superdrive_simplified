using System;
using System.Collections.Generic;
using ConnectTo.Foundation.Business;

namespace Connect2.Foundation.Security
{
    public class SecurityManager : ISecurityManager
    {
        #region public
        public SecurityManager(ISecureStorage storage)
        {
            if (storage == null)
            {
                throw new ArgumentNullException("storage");
            }

            this.storage = storage;
        }
        ~SecurityManager()
        {
            Dispose(false);
        }
        public void Dispose()
        {
            Dispose(true);

            GC.SuppressFinalize(this);
        }
        
        public ISecureCrypto CreateCrypto(SecurePassword password)
        {
            ISecureCrypto crypto = new SecureCrypto();

            crypto.Config.Password = password;

            return crypto;
        }

        SecurePassword LoadPassword(string targetName)
        {
            return storage.LoadPassword(targetName);
        }
        void SavePassword(string targetName, SecurePassword password)
        {
            storage.SavePassword(targetName, password);
        }        
        void DeletePassword(string targetName)
        {
            storage.DeletePassword(targetName);
        }

        public string LoadString(string name)
        {
            string plainString = null;
            
            try
            {
                using (var password = LoadPassword(name))
                {
                    if (password != null)
                    {
                        plainString = password.GetString();
                    }
                }  
            }
            catch
            {
                plainString = null;
                //ignore any exceptions according to requirements
            }

            return plainString;
        }
        public void SaveString(string name, string value)
        {
            try
            {
                SecurePassword password = new SecurePassword(value);

                SavePassword(name, password);

                password.Dispose();
                password = null;
            }
            catch
            {
                //ignore any exceptions according to requirements
            }
        }
        public void DeleteString(string name)
        {
            try
            {
                DeletePassword(name);
            }
            catch
            {
                //ignore any exceptions according to requirements
            }
        }
        #endregion

        #region protected
        protected virtual void Dispose(bool isDisposing)
        {
            if (isDisposed == false)
            {
                if (isDisposing == true)
                {
                    //do nothing now cause no SINGLE password hold by security manager anymore based on update secure design.
                }

                isDisposed = true;
            }
        }

        


        #endregion

        #region private
        private bool isDisposed = false;
        private ISecureStorage storage = null;

        internal const string REMOTE_CONNECT_CODE = "REMOTE_CONNECT_CODE";
        internal const string LOCAL_CONNECT_CODE = "LOCAL_CONNECT_CODE";
        internal const string SSID_PASSWORD = "SSID_PASSWORD";
        #endregion
    }
}
