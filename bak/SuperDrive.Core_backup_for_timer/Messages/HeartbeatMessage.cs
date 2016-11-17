using ConnectTo.Foundation.Messages;
using ConnectTo.Foundation.Protocol;

namespace ConnectTo.Foundation.Core
{
    internal class HeartbeatMessage : Message
    {
        internal HeartbeatMessage()
        {
            Type = MessageType.Heartbeat;
        }
    }
}