using System;
using ConnectTo.Foundation.Business;
using Newtonsoft.Json;
using ConnectTo.Foundation.Protocol;

namespace ConnectTo.Foundation.Messages
{
    public class BrowseRequestMessage : ConversationRequestMessage
    {

        [JsonProperty]
        public String browserId { get; set; }

        [JsonProperty]
        public int maxItemCount { get; set; }

        [JsonProperty]
        public int offset { get; set; }

        [JsonProperty]
        public string path { get; set; }

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