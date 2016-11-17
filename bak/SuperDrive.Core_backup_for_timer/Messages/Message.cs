using System;
using ConnectTo.Foundation.Protocol;
using ConnectTo.Foundation.Core;
using Newtonsoft.Json;
using System.Text;
using Newtonsoft.Json.Linq;
using ConnectTo.Foundation.Business;
using SuperDrive.Library;

namespace ConnectTo.Foundation.Messages
{
    [JsonObject(MemberSerialization.OptIn)]
    public abstract class Message:SendableBase,ISendable 
    {
        /// <summary>
        /// msg_id
        /// </summary>
        [JsonProperty(PropertyName = "msg_id")]
        internal string ID { get; set; }
        /// <summary>
        /// version
        /// </summary>
        [JsonProperty(PropertyName = "version")]
        internal string Version { get; set; }


        protected Message()
        {
            this.Type = MessageType.Unknown;
            Length = 0; //大部分的消息不关心长度。关心长度的消息自己会重新设置这个值
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
                Env.Instance.Logger.Error(e, "ExtractPakcet to Message Exception");
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
                case MessageType.FileDataMessage:
                    return new FileDataMessage();
                case MessageType.BrowseRequest:
                    return new BrowseRequestMessage();
                case MessageType.BrowseResponse:
                    return new BrowseResponseMessage();
                case MessageType.GetItems:
                    return new GetItemsMessage();
                case MessageType.ThumbnailRequest:
                    return new ThumbnailRequestMessage();
                case MessageType.ThumbnailResponse:
                    return new ThumbnailResponseMessage();
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
                default:
#if DEBUG
                    throw new Exception("type="+ t + "Messages::Message.Create中没有实例化这个类型的消息，请在其中增加一个case statement");
#else
                    return null;
#endif
            }
        }
        protected virtual byte[] ToPacketBodyImpl()
        {
            var json = JsonConvert.SerializeObject(
                this, Env.Instance.JsonSetting);

            var bytes = Encoding.UTF8.GetBytes(json);
            return bytes;
        }
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

    internal class ItemJsonConveter : JsonCreationConverter<Item>
    {
        protected override Item Create(Type t, JObject jobj)
        {
            try
            {
                var token = jobj["type"];
                if (token == null) return null;

                var type = (ItemType) Enum.Parse(typeof(ItemType), token.ToString(), true);

                switch (type)
                {
                    case ItemType.File:
                        return new FileItem();
                    case ItemType.Directory:
                        return new DirItem();
                    default:
                        return null;
                }
            }
            catch (Exception)
            {
                return null;
            }
        }
    }

    public abstract class JsonCreationConverter<T> : JsonConverter
    {
        protected abstract T Create(Type objectType, JObject jObject);

        public override bool CanConvert(Type objectType)
        {
            return typeof(T).IsAssignableFrom(objectType);
        }

        public override object ReadJson(JsonReader reader,
                                        Type objectType,
                                         object existingValue,
                                         JsonSerializer serializer)
        {
            JObject jObject = JObject.Load(reader);
            T target = Create(objectType, jObject);
            serializer.Populate(jObject.CreateReader(), target);
            return target;
        }

        public override void WriteJson(JsonWriter writer,
                                       object value,
                                       JsonSerializer serializer)
        {
            try
            {
                serializer.Serialize(writer, value);
            }
            catch (Exception e)
            {
                //TODO 这些异常是否需要处理？
                Console.WriteLine(e.StackTrace);
            }
            
        }
        //防止self reference loop。http://stackoverflow.com/questions/12314438/self-referencing-loop-in-json-net-jsonserializer-from-custom-jsonconverter-web
        public override bool CanWrite
        {
            get
            {
                return false;
            }
        }
    }
}
