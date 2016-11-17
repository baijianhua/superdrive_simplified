using Newtonsoft.Json;
using SuperDrive.Core.Channel.Protocol;

namespace SuperDrive.Core.Messages
{
    [JsonObject(Newtonsoft.Json.MemberSerialization.OptIn)]
    internal class ConfirmMessage : Message
    {
        internal ConfirmMessage()
        {
            Type = MessageType.Confirm;
        }
    }
}