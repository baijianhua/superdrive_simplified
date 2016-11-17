using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using SuperDrive.Core.Enitity;
using SuperDrive.Core.Messages;
using SuperDrive.Core.Support;

namespace SuperDrive.Core.Business
{
	public class GetItemsRequester :  Requester,ITransferConversation, IRemoteListableConversation
	{
		//private ObservableCollection<Item> _items;
		public TransferBundle TransferBundle { get; set; } = new TransferBundle();
		private string _path = Env.FileSystem.DefaultDir;

		public GetItemsRequester(Device device, string id) : base(device, id)
		{
			IsAutoRecoverable = true;
			_rbu = new BrowseRequestUtil(this);
		}

		/// <summary>
		/// 此次请求下载的内容，是在哪个路径之下？
		/// </summary>
		public string Path { get; set; } = Env.FileSystem.DefaultDir;
		public void SetItems(IEnumerable<Item> items)
		{
			//如果会话开始，不再允许赋值。
			if (IsStarted)
			{
				throw new Exception("Conversation already started. Can not change sending items. Please call SuperDriveCore::Create to start another conversation");
			}
			foreach (var v in items)
			{
				var o = (Item)v.Clone();
				o.ConversationID = Id;
				o.Name = v.Name;
				o.RelativePath = ""; //全是顶层的item.
				o.AbsolutePath = System.IO.Path.Combine(Path, o.Name);
				o.IsRemote = true;
				TransferBundle.AddItem(o);
			}
		}
		public string RemotePath { get; set; }
		protected internal override void OnInitRequest()
		{
			//TODO 先检查哪些Items已经存在。直接更新其状态。
			GetItemsMessage message = new GetItemsMessage(TransferBundle.Items);
			PostMessageAsync(message);
		}

		public void Cancel(List<Item> list)
		{
			var msg = new CancelItemMessage { Items = list };
			PostMessageAsync(msg);
		}

		protected internal override void OnMessageReceived(ConversationMessage message)
		{
			var rep = message as BrowseResponseMessage;
			if (rep != null)
			{
				_rbu.ProcessResponse(rep);
			}
		}

		protected override void OnInitRecover()
		{
			throw new NotImplementedException();
		}
		internal override void InternalWormHole(object obj)
		{
			base.InternalWormHole(obj);
			var item = obj as FileItem;
			if (item != null)
			{
				//如果文件已经存在，若不发送这个消息到对方的话，对方会持续发文件过来。
				//但如果已经做到在发送请求列表之前就剔除，或许不需要？
				//还是不行，因为如果一个文件夹里面有子文件存在，这个逻辑还是需要的
				item.Completed += o => PostMessageAsync(new ConfirmItemMessage(item.Id));
			}
		}

		//TODO 取消该如何处理？
		readonly HttpDownloader _downloader = new HttpDownloader();
		public void StartDownload()=>_downloader.PostItems(TransferBundle.Items);
		private BrowseRequestUtil _rbu;
		public Task<IEnumerable<Item>> GetDirChildren(DirItem dir)=>_rbu.GetDirChildren(dir);
		public DirItem CurrentDir => _rbu.CurrentDir;
	}
}
