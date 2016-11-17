using SuperDrive.Core.Channel.Protocol;

namespace SuperDrive.Core.Messages
{
    internal class ChannelReadyMessage : Message
    {
        internal ChannelReadyMessage()
        {
            Type = MessageType.ChannelReady;
        }
    }
    internal class UnpairMessage : Message
    {
        public UnpairMessage()
        {
            Type = MessageType.Unpair;
        }
    }
}