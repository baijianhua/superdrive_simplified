using Newtonsoft.Json;
using SuperDrive.Core.Channel.Protocol;

namespace SuperDrive.Core.Messages
{
    [JsonObject(MemberSerialization.OptIn)]
    internal class DisconnectMessage : Message
    {
        internal DisconnectMessage()
        {
            Type = MessageType.Disconnect;
        }

        [Newtonsoft.Json.JsonProperty]
        public bool? UnpairMark { get; internal set; } = null;
    }
}
