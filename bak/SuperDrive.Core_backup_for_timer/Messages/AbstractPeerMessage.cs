using ConnectTo.Foundation.Core;
using System;

namespace ConnectTo.Foundation.Messages
{
    abstract class AbstractPeerMessage:Message
    {
        internal string ID { get; set; }

        internal string Name { get; set; }

        internal Avatar Avatar { get; set; }

        internal string IPAddress { get; set; }

        internal Version Version { get; set; }
    }
}
