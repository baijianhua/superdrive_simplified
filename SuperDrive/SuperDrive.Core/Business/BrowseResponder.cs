using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SuperDrive.Core.Enitity;
using SuperDrive.Core.Messages;
using SuperDrive.Core.Support;

namespace SuperDrive.Core.Business
{
	public class BrowseResponder : Responder, IItemProviderConversation
	{
		private readonly BrowseResponseUtil _bru;
		//private BrowseResponseMessage _respondMessage = null;
		List<DirItem> _browseHistories = new List<DirItem>();
		internal BrowseResponder()
		{
			_bru = new BrowseResponseUtil();
		}
		public DirItem CurrentFolder { get; set; }
		protected override async Task<ConversationAgreeMessage> OnAgreed()
		{
			return await BrowseResponseUtil.Response(this, RequestMessage as BrowseRequestMessage);
		}
		protected internal override async void OnMessageReceived(ConversationMessage message)
		{
			var brMessage = message as BrowseRequestMessage;
			if (brMessage == null) return;

			#region comment
			//TODO 如果是安卓又该如何处理？首先需要搞清楚这是在分类浏览，还是在浏览目录，如果是在浏览目录，这个代码可用
			//如果是在分类浏览，那么需要去数据库中查询，并返回列表。

			//如果安卓做分类浏览，那么分类浏览中有没有子目录的概念？有的图库中有多个相册，是如何处理的？多个相册看起来像是文件夹的概念。

			//TODO 新拍摄的图片之类的，没有立即存储到MediaStore，如何处理？
			//TODO 如果有生成并缓存缩略图，那些缩略图要被MediaStore忽略才行。

			//http://blog.csdn.net/yhcelebrite/article/details/11714925
			//http://blog.csdn.net/bgc525725278/article/details/8131657              
			#endregion

			var respondMessage = await BrowseResponseUtil.Response(this, brMessage);
			
			if (CurrentFolder?.Children.FirstOrDefault(c => c.Id == respondMessage.CurrentDir?.Id) != null)
			{
				//如果这次要求浏览的目录是上次浏览的目录的子目录
				//CurrentFolder.FolderString
				respondMessage.CurrentDir.Parent = CurrentFolder;
			}
			CurrentFolder = respondMessage.CurrentDir;
			if (CurrentFolder != null)
			{
				if (NamedFolders.IsNamedFolderId(CurrentFolder.Id))
				{
					_browseHistories.Clear(); //已经到顶层目录，不会再有返回操作。
				}
				_browseHistories.Add(CurrentFolder);
			}
			PostMessageAsync(respondMessage);
		}

		public AbstractFileItem FindItem(string itemId)
		{
			//请求顶层Item
			if (NamedFolders.IsNamedFolderId(itemId)) return NamedFolders.GetFolderById(itemId);
			//未指定请求目录
			if (CurrentFolder == null) return NamedFolders.ImageDir;
			//另一端的返回操作？看返回的路径，是不是以前提供过的，提供过才允许上一层，否则用户一直上一层，可以看到不许他看的数据
			AbstractFileItem af = _browseHistories.FirstOrDefault(c => c.Id == itemId);
			//以上都不是，看看请求的item是不是提供内容的子目录。
			af = af ?? CurrentFolder.Children.FirstOrDefault(c => c.Id == itemId);
			return af;
		}
	}
}