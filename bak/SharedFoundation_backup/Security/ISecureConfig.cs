using System;

namespace Connect2.Foundation.Security
{
    public interface ISecureCryptoConfig : IDisposable
    {
        bool IsValid { get; }

        string Salt { get; set; }
        string InitiaVector { get; set; }
        SecurePassword Password { get; set; }
        uint SaltIterations { get; set; }
        uint SaltedInitialVectorBytesCount { get; set; }
        uint SaltedPasswordBytesCount { get; set; }
        uint MinimumPasswordLength { get; set; }

        event EventHandler Updated;
    }
}
