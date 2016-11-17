using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Xml;
using System.Xml.Serialization;

using ConnectTo.Foundation.Extension;
using ConnectTo.Foundation.Helper;
using ConnectTo.Foundation.Update.Xml;

namespace ConnectTo.Foundation.Update
{
    public abstract class UpdateManagerBase
    {
        protected UpdateManagerBase(string subscriptionUrl) 
            : this(new Uri(subscriptionUrl))
        {
        }

        protected UpdateManagerBase(Uri subscriptionUrl)
        {
            if (subscriptionUrl == null)
                throw new ArgumentNullException(nameof(subscriptionUrl));

            SubscriptionUrl = subscriptionUrl;
            IsSubscriptionLocalAvailable = File.Exists(SubscriptionLocalPath);
        }

        public Uri SubscriptionUrl { get; }

        public string SubscriptionLocalPath
            => Path.Combine(Path.GetTempPath(), Path.GetFileName(SubscriptionUrl.ToString()));

        public bool IsSubscriptionLocalAvailable { get; }

        public Uri InstallerUrl { get; protected set; }

        public string InstallerLocalPath { get; protected set; }

        public bool IsInstallerDownloaded { get; private set; }

        public bool IsInstallerRunning
        {
            get
            {
                var result = false;

                var fileName = Path.GetFileNameWithoutExtension(InstallerLocalPath);
                if (IsInstallerDownloaded
                    && Process.GetProcessesByName(fileName).Length != 0)
                {
                    result = true;
                }

                return result;
            }
        }

        public bool IsUpdateAvailable(UpdateAppInfo appInfo, out Version latest)
        {
            if (appInfo == null) throw new ArgumentNullException(nameof(appInfo));

            var result = false;
            latest = null;
            
            var document = GetSubscriptionDocument();
            if (document.IsValid() && document.IsSignedByLenovo())
            {
                var targets = document.SelectNodes("//PublishTarget");

                if (targets != null)
                {
                    var deserializer = new XmlSerializer(typeof(PublishTarget));

                    foreach (XmlNode n in targets)
                    {
                        var reader = new XmlNodeReader(n);
                        var target = deserializer.Deserialize(reader) as PublishTarget;

                        if (target != null 
                            && target.LocalFilter.IsMatched(appInfo)
                            && new Version(target.WebPackage.Version) > appInfo.AppVersion)
                        {
                            latest = new Version(target.WebPackage.Version);

                            try
                            {
                                InstallerUrl = new Uri(target.WebPackage.Address);

                                InstallerLocalPath = Path.Combine(Path.GetTempPath(), Path.GetFileName(InstallerUrl.ToString()));

                                if (File.Exists(InstallerLocalPath))
                                {
                                    if (IsSubscriptionLocalAvailable)
                                    {
                                        if (target.WebPackage.MD5.Equals(
                                            FileHelper.GetMD5Hash(InstallerLocalPath), StringComparison.OrdinalIgnoreCase))
                                        {
                                            IsInstallerDownloaded = true;
                                        }
                                        else
                                        {
                                            FileHelper.SafeDelete(InstallerLocalPath);
                                        }
                                    }
                                    else
                                    {
                                        FileHelper.SafeDelete(InstallerLocalPath);
                                    }
                                }
                                result = true;
                                break;
                            }
                            catch
                            {
                                // ignored
                            }
                        }
                    }
                }
            }

            if (!result && File.Exists(SubscriptionLocalPath))
            {
                FileHelper.SafeDelete(SubscriptionLocalPath);
            }

            return result;
        }

        public virtual void DownloadInstaller() { }

        private XmlDocument GetSubscriptionDocument()
        {
            if (!File.Exists(SubscriptionLocalPath))
            {
                try
                {
                    var url = $"{SubscriptionUrl}?t={DateTime.UtcNow.Ticks}";
                    using (var client = new WebClient())
                    {
                        client.DownloadFile(url, SubscriptionLocalPath);
                    }
                }
                catch
                {
                    return null;
                }
            }

            var document = new XmlDocument { PreserveWhitespace = true };

            try
            {
                document.Load(SubscriptionLocalPath);

                return document;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
