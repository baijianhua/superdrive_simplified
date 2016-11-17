using System;

namespace ConnectTo.Foundation.Update
{
    public class UpdateAppInfo
    {
        public UpdateAppInfo(Version appVersion, Version osVersion, string appChannel, string osLanguage, string osCountry)
        {
            if (appVersion == null) throw new ArgumentNullException(nameof(appVersion));
            if (osVersion == null) throw new ArgumentNullException(nameof(osVersion));
            if (string.IsNullOrEmpty(appChannel)) throw new ArgumentException(nameof(appChannel));
            if (string.IsNullOrEmpty(osLanguage)) throw new ArgumentException(nameof(osLanguage));
            if (string.IsNullOrEmpty(osCountry)) throw new ArgumentException(nameof(osCountry));

            AppVersion = appVersion;
            OSVersion = osVersion;
            AppChannel = appChannel;
            OSLanguage = osLanguage;
            OSCountry = osCountry;
        }

        public Version AppVersion { get; }

        public Version OSVersion { get; }

        public string AppChannel { get; }

        public string OSLanguage { get; }

        public string OSCountry { get; }
    }
}
