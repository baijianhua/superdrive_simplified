using System;
using SuperDrive.Core.Enitity;

namespace SuperDrive.Core.Messages
{
    abstract class AbstractPeerMessage:Message
    {
        internal string Id { get; set; }

        internal string Name { get; set; }

        internal Avatar Avatar { get; set; }

        internal string IPAddress { get; set; }

        internal Version Version { get; set; }
    }
}
