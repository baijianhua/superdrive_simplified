using System;

namespace Connect2.Foundation.Security
{
    public interface ISecureCrypto : IDisposable
    {
        ISecureCryptoConfig Config { get; }
        
        byte[] Encrypt(byte[] plainBytes);
        byte[] Encrypt(byte[] plainBytes, uint offset, uint count);
        string Encrypt(string plainString);
        
        byte[] Decrypt(byte[] secureBytes, SecureHead secureHead);
        byte[] Decrypt(byte[] secureBytes, uint offset, uint count, SecureHead secureHead);
        string Decrypt(string secureString);
    }
}
