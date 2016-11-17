using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;

namespace ConnectTo.Foundation.Update.Xml
{
    [XmlRoot("LocalFilter")]
    public class Filter
    {
        [XmlArray(ElementName = @"AppVersionScopes", IsNullable = false)]
        [XmlArrayItem(ElementName = @"VersionScope", IsNullable = false)]
        public List<VersionScope> AppVersionScopes { get; set; }

        [XmlArray(ElementName = @"OSVersionScopes", IsNullable = false)]
        [XmlArrayItem(ElementName = @"VersionScope", IsNullable = false)]
        public List<VersionScope> OSVersionScopes { get; set; }

        [XmlArray(ElementName = @"AppChannelScope", IsNullable = false)]
        [XmlArrayItem(ElementName = @"Channel", IsNullable = false)]
        public List<string> AppChannelScope { get; set; }

        [XmlArray(ElementName = @"OSLanguageScope", IsNullable = false)]
        [XmlArrayItem(ElementName = @"Language", IsNullable = false)]
        public List<string> OSLanguageScope { get; set; }

        [XmlArray(ElementName = @"OSCountryScope", IsNullable = false)]
        [XmlArrayItem(ElementName = @"Region", IsNullable = false)]
        public List<string> OSCountryScope { get; set; }

        public bool IsMatched(UpdateAppInfo appInfo)
        {
            if (appInfo == null) throw new ArgumentNullException(nameof(appInfo));

            var result = false;

            try
            {
                result = IsVersionMatched(appInfo.AppVersion, AppVersionScopes)
                         && IsVersionMatched(appInfo.OSVersion, OSVersionScopes)
                         && IsNameMatched(appInfo.AppChannel, AppChannelScope)
                         && IsNameMatched(appInfo.OSLanguage, OSLanguageScope)
                         && IsNameMatched(appInfo.OSCountry, OSCountryScope);
            }
            catch
            {
                // ignored
            }

            return result;
        }

        private bool IsVersionMatched(Version version, List<VersionScope> versionScopes)
        {
            return versionScopes == null
                   || versionScopes.Count == 0
                   || versionScopes.Any(versionScope => versionScope.IsMatched(version));
        }
        private bool IsNameMatched(string name, List<string> nameScope)
        {
            name = name.Trim();

            return nameScope == null
                   || nameScope.Count == 0
                   || nameScope.Any(
                       currentName => currentName.Trim().Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        private bool IsNumberMatched(int number, List<int> numberScope)
        {
            bool result;

            if (numberScope == null || numberScope.Count == 0)
            {
                result = true;
            }
            else
            {
                result = numberScope.Contains(number);
            }

            return result;
        }
    }

    [XmlRoot("VersionScope")]
    public class VersionScope
    {
        [XmlAttribute("minimum")]
        public string Minimum { get; set; }

        [XmlAttribute("maximum")]
        public string Maximum { get; set; }

        public bool IsMatched(Version version)
        {
            var result = false;

            try
            {
                var maximum = new Version(Maximum);
                var minimum = new Version(Minimum);

                result = version >= minimum && version <= maximum;
            }
            catch
            {
                // ignored
            }

            return result;
        }
    }

    [XmlRoot("AppVersionScopes")]
    public class AppVersionScopes
    {
        [XmlElement("VersionScope")]
        public VersionScope VersionScope { get; set; }
    }
}
