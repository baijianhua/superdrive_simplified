using Newtonsoft.Json;
using SuperDrive.Core.Channel.Protocol;
using SuperDrive.Core.Enitity;

namespace SuperDrive.Core.Messages
{
    [JsonObject(Newtonsoft.Json.MemberSerialization.OptIn)]
    public class ConnectMessage : AbstractDeviceMessage
    {
        //用于反序列化
        public ConnectMessage()
        {

        }
        public ConnectMessage(Device device)
        {
            InitFromDevice(device);
            IsSecured = device.IsSecured;
            Type = MessageType.Connect;
        }
        internal bool IsSecured { get; set; }
        [JsonProperty]
        public string ChallengeString { get; internal set; }
        [JsonProperty]
        public string ConnectCode { get; internal set; }
        [JsonProperty]
        public string SessionCode { get; internal set; }
    }
}