using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using SuperDrive.Core.Enitity;
using SuperDrive.Core.Messages;
using System.Threading.Tasks;

namespace SuperDrive.Core.Business
{
        public class SendItemsResponder : Responder, ITransferConversation, IRemoteListableConversation
	{
		public TransferBundle TransferBundle { get; } = new TransferBundle();

	        internal SendItemsResponder()
	        {
			 _browseRequestHelper = new BrowseRequestUtil(this);
		}
                public void Cancel(List<Item> list)
                {
	                var msg = new CancelItemMessage {Items = list};
	                PostMessageAsync(msg);
                }

                internal override ConversationRequestMessage RequestMessage
                {
                        set
                        {
                                var msg = value as SendItemsMessage;
	                        if (msg == null) return;

	                        base.RequestMessage = value;
				

				foreach (var v in msg.Items)
				{
					v.AbsolutePath = System.IO.Path.Combine(Path, v.Name);
					v.ConversationID = Id;
					v.IsRemote = true;
					TransferBundle.AddItem(v);
				}
			}
                }
	        public string Path { get; set; } = Env.FileSystem.DefaultDir;
		protected override Task<ConversationAgreeMessage> OnAgreed()
                {
			//如果想下载到不同位置，调用Agree(path)
			_downloader.PostItems(TransferBundle.Items);
			return base.OnAgreed();
                }
                readonly HttpDownloader _downloader = new HttpDownloader();
		protected internal override void OnMessageReceived(ConversationMessage message)
                {
	                var itemMessage = message as CancelItemMessage;
	                if (itemMessage != null)
                        {
                                throw new NotImplementedException();
                        }

			var browseResponse = message as BrowseResponseMessage;
			// ReSharper disable once InvertIf
			if (browseResponse != null)
			{
				_browseRequestHelper.ProcessResponse(browseResponse);
			} 
                }

	        public void Agree(string v)
	        {
		        Path = v;
                        Agree();
                }

	        private BrowseRequestUtil _browseRequestHelper;
		public Task<IEnumerable<Item>> GetDirChildren(DirItem dir)=>_browseRequestHelper.GetDirChildren(dir);
	        public DirItem CurrentDir => _browseRequestHelper.CurrentDir;
	}
}
