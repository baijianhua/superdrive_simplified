
namespace Connect2.Foundation.Security
{
    public interface ISecureStorage
    {
        SecurePassword LoadPassword(string targetName);
        bool SavePassword(string targetName, SecurePassword password);
        bool DeletePassword(string targetName);
    }
}
