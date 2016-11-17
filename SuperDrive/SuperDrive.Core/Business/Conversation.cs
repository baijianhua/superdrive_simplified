using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using SuperDrive.Core.Enitity;
using SuperDrive.Core.Messages;

namespace SuperDrive.Core.Business
{
	public interface IConversation
	{
		string Id { get; set; }
		Device Peer { get; set; }
	}

	public interface ITransferConversation : IConversation
	{
		void Cancel();
		void Cancel(List<Item> list);
		TransferBundle TransferBundle { get; }
	}

	public interface IItemProviderConversation : IConversation
	{
		AbstractFileItem FindItem(string itemId);
	}

	public interface IRemoteListableConversation
	{
		Task<IEnumerable<Item>> GetDirChildren(DirItem dir);
		DirItem CurrentDir { get;  }
	}

	public abstract class Conversation : BindableBase, IConversation
	{
		internal bool IsStarted { get; set; }
		public string Id { get; set; }
		public virtual Device Peer { get; set; }
		public event Action<Conversation> Terminated;

		protected internal void PostMessageAsync(Message sendable)
		{
#pragma warning disable CS4014 // 由于此调用不会等待，因此在调用完成前将继续执行当前方法
			PostMessage(sendable);
#pragma warning restore CS4014 // 由于此调用不会等待，因此在调用完成前将继续执行当前方法
		}

		protected internal async Task<bool> PostMessage(Message sendable)
		{
			AttachConversationId(sendable);
			return await Peer.Post(sendable).ConfigureAwait(false);
		}
		public void Terminate()
		{
			Terminated?.Invoke(this);
			End();
		}
		private void AttachConversationId(ISendable sendable)
		{
			if (sendable is IConversational)
			{
				((IConversational)sendable).ConversationID = Id;
			}
		}
		private int _seconds = 5 * 60; //默认5分钟超时
		protected Timer conversationTimeoutTimer;
		//internal ConversationState State { get; set; }
		public event Action<Conversation> Timeout;
		public Action<Conversation> Errored { get; set; }

		internal int TimeoutSeconds
		{
			get
			{
				return _seconds;
			}
			set
			{
				if (value <= 0)
				{
					throw new ArgumentOutOfRangeException("TimeoutSeconds", "must > 0");
				}

				_seconds = value;
				if (IsStarted) //如果会话已经开始，重新设定计时器。
				{
					StartTimer();
				}
			}
		}
		/// <summary>
		/// 注意，这个方法切勿滥用！！！这个方法的初衷，是为了能够让Conversation中发生的子文件夹中的文件，或者其他子项目，本身没有
		/// 被Conversation直接管理，而当发生问题或者状态变化的时候，有需要让Conversation知道。
		/// 所以任何一个东西，只要让Conversation知道一次就可以了。其他事件变化，应该通过事件来追踪。
		/// 
		/// 如果滥用了这个方法，如果在其中给对象绑定了事件，会被多次绑定，出现莫名其妙的错误。
		/// </summary>
		/// <param name="obj"></param>
		internal virtual void InternalWormHole(object obj) { }
		protected void StartTimer()
		{
			//conversationTimeoutTimer?.Stop();
			//if (conversationTimeoutTimer == null)
			//{
			//    conversationTimeoutTimer = new Timer();
			//    conversationTimeoutTimer.AutoReset = false; //一次性
			//    conversationTimeoutTimer.Elapsed += OnTimeoutImpl;
			//}
			//conversationTimeoutTimer.Interval = _seconds * 1000;
			//conversationTimeoutTimer.Start();

		}
		//void OnTimeoutImpl(object sender, ElapsedEventArgs e)
		//{
		//    //会话超时意味着什么？如果用户想要恢复，可以重新调用Start
		//    End();
		//    Timeout?.Invoke(this);
		//}


		protected internal virtual bool Process(ConversationMessage message)
		{
			//收到一次应答
			//conversationTimeoutTimer?.ReStart();
			OnMessageReceived(message);
			return true;
		}

		protected internal virtual void OnMessageReceived(ConversationMessage message)
		{

		}



		internal virtual void End()
		{
			//conversationTimeoutTimer?.Stop();
			SuperDriveCore.RemoveConversation(this);

		}

		public void Cancel()
		{
			PostMessage(new CancelConversationMessage());
		}

		public virtual void CancelOperation()
		{

		}
	}

	internal class PendableAction
	{
		private Timer timer;
		private object locker = new object();
		private Device device;
		private Action action;
		private double timeout;
		private Action timeoutAction;


		public PendableAction(Device device, Action action, double timeout, Action timeoutAction)
		{
			this.device = device;
			this.action = action;
			//TODO 是否存在不设置timeout和timeoutAction的情况？
			this.timeout = timeout;
			this.timeoutAction = timeoutAction;

			timer = new Timer(_ => timeoutAction?.Invoke(), null, (int)timeout, 0);

		}

		public void Cancel()
		{
			lock (locker)
			{
				timer.Dispose();
			}
		}

		internal void Invoke()
		{
			//避免发生超时的回调代码与这个方法同时执行。这里表示要做这件事，超时的则在说不要做了。
			lock (locker)
			{
				//为什么Invoke的时候timer会是空的？没设置timer?
				timer.Dispose();
			}
			action?.Invoke();
		}
	}
}
