using System;
using ConnectTo.Foundation.Business;
using Newtonsoft.Json;
using System.Collections.Generic;
using ConnectTo.Foundation.Core;

namespace ConnectTo.Foundation.Messages
{
    public class BrowseResponseMessage : ConversationMessage
    {
        [JsonProperty]
        public String browserId { get; set; }
      
        [JsonProperty]
        public string path { get; set; }

        [JsonProperty]
        public List<Item> Items { get; set; }

        public BrowseResponseMessage()
        {
            Type = Protocol.MessageType.BrowseResponse;
        }


    }
}