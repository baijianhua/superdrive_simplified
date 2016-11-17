using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SuperDrive.Core.Enitity;
using SuperDrive.Core.Messages;
using SuperDrive.Core.Support;

namespace SuperDrive.Core.Business
{
        internal class BrowseResponseUtil
        {
                public BrowseResponseUtil()
                {
                }

                public DirItem CurrentFolder { get; set; }
#pragma warning disable 1998
                internal static async Task<BrowseResponseMessage> Response(IItemProviderConversation conv, BrowseRequestMessage browseRequestMessage)
#pragma warning restore 1998
                {
                        var pathId = browseRequestMessage.DirItemId;
                        var dir = conv.FindItem(pathId) as DirItem;
                        var brpm = new BrowseResponseMessage { Id = conv.Id };
                        if (dir == null)
                        {
                                brpm.CurrentDir = null;
                        }
                        else
                        {
	                        brpm.CurrentDir = dir;
	                        brpm.Items = dir.Children;
                        }
                        return brpm;
                }

	}
}