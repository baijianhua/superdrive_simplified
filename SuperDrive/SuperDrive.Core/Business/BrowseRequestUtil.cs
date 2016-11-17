using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SuperDrive.Core.Enitity;
using SuperDrive.Core.Messages;
using SuperDrive.Core.Support;

namespace SuperDrive.Core.Business
{
	internal class BrowseRequestUtil
	{
		private BrowseRequestMessage _currentRequest;
		public DirItem CurrentDir { get; internal set; }
		private readonly Conversation _conversation;
		TaskCompletionSource<IEnumerable<Item>> _listResult = new TaskCompletionSource<IEnumerable<Item>>();
		internal BrowseRequestUtil(IRemoteListableConversation conversation)
		{
			_conversation = conversation as Conversation;
			if (_conversation == null)
			{
				throw new Exception($"Must be {nameof(IRemoteListableConversation)} {nameof(Conversation)}");
			}
		}
		public void Cancel()
		{
			_listResult?.TrySetResult(null);
			_listResult = null;
		}
		//����û����������һ��Ŀ¼����һ����������������Ŀ¼������ô������
		public Task<IEnumerable<Item>> GetDirChildren(DirItem dir, TaskCompletionSource<ConversationMessage> response = null)
		{
			if (dir.Equals(CurrentDir) && _listResult != null)
				return _listResult.Task; //����Ѿ�������ˣ��������������������ͬ���͵ȴ��ϴη��ؽ����
			//��ʾ���������������һ�ε����󷵻�null��
			_listResult?.TrySetResult(null);

			CurrentDir = dir;
			_listResult = new TaskCompletionSource<IEnumerable<Item>>();
			_listResult.SetValueWhenTimeout(TimeSpan.FromSeconds(Consts.DefaultConnectTimeoutSeconds), null);
			_currentRequest = new BrowseRequestMessage { DirItemId = dir.Id };
			//Env.Logger.Log($"Post Message {_currentRequest}", nameof(RemoteBrowser));
			var postTask = _conversation.PostMessage(_currentRequest);
			postTask.ConfigureAwait(false);
			if (postTask.Result) return _listResult.Task;

			_listResult.TrySetResult(null);
			response?.TrySetResult(null);
			return _listResult.Task;
		}

		internal void ProcessResponse(BrowseResponseMessage rep)
		{
			if (rep == null || rep.BrowserId != _currentRequest?.BrowserId) return;

			CurrentDir = rep.CurrentDir;
			foreach (var item in rep.Items)
			{
				item.Parent = rep.CurrentDir;
			}
			_listResult?.TrySetResult(rep.Items);
			_listResult = null;
		}
	}
}