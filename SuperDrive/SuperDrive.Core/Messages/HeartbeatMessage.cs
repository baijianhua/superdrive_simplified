using SuperDrive.Core.Channel.Protocol;

namespace SuperDrive.Core.Messages
{
    internal class HeartbeatMessage : Message
    {
        internal HeartbeatMessage()
        {
            Type = MessageType.Heartbeat;
        }
    }
}