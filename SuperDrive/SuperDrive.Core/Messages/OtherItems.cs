using SuperDrive.Core.Enitity;

namespace SuperDrive.Core.Messages
{

    internal class ItemMessage : Message
    {
        public Item Item { get; internal set; }
    }
}
