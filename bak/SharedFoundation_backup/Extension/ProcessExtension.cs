using System;
using System.Diagnostics;
using System.IO;

namespace ConnectTo.Foundation.Extension
{
    public static class ProcessExtension
    {
        public static bool StartAsAdministrator(this Process process, string fileName, string argument = null)
        {
            if (string.IsNullOrEmpty(fileName))
                throw new ArgumentException("File name cannot be null or empty.");
            if (!File.Exists(fileName))
                throw new FileNotFoundException();

            process.StartInfo.Verb = "runas";
            process.StartInfo.FileName = fileName;

            if (argument != null)
            {
                process.StartInfo.Arguments = argument;
            }
            

            try
            {
                return process.Start();
            }
            catch
            {
                return false;
            }
        }
    }
}
