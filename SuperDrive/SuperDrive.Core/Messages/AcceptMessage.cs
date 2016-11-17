using SuperDrive.Core.Channel.Protocol;
using SuperDrive.Core.Enitity;

namespace SuperDrive.Core.Messages
{
    internal class AcceptMessage : AbstractDeviceMessage
    {
        [Newtonsoft.Json.JsonProperty]
        public string ChallengeResponse { get; internal set; }
        [Newtonsoft.Json.JsonProperty]
        public string SessionCode { get; internal set; }

        internal AcceptMessage(Device localDevice):this()
        {
            InitFromDevice(localDevice);
        }
        internal AcceptMessage()
        {
            Type = MessageType.Accept;
        }
    }
}