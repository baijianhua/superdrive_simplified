using ConnectTo.Foundation.Core;
using Newtonsoft.Json;
using System.Collections.Generic;
using ConnectTo.Foundation.Business;
using System;
using System.Linq;

namespace ConnectTo.Foundation.Messages
{
    [JsonObject(Newtonsoft.Json.MemberSerialization.OptIn)]
    public class GetItemsMessage : ConversationRequestMessage
    {

        [JsonProperty(PropertyName = "items")]
        public List<Item> Items { get; set; }

        [JsonProperty(PropertyName = "total_length")]
        public long TotalLength { get; set; }

        [JsonProperty(PropertyName = "browser_Id")]
        public string BrowseId { get; set; }

        public string Path { get; internal set; }

        public GetItemsMessage()
        {
            Type = Protocol.MessageType.GetItems;
        }

        public GetItemsMessage(List<Item> items) : this()
        {
            Items = items.Where(o => o.TransferState != TransferState.Completed).ToList();
        }

        internal override Responder CreateResponder()
        {
            return new GetItemsResponder();
        }
    }
    [JsonObject(Newtonsoft.Json.MemberSerialization.OptIn)]
    internal class GetItemsRecoverMessage: ConversationRecoverRequestMessage
    {
        internal GetItemsRecoverMessage()
        {
            Type = Protocol.MessageType.GetItemsRecoverRequest;
        }
        [JsonProperty(PropertyName = "items")]
        public List<Item> Items { get; internal set; }
    }
}
