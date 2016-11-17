using ConnectTo.Foundation.Protocol;

namespace ConnectTo.Foundation.Messages
{
    internal class CancelMessage : Message
    {
        internal CancelMessage()
        {
            Type = MessageType.Cancel;
        }
    }
}
