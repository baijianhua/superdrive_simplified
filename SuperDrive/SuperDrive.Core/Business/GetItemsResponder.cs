using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using SuperDrive.Core.Annotations;
using SuperDrive.Core.Enitity;
using SuperDrive.Core.Messages;
using SuperDrive.Core.Support;

namespace SuperDrive.Core.Business
{
        public class GetItemsResponder : Responder, ITransferConversation, IItemProviderConversation
        {
                internal override ConversationRequestMessage RequestMessage
                {
                        set
                        {
                                var msg = value as GetItemsMessage;
	                        if (msg == null) return;

				foreach (var item in msg.Items)
				{
					item.AbsolutePath = Util.FromBase64(item.Id);
					if (!item.Exists) continue;

					item.ConversationID = Id;
					item.RelativePath = "";
					TransferBundle.AddItem(item);
				}
                        }
                }

                internal GetItemsResponder()
                {
                }

                public void Cancel(List<Item> list)
                {
			throw new NotImplementedException();
                }

	        public TransferBundle TransferBundle { get; } = new TransferBundle();

	        protected internal override async void OnMessageReceived(ConversationMessage message)
                {
                        var brm = message as BrowseRequestMessage;
                        if (brm != null)
                        {
                                //需要查找父目录，看当前请求的文件或者目录，是不是在他们的子项目，如果是子项目，才给回应。
                                var msg = await BrowseResponseUtil.Response(this, brm);
                                PostMessageAsync(msg);
                        }
                        else if (message is GetItemsRecoverMessage)
                        {
                                throw new NotImplementedException();
                        }
                        else if (message is CancelItemMessage)
                        {
                                throw new NotImplementedException();
                        }
                }

#pragma warning disable 1998
                protected override async Task<ConversationAgreeMessage> OnAgreed()
#pragma warning restore 1998
                {
                        //获取要下载的所有Item的长度，并且把Item的详细信息填充到Response消息里面。
                        var msg = new GetItemAgreedMessage();
	                // ReSharper disable once LoopCanBePartlyConvertedToQuery
                        foreach (var item in TransferBundle.Items.Select(i=>i as AbstractFileItem).Where(af=>af != null))
                        {
                                item.GetLength();
                        }
                        msg.Items = TransferBundle.Items;
                        return msg;
                }

                public AbstractFileItem FindItem(string itemId)=>TransferBundle.FindItem(itemId);
        }
}
