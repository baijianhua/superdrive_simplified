using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SuperDrive.Core.Support
{
    public class Consts
    {
#if DEBUG
        public static double DefaultConnectTimeoutSeconds = 60;
#else
        public static double DefaultConnectTimeoutSeconds = 15;
#endif

        public const string SessionId = "__SESSION_ID__";
        public const string ItemId = "__ITEM_ID__";
        public const string GetItemUriPath = "getitem";
        public const string GetThumbnailUriPath = "thumbnail";

        //TODO 能不能用同一个端口？
        public const int DiscoverPort = 49999;
        public const int ChannelPort  = 49998;
        public const int HttpPort     = 49997;
    }
}
