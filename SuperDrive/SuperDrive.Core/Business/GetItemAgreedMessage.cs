using System.Collections.Generic;
using System.Collections.ObjectModel;
using Newtonsoft.Json;
using SuperDrive.Core.Channel.Protocol;
using SuperDrive.Core.Enitity;
using SuperDrive.Core.Messages;

namespace SuperDrive.Core.Business
{
    public class GetItemAgreedMessage:ConversationAgreeMessage
    {
        public GetItemAgreedMessage()
        {
            Type = MessageType.GetItemAgreed;
        }
        [JsonProperty]
        public IEnumerable<Item> Items { get; set; }
    }
}