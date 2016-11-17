using Newtonsoft.Json;
using SuperDrive.Core.Channel.Protocol;

namespace SuperDrive.Core.Messages
{
    [JsonObject(MemberSerialization.OptIn)]
    internal class RejectMessage : Message
    {
        
        internal const int CONNECT_CODE_INCORRECT = 1;
        internal const int ALLOW_ONLY_ONE_PAIR_DEVICE  = 2;

        [JsonProperty]
        public int? RejectCode { get; set; } = null;
        internal RejectMessage()
        {
            Type = MessageType.Reject;
        }
    }
}