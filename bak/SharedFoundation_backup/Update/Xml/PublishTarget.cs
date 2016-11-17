using System.Xml.Serialization;

namespace ConnectTo.Foundation.Update.Xml
{
    [XmlRoot("PublishTarget")]
    public class PublishTarget
    {
        [XmlElement("WebPackage")]
        public WebPackage WebPackage { get; set; }

        [XmlElement("LocalFilter")]
        public Filter LocalFilter { get; set; }
    }
}
