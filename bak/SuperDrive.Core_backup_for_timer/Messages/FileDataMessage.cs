using System;
using System.IO;
using ConnectTo.Foundation.Business;
using ConnectTo.Foundation.Protocol;

namespace ConnectTo.Foundation.Messages
{
    internal class FileDataMessage : ConversationMessage
    {
        public static readonly int BUFFER_SIZE = 1024 * 8;
        internal FileDataMessage(){
            Type = MessageType.FileDataMessage;
        }
        public string ItemID { get; set; }
        public long Offset { get; set; }
        
        public byte[] Data { get; set; }

        protected override void FromBytesImpl(byte[] body)
        {
            var reader = new BinaryReader(new MemoryStream(body));
            ConversationID = reader.ReadString();
            ItemID = reader.ReadString();
            Offset = reader.ReadInt64();
            Length = reader.ReadInt64();
            Data = reader.ReadBytes((int)Length);
        }

        protected override byte[] ToPacketBodyImpl()
        {
            var stream = new MemoryStream();
            var writer = new BinaryWriter(stream);
            writer.Write(ConversationID);
            writer.Write(ItemID);
            writer.Write(Offset);
            writer.Write(Length);
            writer.Write(Data);
#if DEBUG
            if((Data == null && Length !=0) || Length != Data.Length)
            {
                throw new Exception("Wrong Message Gennerated, please check!");
            }
#endif
            return stream.ToArray();
        }
    }
}
