using ConnectTo.Foundation.Protocol;
using System;
using Newtonsoft.Json;

namespace ConnectTo.Foundation.Messages
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
