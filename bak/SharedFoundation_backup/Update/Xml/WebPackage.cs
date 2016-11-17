using System;
using System.Xml.Serialization;

namespace ConnectTo.Foundation.Update.Xml
{
    [XmlRoot("WebPackage")]
    public class WebPackage
    {
        [XmlElement("Version")]
        public string Version { get; set; }

        [XmlElement("MD5")]
        public string MD5 { get; set; }

        [XmlElement("Address")]
        public string Address { get; set; }

        [XmlAttribute("timestamp")]
        public string Timestamp { get; set; }
    }
}
