using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using SuperDrive.Core.Business;
using SuperDrive.Core.Channel.Protocol;
using SuperDrive.Core.Enitity;

namespace SuperDrive.Core.Messages
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
            Type = MessageType.GetItems;
        }

        public GetItemsMessage(IEnumerable<Item> items) : this()
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
            Type = MessageType.GetItemsRecoverRequest;
        }
        [JsonProperty(PropertyName = "items")]
        public List<Item> Items { get; internal set; }
    }
}
