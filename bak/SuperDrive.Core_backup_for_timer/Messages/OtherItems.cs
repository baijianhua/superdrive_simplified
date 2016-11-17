using System;
using System.Collections.Generic;
using System.Text;
using ConnectTo.Foundation.Core;

namespace ConnectTo.Foundation.Messages
{

    internal class ItemMessage : Message
    {
        public Item Item { get; internal set; }
    }
}
