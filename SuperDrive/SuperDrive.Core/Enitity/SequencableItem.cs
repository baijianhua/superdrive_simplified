using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using SuperDrive.Core.Messages;
using Util = SuperDrive.Core.Support.Util;

namespace SuperDrive.Core.Enitity
{
	public abstract class SequencableItem : Item
	{
		int _proirty = 1;
		private const int TimeAdjustor = 1000; //1秒钟。
		private bool firstSent = true;
		public SequencableItem() : base(ItemType.Sequencable)
		{
		}

		public SequencableItem(ItemType type) : base(type)
		{
		}

		public int Priority
		{
			get
			{
				return _proirty;
			}
			set
			{
				if (value < 1 || value > 10)
				{
					throw new ArgumentOutOfRangeException("Priority", "0< Priority<10");
				}
				_proirty = value;
			}


		}
		/// <summary>
		/// 动态优先级和优先级不能合并是因为优先级是用户主动设置的，需要尊重并保留这个设置。
		/// </summary>
		public virtual int DynamicPriority
		{
			get
			{
				//一个TimeAdjustor，提高一个优先级。
				return _proirty - (Environment.TickCount - LastProcessedTime) / TimeAdjustor;
			}
		}
		public int LastProcessedTime { get; set; }

		public void RecordLastProcessedTime()
		{
			LastProcessedTime = Environment.TickCount;
		}
		//seal是因为希望所有的子类共用检查TransferState的代码。不希望这个逻辑被破坏。
		public sealed override Message GetNextMessage()
		{
			var message = GetNextMessageImpl();

			//!!!!注意，这个地方加代码要小心。因为它的调用关系很复杂，会父子传递，还会递归(尽量调用GetNextMessageImpl可以避免一点)。

			//例如，别人调用DirItem.GetNextMessage时，DirItem会调用FileItem的GetNextMessage(通过SequencableItem) ，
			//在FileItem里面，这个代码被执行了一次。然后在DirItem里面，这个代码又被执行了一次。例如原来message.SentCompleted += ()=>Progress，就出了问题。
			//针对目录调用GetNextMessage,  目录回去调用FileItem.GetNextMessage, 那里面进度汇报了一次，然后回到目录的GetNextMessage，进度又被汇报了一次。

			//本来在这里有这样的代码，但想想还是算了。宁可在每个真正生成Message地方设置ConversationID吧。在这个地方做这个事情，考虑到上面的因素，会被执行好多次。
			//((ConversationMessage)message).ConversationID = ConversationID;

			if (message == null)
			{
				if (TransferState != TransferState.Error && TransferState != TransferState.PostCompleted)
				{
					message = GetNextMessageImpl();
				}
			}

			//这几行代码有没有问题？只要确定这个东西是每个Item本身都需要的，那就没问题。容器和容器中的项目，都需要收到属于自己的SendCompleted事件。
			//Progress不同。同一个message，不应该导致两个Progress Event，所以不能在这里处理。

			//TODO 重构 这个实现可以工作，但并不够好。如果返回的Message是Null，现在并没有把TransferState=PostCompleted,这个函数就会再执行一次。而且上一次就该SendCompleted
			//到这一次才更新。有什么更好的办法没有？


			if (TransferState == TransferState.PostCompleted)
			{
				if (message != null)
				{
					message.SendCompleted += o => TransferState = TransferState.SentCompleted;
				}
				else
				{
					TransferState = TransferState.SentCompleted;
				}
			}

			return message;
		}

		protected virtual Message GetNextMessageImpl()
		{
			throw new NotImplementedException();
		}
	}

	//class ListSequencable : SequencableItem
	//{
	//	List<Item> _items = null;
	//	object _locker = new object();
	//	public bool StopWhenSubItemError { get; set; }

