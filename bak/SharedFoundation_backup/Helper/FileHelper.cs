using System;
using System.IO;
using System.Security.Cryptography;

namespace ConnectTo.Foundation.Helper
{
    public static class FileHelper
    {
        public static string GetMD5Hash(string filePath)
        {
            if (File.Exists(filePath))
            {
                var hash = MD5.Create().ComputeHash(File.ReadAllBytes(filePath));
                return BitConverter.ToString(hash).Replace("-", string.Empty);
            }
            return null;
        }

        public static void SafeDelete(string filePath)
        {
            if (File.Exists(filePath))
            {
                try
                {
                    File.Delete(filePath);
                }
                catch
                {
                    // ignored
                }
            }
        }
    }
}
