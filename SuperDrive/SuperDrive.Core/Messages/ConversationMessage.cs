using System.Collections.Generic;
using Newtonsoft.Json;
using SuperDrive.Core.Business;
using SuperDrive.Core.Channel.Protocol;
using SuperDrive.Core.Enitity;

namespace SuperDrive.Core.Messages
{
    public abstract class ConversationMessage : Message,IConversational
    {
        [JsonProperty(PropertyName = "conversation_id")]
        public string ConversationID { get; set; }
    }

    [JsonObject(Newtonsoft.Json.MemberSerialization.OptIn)]
    public class ConversationAgreeMessage : ConversationMessage
    {
        internal ConversationAgreeMessage()
        {
            Type = MessageType.AgreeConversation;
        }
    }
    public enum RejectCode
    {
        DeSerializeFailed
    }
    [JsonObject(Newtonsoft.Json.MemberSerialization.OptIn)]
    public class ConversationRejectMessage : ConversationMessage
    {
        private RejectCode deSerializeFailed;

        [JsonProperty(PropertyName = "reject_code")]
        public RejectCode RejectCode { get; set; }
        internal ConversationRejectMessage()
        {
            Type = MessageType.RejectConversation;
        }

        public ConversationRejectMessage(RejectCode deSerializeFailed):this()
        {
            this.deSerializeFailed = deSerializeFailed;
        }
    }
    [JsonObject(Newtonsoft.Json.MemberSerialization.OptIn)]
    internal class CancelConversationMessage : ConversationMessage
    {
        internal CancelConversationMessage()
        {
            Type = MessageType.CancelConversation;
        }
    }
    public abstract class ConversationRequestMessage : ConversationMessage
    {
        public Device RemoteDevice { get; internal set; }

        internal abstract Responder CreateResponder();
    }
    //每种恢复会话的请求都是相同的。无需为每种业务单独定义请求恢复的消息。
    internal class ConversationRecoverRequestMessage : ConversationMessage
    {
        internal ConversationRecoverRequestMessage()
        {
            Type = MessageType.ConversationRecoverRequest;
        }
    }
    //每种会话的Response不同，所以这个需要是抽象类
    public abstract class ConversationRecoverAgreedMessage : ConversationAgreeMessage
    {
    }
    [JsonObject(Newtonsoft.Json.MemberSerialization.OptIn)]
    internal class RecoverSendItemsResponse : ConversationRecoverAgreedMessage
    {
        [JsonProperty(PropertyName = "items")]
        public List<Item> Items { get; set; }
        internal RecoverSendItemsResponse()
        {
            Type = MessageType.RecoverSendItemsResponse;
        }
        internal void SetItems(List<Item> transferringItems)
        {
            Items = transferringItems;
        }
    }
}
