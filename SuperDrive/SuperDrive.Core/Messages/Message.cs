using System;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SuperDrive.Core.Business;
using SuperDrive.Core.Channel.Protocol;
using SuperDrive.Core.Enitity;

namespace SuperDrive.Core.Messages
{
    [JsonObject(MemberSerialization.OptIn)]
    public abstract class Message:SendableBase 
    {
        /// <summary>
        /// msg_id
        /// </summary>
        [JsonProperty(PropertyName = "msg_id")]
        internal string Id { get; set; }
        /// <summary>
        /// version
        /// </summary>
        [JsonProperty(PropertyName = "version")]
        internal string Version { get; set; }


        protected Message()
        {
            Type = MessageType.Unknown;
        }
        internal MessageType Type { get; set; }

        
        internal virtual Packet ToPacket()
        {
            var bytes = ToPacketBodyImpl();
            PacketHeader header = new PacketHeader(PacketHeader.DefaultVersion, Type, bytes.Length);
            return new Packet(header, bytes);
        }

        //要不在一个地方，转化所有的类型，要不就每个类型负责转化自己。每个类型转化自己更好一些。它最了解自己。
        internal static Message FromPacket(Packet packet)
        {
            try
            {
                var message = Create(packet.Header.MessageType);
                message?.FromBytesImpl(packet.Body);
                return message;
            }
            catch(Exception e)
            {
                Env.Logger.Log("ExtractPakcet to Message Exception",stackTrace:e.StackTrace);
                return null;
            }
            
        }
        //除非通过泛型传入具体类型，否则通过反射中的Type是没办法生成具体类型的。
        //TODO 如何根据type找到一个Message的具体类型？ 用反射有更好的方法吗？
        //定义成一个map可以少写点代码？
        private static Message Create(MessageType t)
        {
            switch (t)
            {
                case MessageType.Accept:
                    return new AcceptMessage();
                case MessageType.Reject:
                    return new RejectMessage();
                case MessageType.Disconnect:
                    return new DisconnectMessage();
                case MessageType.Acknowledge:
                    return new AcknowledgeMessage();
                case MessageType.Cancel:
                    return new CancelMessage();
                case MessageType.CancelItems:
                    return new CancelItemMessage();
                case MessageType.Confirm:
                    return new ConfirmMessage();
                case MessageType.Discover:
                    return new OnlineMessage();
                case MessageType.Connect:
                    return new ConnectMessage();
                case MessageType.SendItems:
                    return new SendItemsMessage();
                case MessageType.AgreeConversation:
                    return new ConversationAgreeMessage();
                case MessageType.BrowseRequest:
                    return new BrowseRequestMessage();
                case MessageType.BrowseResponse:
                    return new BrowseResponseMessage();
                case MessageType.GetItems:
                    return new GetItemsMessage();
                case MessageType.ThumbnailRequest:
                    return new ThumbnailRequestMessage();
                case MessageType.ConfirmItem:
                    return new ConfirmItemMessage();
                case MessageType.RecoverSendItemsResponse:
                    return new RecoverSendItemsResponse();
                case MessageType.ConversationRecoverRequest:
                    return new ConversationRecoverRequestMessage();
                case MessageType.GetItemsRecoverRequest:
                    return new GetItemsRecoverMessage();
                case MessageType.RejectConversation:
                    return new ConversationRejectMessage();
                case MessageType.ReceiveItemError:
                    return new ReceiveItemErrorMessage();
                case MessageType.Offline:
                    return new OfflineMessage();
                case MessageType.ChannelReady:
                    return new ChannelReadyMessage();
                case MessageType.GetItemAgreed:
                    return new GetItemAgreedMessage();
                default:
#if DEBUG
                    throw new Exception("type="+ t + "Messages::Message.Create中没有实例化这个类型的消息，请在其中增加一个case statement");
#else
                    return null;
#endif
            }
        }
        string ToJson()=>JsonConvert.SerializeObject(this, Env.JsonSetting);
        public override string ToString() => $"{Type} {ToJson()}" ;
        protected virtual byte[] ToPacketBodyImpl()=>Encoding.UTF8.GetBytes(ToJson());
        protected virtual void FromBytesImpl(byte[] body)
        {
            var json = Encoding.UTF8.GetString(body,0,body.Length);
            JsonConvert.PopulateObject(json, this);
        }
        //String也有ToString...:(
        public override Message GetNextMessage()
        {
            TransferState = TransferState.PostCompleted;
            return this;
        }
    }

    
}
