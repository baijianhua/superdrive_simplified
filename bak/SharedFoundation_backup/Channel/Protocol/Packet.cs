using System;
using Connect2.Foundation.Security;
using ConnectTo.Foundation.Business;

namespace ConnectTo.Foundation.Protocol
{
    public class Packet
    {
        internal Packet(PacketHeader header, byte[] body)
        {
            if (header == null || body == null )
            {
                throw new ArgumentNullException();
            }

            PacketType = header.PacketType;
            MessageType = header.MessageType;
            Header = header;
            Body = body;
        }

        internal PacketType PacketType { get; private set; }

        internal MessageType MessageType { get; private set; }

        internal PacketHeader Header { get; private set;}

        internal byte[] Body { get; private set; }

        internal byte[] ToBytes(ISecureCrypto crypto = null)
        {
            byte[] result = null;
            byte[] headerBytes = null;
            if(crypto == null)
            {
                Header.Flag = PacketHeader.PLAIN_FLAG;
            }else
            {
                Header.Flag = PacketHeader.SECURE_FLAG;
                Body = crypto.Encrypt(Body);
                if(Body != null)
                    Header.BodyLength = Body.Length;
            }
            headerBytes = Header.ToBytes();
            if(Body != null && headerBytes != null)
            {
                result = new byte[headerBytes.Length + Body.Length];
                Buffer.BlockCopy(headerBytes, 0, result, 0, headerBytes.Length);
                Buffer.BlockCopy(Body, 0, result, headerBytes.Length, Body.Length);
            }
            return result;
        }
    }
}