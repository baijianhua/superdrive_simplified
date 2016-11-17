using System;
using ConnectTo.Foundation.Core;
using ConnectTo.Foundation.Protocol;

namespace ConnectTo.Foundation.Messages
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