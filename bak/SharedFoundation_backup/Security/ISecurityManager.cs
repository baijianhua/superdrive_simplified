
using System;
using System.Collections.Generic;

namespace Connect2.Foundation.Security
{
    public interface ISecurityManager : IDisposable
    {
        ISecureCrypto CreateCrypto(SecurePassword password);
        string LoadString(string name);
        void SaveString(string name, string value);
        void DeleteString(string name);
    }
}
