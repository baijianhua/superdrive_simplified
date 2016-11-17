using System;
using Newtonsoft.Json;
using SuperDrive.Core.Business;
using SuperDrive.Core.Channel.Protocol;

namespace SuperDrive.Core.Messages
{
    public class BrowseRequestMessage : ConversationRequestMessage
    {

        [JsonProperty]
        public string BrowserId { get; set; }

        [JsonProperty]
        public int MaxItemCount { get; set; }

        [JsonProperty]
        public int Offset { get; set; }

        [JsonProperty]
        public string DirItemId { get; set; }

        public BrowseRequestMessage()
        {
            Type = MessageType.BrowseRequest;
        }

        internal override Responder CreateResponder()
        {
            return new BrowseResponder();
        }

    }
}