using System;
using ConnectTo.Foundation.Business;
using Newtonsoft.Json;
using ConnectTo.Foundation.Protocol;
using ConnectTo.Foundation.Core;
using System.Collections.Generic;

namespace ConnectTo.Foundation.Messages
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