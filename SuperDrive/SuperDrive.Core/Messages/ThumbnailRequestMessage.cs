using System.Collections.Generic;
using Newtonsoft.Json;
using SuperDrive.Core.Channel.Protocol;

namespace SuperDrive.Core.Messages
{
    public class ThumbnailRequestMessage : ConversationMessage
    {
        [JsonProperty]
        public List<string> itemIDList { get; set; }

        public ThumbnailRequestMessage()
        {
            Type = MessageType.ThumbnailRequest;
        }
    }
}