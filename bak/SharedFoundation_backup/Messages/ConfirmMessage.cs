using ConnectTo.Foundation.Protocol;
using Newtonsoft.Json;

namespace ConnectTo.Foundation.Messages
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