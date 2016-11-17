using SuperDrive.Core.Channel.Protocol;

namespace SuperDrive.Core.Messages
{
    internal class CancelMessage : Message
    {
        internal CancelMessage()
        {
            Type = MessageType.Cancel;
        }
    }
}
