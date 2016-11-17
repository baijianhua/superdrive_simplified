using System;
using System.IO;

namespace SuperDrive.Core.Channel.Protocol
{
    enum PacketType { NONE } //packettype不再需要。
 
    internal class PacketHeader
    {
        public const int PLAIN_FLAG = 0xFA;
        public const int SECURE_FLAG = 0xFB;

        internal static readonly int DefaultLength = 20;
        internal static readonly int DefaultVersion = 1;
        internal int version;

        internal int Flag { get; set; } = PLAIN_FLAG;

        internal PacketType PacketType { get; private set; }

        internal MessageType MessageType { get; private set; }

        internal int BodyLength { get; set; }

        //TODO 为什么要构造一个类？那么多Packet,每次都要这样处理好浪费。不如直接使用int.

        internal PacketHeader(int version, MessageType messageType, int bodyLength)
        {
            this.version = version;
            PacketType = PacketType.NONE;
            MessageType = messageType;
            BodyLength = bodyLength;
        }


        static internal bool IsValidFlag(int flag)
        {
            return flag == PLAIN_FLAG || flag == SECURE_FLAG;
        }

        internal static PacketHeader FromBytes(byte[] source, int offset, int length)
        {
            if( source == null )
            {
                throw new ArgumentNullException();
            }

            if (!(offset >= 0 && offset < source.Length &&
                                                 offset + length <= source.Length))
            {
                throw new ArgumentException();
            }

            using (var reader = new BinaryReader(new MemoryStream(source, offset, length)))
            {
                try
                {
                    var flag = reader.ReadInt32();
                    if (IsValidFlag(flag))
                    {
                        var version = reader.ReadInt32();
                        var packetType = (PacketType)reader.ReadInt32();
                        MessageType messageType = MessageType.Unknown;
                        int type = reader.ReadInt32();
                        if (Enum.IsDefined(typeof(MessageType), type))
                        {
                            messageType = (MessageType)type;
                        }                        
                        var bodyLength = reader.ReadInt32();
                        var ph = new PacketHeader(version, messageType, bodyLength);
                        ph.Flag = flag;
                        return ph;
                    }
                    return null;
                }
                catch
                {
                    return null;
                }
            }
        }

        internal static PacketHeader FromBytes(byte[] source)
        {
            return FromBytes(source, 0, source.Length);
        }

        internal byte[] ToBytes()
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(Flag);
                writer.Write(version);
                writer.Write((int)PacketType);
                writer.Write((int)MessageType);
                writer.Write(BodyLength);

                return stream.ToArray();
            }
        }
    }
}