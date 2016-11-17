using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using SuperDrive.Core.Channel.Protocol;
using SuperDrive.Core.Enitity;

namespace SuperDrive.Core.Messages
{
    public class BrowseResponseMessage : ConversationAgreeMessage
    {
        [JsonProperty]
        public String BrowserId { get; set; }
      
        [JsonProperty]
        public DirItem CurrentDir { get; set; }

        [JsonProperty]
        public IEnumerable<Item> Items { get; set; }

        public BrowseResponseMessage()
        {
            Type = MessageType.BrowseResponse;
        }


    }
}