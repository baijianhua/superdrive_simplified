using System;
using ConnectTo.Foundation.Business;
using Newtonsoft.Json;
using ConnectTo.Foundation.Protocol;
using ConnectTo.Foundation.Core;
using System.IO;

namespace ConnectTo.Foundation.Messages
{
    public class ThumbnailResponseMessage : ConversationMessage
    {
        public ThumbnailResponseMessage()
        {
            Type = MessageType.ThumbnailResponse;
        }

        public string ID { get; set; }

        public string Name { get; set; }

        public byte[] Data { get; set; }

        protected override void FromBytesImpl(byte[] body)
        {
            var reader = new BinaryReader(new MemoryStream(body));
            ConversationID = reader.ReadString();
            ID = reader.ReadString();
            Name = reader.ReadString();
            Length = reader.ReadInt64();
            Data = reader.ReadBytes((int)Length);
        }

        protected override byte[] ToPacketBodyImpl()
        {
            var stream = new MemoryStream();
            var writer = new BinaryWriter(stream);
            writer.Write(ConversationID);
            writer.Write(ID);
            writer.Write(Name);
            writer.Write(Length);
            writer.Write(Data);
#if DEBUG
            if ((Data == null && Length != 0) || Length != Data.Length)
            {
                throw new Exception("Wrong Message Gennerated, please check!");
            }
#endif
            return stream.ToArray();
        }

    }
}