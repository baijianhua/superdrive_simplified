using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using SuperDrive.Core.Enitity;
using SuperDrive.Core.Messages;

namespace SuperDrive.Core.Business
{
	public class SendItemsRequester : Requester, ITransferConversation, IItemProviderConversation
	{
		public TransferBundle TransferBundle { get; } = new TransferBundle();
		public async Task SetItems(IEnumerable<Item> items)
		{
			if (IsStarted)
			{
				throw new Exception("Conversation already started. Can not change sending items. Please call SuperDriveCore::Create to start another conversation");
			}
			await Task.Run(() =>
			{
				foreach (var tmp in items)
				{
					Item item = (Item)tmp.Clone();
					var af = item as AbstractFileItem;
					af?.GetLength();
					item.NeedConfirm = item.Length != 0;
					item.WaitConfirmTimeoutMilliSeconds = 10000; //等待确认时间为10秒钟
					item.RelativePath = "";
					item.ConversationID = Id;

					TransferBundle.AddItem(item);
				}
			});
			//深度拷贝。只有这样，同一个文件发送给多个目标，进度等状态信息才能独立跟踪。事件才能独立处理。
		}
		protected internal override void OnInitRequest()
		{
			SendItemsMessage message = new SendItemsMessage(TransferBundle.Items);
			PostMessageAsync(message);
		}

		public void Cancel(List<Item> list)
		{
			throw new NotImplementedException();
		}

		protected internal override void OnRecoverAgreed(ConversationRecoverAgreedMessage recoverResponseMessage)
		{
			throw new NotImplementedException();
		}
		public SendItemsRequester(Device device, string id) : base(device, id)
		{
			IsAutoRecoverable = true;
		}
		protected internal override async void OnMessageReceived(ConversationMessage message)
		{
			var brm = message as BrowseRequestMessage;
			if (brm != null)
			{
				//需要查找父目录，看当前请求的文件或者目录，是不是在他们的子项目，如果是子项目，才给回应。
				var msg = await BrowseResponseUtil.Response(this, brm);
				PostMessageAsync(msg);
				return;
			}

			if (message is CancelItemMessage)
			{
				throw new NotImplementedException();
			}
			else if (message is ReceiveItemErrorMessage)
			{
				throw new NotImplementedException();
			}
		}

		public AbstractFileItem FindItem(string itemId) => TransferBundle.FindItem(itemId);
	}
}