	//	//是不是这个构造函数要允许ISendable进来？
	//	internal ListSequencable(string conversationId, IEnumerable<Item> items)
	//	{
	//		Util.Check(items != null);
	//		StopWhenSubItemError = true;
	//		ConversationID = conversationId;
	//		//注意，这里不要做深拷贝。
	//		this._items = items.ToList();
	//		this._items.Sort(SendableComparer<Item>.Instance);
	//		_items.ForEach(item =>
	//		{
	//			item.Parent = this;
	//			Length += item.Length;
	//		});
	//	}

	//	public List<Item> Items => _items;

	//	protected override Message GetNextMessageImpl()
	//	{
	//		RecordLastProcessedTime();
	//		Message message = null;

	//		//Debug.WriteLine("Item status before get message Name=[" + Name + "] Length=" + Length + " TransferedLength="+TransferredLength);
	//		//items.ForEach(i => Debug.WriteLine("name="+i.Name+ " Id="+i.Id));

	//		lock (_locker)
	//		{
	//			_items.RemoveAll(sendable => sendable == null
	//			|| sendable.IsPostCompleted
	//			|| sendable.TransferState == TransferState.Canceled
	//			|| sendable.TransferState == TransferState.Error);
	//		}

	//		if (_items.Count > 0)
	//		{
	//			//大部分时候，内部不用考虑发送优先级。因为同一批次的文件，并行发送没有意义，甚至是否需要sort都不一定。
	//			//但如果用户手工调整呢？
	//			//items.Sort(SendableComparer<Item>.Instance);
	//			Item selectedItem = null;
	//			lock (_locker)
	//			{
	//				selectedItem = _items[0];
	//			}
	//			message = selectedItem.GetNextMessage();
	//			if (message == null)
	//			{
	//				//如果得到了一条空消息，而且不是错误的，尝试下一条消息。递归会导致直到读到非空消息或者所有消息读完为止。
	//				//在一个文件夹含有空文件时，会有这种情况。在当前的子元素为一个ListSequencable而且ItemCount = 0的时候，也会得到一个null的message.
	//				if (selectedItem.TransferState == TransferState.Error)
	//				{
	//					if (StopWhenSubItemError)
	//					{
	//						//如果一个子项目Error了，这个项目也要变成Error. 这会导致整个项目从发送队列里面移除。
	//						TransferState = TransferState.Error;
	//					}
	//					RemoveItem(selectedItem);
	//				}
	//				else if (selectedItem.IsPostCompleted)
	//				{
	//					RemoveItem(selectedItem);
	//				}
	//			}
	//		}
	//		else
	//		{
	//			TransferState = TransferState.PostCompleted;
	//		}

	//		return message;
	//	}
	//	void RemoveItem(Item item)
	//	{
	//		lock (_locker)
	//		{
	//			_items.Remove(item);
	//			if (_items.Count == 0)
	//			{
	//				TransferState = TransferState.PostCompleted;
	//			}
	//		}
	//	}
	//	internal void Clear()
	//	{
	//		lock (_locker)
	//		{
	//			_items?.Clear();
	//			Length = 0;
	//		}

	//	}
	//	/// <summary>
	//	/// 即使是正在传送一个文件发了这个命令之后，也会终止。因为GetNextMessage的实现，会取Item0, 而如果Item已经被移除了
	//	/// Item 0 取到的就是另一个东西了。不过这个操作，要相应的更新Length属性。
	//	/// </summary>

	//	internal void Remove(List<Item> list)
	//	{
	//		lock (_locker)
	//		{
	//			list.ForEach((i) =>
	//			{
	//				_items.Remove(i);
	//				Length -= i.Length;
	//				if (i.TransferState == TransferState.Transferring)
	//					TransferredLength -= i.TransferredLength;
	//			});

	//			if (_items.Count == 0)
	//			{
	//				TransferState = TransferState.PostCompleted;
	//			}
	//		}
	//	}

	//	public override object Clone()
	//	{
	//		//将来如果实现的话，记得复制元素所有内容。
	//		throw new NotImplementedException();
	//	}
	//}
}
