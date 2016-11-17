using System;
using System.Collections.Generic;
using System.Text;
using ConnectTo.Foundation.Core;
using ConnectTo.Foundation.Messages;
using Newtonsoft.Json;
using ConnectTo.Foundation.Protocol;

namespace ConnectTo.Foundation.MISC
{
    [JsonObject(Newtonsoft.Json.MemberSerialization.OptIn)]
    public class ConnectQRCode
    {
        internal static readonly byte MASK = 0x32;

        [JsonProperty]
        public string SSID { get; set; } = string.Empty;
        [JsonProperty]
        public ConnectMessage ConnectMessage { get; set; }
        [JsonProperty]
        public string SSIDPassword { get; set; } = string.Empty;
        public Device ExtractedDevice { get; set; }
        [JsonProperty]
        public string ConnectCode { get; internal set; }

        [JsonProperty(PropertyName = "PV")]
        public int ProtocolVersion { get; internal set;}

        internal static string Mask(string json)
        {
            StringBuilder sb = new StringBuilder();
            for(int i=0; i<json.Length;i++)
            {
                var c = json[i] ^ MASK;
                sb.Append((char)c);
            }
            return sb.ToString();
        }
    }
}
