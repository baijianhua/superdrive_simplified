using ConnectTo.Foundation.Core;
using Newtonsoft.Json;
using System.Collections.Generic;
using ConnectTo.Foundation.Business;
using System;
using ConnectTo.Foundation.Protocol;

namespace ConnectTo.Foundation.Messages
{
    [JsonObject(Newtonsoft.Json.MemberSerialization.OptIn)]
    public class SendItemsMessage : ConversationRequestMessage
    {
        [JsonProperty(PropertyName = "parent_item_id")]
        public string ParentItemID { get; set; }
        [JsonProperty(PropertyName = "items")]
        public List<Item> Items { get; set; }

        [JsonProperty(PropertyName = "total_length")]
        public long TotalLength { get; set; }

        public SendItemsMessage()
        {
            Type = MessageType.SendItems;
        }

        public SendItemsMessage(List<Item> items) : this()
        {
            Items = items;
            if(items != null)
                items.ForEach(i => TotalLength += i.Length);
        }

        internal override Responder CreateResponder()
        {
            return new SendItemsResponder();
        }

        
    }

    [JsonObject(Newtonsoft.Json.MemberSerialization.OptIn)]
    internal class CancelItemMessage : ConversationMessage
    {
        [JsonProperty(PropertyName = "items")]
        internal List<Item> Items { get; set; }

        internal CancelItemMessage()
        {
            Type = MessageType.CancelItems;
        }
    }
    [JsonObject(MemberSerialization.OptIn)]
    internal class ConfirmItemMessage : ConversationMessage
    {
        [JsonProperty(PropertyName = "item_id")]
        internal string ItemID { get; set; }
        internal ConfirmItemMessage()
        {
            Type = MessageType.ConfirmItem;
        }

        public ConfirmItemMessage(string ItemID):this()
        {
            this.ItemID = ItemID;
        }
    }

    [JsonObject(MemberSerialization.OptIn)]
    internal class ReceiveItemErrorMessage : ConversationMessage
    {
        [JsonProperty(PropertyName = "item_id")]
        internal string ItemID { get; set; }
        public TransferErrorCode ErrorCode { get; internal set; }

        public ReceiveItemErrorMessage(string iD):this()
        {
            Type = MessageType.ReceiveItemError;
            this.ItemID = iD;
        }

        public ReceiveItemErrorMessage()
        {
        }
    }
}
