using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SuperDrive.Core.Annotations;
using SuperDrive.Core.Business;
using SuperDrive.Core.Messages;
using SuperDrive.Core.Support;

namespace SuperDrive.Core.Enitity
{
	public interface ICompletable
	{
		event Action<ICompletable> Completed;
		event Action<ICompletable> Started;
	}
	internal interface ISplittable
	{
		Message GetNextMessage();
	}
	//Message是Sendable，所以需要public
	public interface ISendable : ICompletable
	{
		//Post完毕,但不知道是否已经发送到socket.
		//TODO 重构 改成GetMessageCompleted?
		event Action<ISendable> PostCompleted;
		//数据已经全部交给socket。但不知道发送是否成功。
		event Action<ISendable> SendCompleted;
		//已经收到对方确认，发送成功。
		event Action<ISendable> Errored;
		//一个被发送的Item，在需要对方确认时，等待了若干时间后，仍然没有收到对方的确认消息，触发此事件。
		event Action<ISendable> WaitConfirmTimeouted;
		TransferState TransferState { get; set; }
		/// <summary>
		/// 发送的Item，是否需要对确认才算完毕？
		/// </summary>
		bool NeedConfirm { get; set; }
		bool IsPostCompleted { get; }
		/// <summary>
		/// 如果一个被发送的Item需要对方确认才算完毕，这个值表示当Post完毕后过多久多久才算超时出错。
		/// </summary>
		int WaitConfirmTimeoutMilliSeconds { get; set; }
		/// <summary>
		/// 只有在状态变成PostCompleted或者Error的时候才允许返回Null值。
		/// </summary>
		/// <returns></returns>
		Message GetNextMessage();
	}

	public interface IProgressable : ICompletable
	{
		event Action<IProgressable, long> Progressed;
		long TransferredLength { get; set; }
		void Progress(int length);
		long Length { get; set; }

	}
	internal interface ISeekable
	{
		void SeekTo(long position);
	}

	internal class SendableComparer<T> : IComparer<T>
		where T : ISendable
	{
		//会根据T生成不同的Instance。互不干扰。不用模板类不行，List<Item>, List<Sendable>会要求不同的比较器。
		public static SendableComparer<T> Instance = new SendableComparer<T>();
		internal string Name { get; set; }
		public int Compare(T x, T y)
		{
			if (x is SequencableItem)
			{
				if (y is SequencableItem)
				{
					//为何强制类型转换不能通过编译？ SequencableItem x2 = (SequencableItem)x;
					var x1 = x as SequencableItem;
					var y1 = y as SequencableItem;
					return x1.DynamicPriority - y1.DynamicPriority;
				}
				return 1; //x 它后面。
			}
			if (y is SequencableItem)
			{
				return -1; //x 排在ISplittableSendable 前面。
			}
			return 0;
		}
	}
	public abstract class BindableBase : INotifyPropertyChanged
	{

		public event PropertyChangedEventHandler PropertyChanged;
		protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}
	}
	public abstract class SendableBase : BindableBase, ISendable
	{
		public event Action<ISendable> Errored;
		public event Action<ISendable> PostCompleted;
		public event Action<ISendable> SendCompleted;
		public event Action<ISendable> Canceled; 
		public event Action<ISendable> WaitConfirmTimeouted;
		public event Action<ICompletable> Started;
		public event Action<ICompletable> Completed;
		public event Action<SendableBase, TransferState> StateChanged;

		[JsonProperty(PropertyName = "length")]
		public virtual long Length { get; set; } = 0;
		[JsonProperty(PropertyName = "need_confirm")]
		public bool NeedConfirm { get; set; }
		public bool IsPostCompleted => TransferState != TransferState.Idle && TransferState != TransferState.Transferring;
		public int WaitConfirmTimeoutMilliSeconds { get; set; }
		private TransferState _transferState;
		private readonly object _locker = new object();
		public TransferState TransferState
		{
			get { lock (_locker) return _transferState; }
			set
			{
				lock (_locker)
				{
					if (_transferState == TransferState.Completed)
					{
						//如果已经变成传输确认状态，就不要再变化了,
#if DEBUG
						throw new Exception("Item state has already bing confirmed, why change again?");
#else
                        return;
#endif
					}

					if (value == _transferState) return;


					var prevState = _transferState;
					_transferState = value;
					switch (_transferState)
					{
						case TransferState.Transferring:
							if (prevState == TransferState.Idle)
							{
								Started?.Invoke(this);
							}
							break;
						case TransferState.Error:
							OnErrored();
							Errored?.Invoke(this);
							break;
						case TransferState.Canceled:
							OnCanceled();
							Canceled?.Invoke(this);
							break;
						//以下状态对于发送端来说，依次变化。对于接收端来说，仅有Completed状态。
						case TransferState.PostCompleted:
							OnPostCompleted();
							PostCompleted?.Invoke(this);
							break;
						case TransferState.SentCompleted:
							//if (this is FileItem || this is ListSequencable)
							//{
							//	Console.WriteLine("test");
							//}
							SendCompleted?.Invoke(this);
							break;
						case TransferState.Completed:
							OnCompleted();
							Completed?.Invoke(this);
							break;
					}
					StateChanged?.Invoke(this, value);
					OnPropertyChanged();
				}
			}
		}

		protected virtual void OnConfirmed() { }

		protected virtual void OnPostCompleted() { }

		protected virtual void OnErrored() { }

		protected virtual void OnCanceled() { }

		protected virtual void OnCompleted() { }
		Timer _waitConfirmTimer;

		protected SendableBase()
		{
			NeedConfirm = false;
			WaitConfirmTimeoutMilliSeconds = 5000;
		}

		public virtual Message GetNextMessage()
		{
			throw new NotSupportedException();
		}
		public virtual bool IsTransferEnd()
		{
			return TransferState == TransferState.Canceled || TransferState == TransferState.Completed || TransferState == TransferState.Error;
		}
	}

	public enum TransferErrorCode
	{
		FileExistNotEqual,
		CreateFailed,
		WrongPacket,
		NullFileStream,
		ReadFileDataFailed,
		OpenFileError
	}

	internal class ItemJsonConveter : JsonCreationConverter<Item>
	{
		protected override Item Create(Type t, JObject jobj)
		{
			try
			{
				var token = jobj["type"];
				if (token == null) return null;

				var type = (ItemType)Enum.Parse(typeof(ItemType), token.ToString(), true);

				switch (type)
				{
					case ItemType.File:
						return new FileItem();
					case ItemType.Directory:
						return new DirItem();
					default:
						return null;
				}
			}
			catch (Exception)
			{
				return null;
			}
		}
	}

	public abstract class JsonCreationConverter<T> : JsonConverter
	{
		protected abstract T Create(Type objectType, JObject jObject);

		public override bool CanConvert(Type objectType)
		{
			return typeof(T).GetTypeInfo().IsAssignableFrom(objectType.GetTypeInfo());
		}

		public override object ReadJson(JsonReader reader,
										Type objectType,
										 object existingValue,
										 JsonSerializer serializer)
		{
			JObject jObject = JObject.Load(reader);
			T target = Create(objectType, jObject);
			serializer.Populate(jObject.CreateReader(), target);
			return target;
		}

		public override void WriteJson(JsonWriter writer,
									   object value,
									   JsonSerializer serializer)
		{
			try
			{
				serializer.Serialize(writer, value);
			}
			catch (Exception e)
			{
				//TODO 这些异常是否需要处理？
				Console.WriteLine(e.StackTrace);
			}

		}
		//防止self reference loop。http://stackoverflow.com/questions/12314438/self-referencing-loop-in-json-net-jsonserializer-from-custom-jsonconverter-web
		public override bool CanWrite => false;
	}

	[JsonConverter(typeof(ItemJsonConveter))]
	[JsonObject(MemberSerialization.OptIn)]
	public abstract class Item : SendableBase, IConversational, IProgressable, ICloneable
	{
		public TransferErrorCode ErrorCode { get; set; }
		public event Action<IProgressable, long> Progressed;
		[JsonProperty(PropertyName = "conversation_id")]
		public string ConversationID { get; set; }

		private string _id;
		[JsonProperty(PropertyName = "item_id")]
		public virtual string Id
		{
			get { return _id ?? (_id = StringHelper.NewRandomGUID()); }
			//仅仅get不够，因为在反序列化时需要set.
			set { _id = value; }
		}
		[JsonProperty(PropertyName = "name")]
		public string Name { get; set; }
		[JsonProperty(PropertyName = "type")]
		public ItemType Type { get; set; }
		//TODO FileItem和DirItem需要计算相对路径
		[JsonProperty(PropertyName = "relative_path")]
		public string RelativePath { get; set; }
		public bool IsRemote { get; set; }
		//TODO 能不能不用定义这个private字段？自动属性能不能支持OnPropertyChanged?
		public long TransferredLength { get; set; }

		public virtual int Priority { get; set; }
		public string AbsolutePath { get; set; }
		public virtual bool Exists { get; }
		[JsonProperty]
		public DirItem Parent { get; set; }
		public DirItem TopLevelDir { get; internal set; }

		//object itemLocker = new object();
		private Conversation _conv;
		protected internal Conversation Conversation => _conv ?? (_conv = SuperDriveCore.Conversations.GetByKey(ConversationID));

		public TransferBundle TransferBundle { get; set; }

		//仅用于便于子类初始化一些成员。
		protected Item(ItemType type)
		{
			Type = type;
			Exists = true;
		}

		internal void ForceComplete(TransferState state)
		{
			//正常状况下，如果已经IsPostCompleted,就不会更新Length了。
			//异常情况是，当对方通知文件已经存在时，这边不管已经传输多少，直接把状态修改成Confirmed。此时界面上需要直接把进度更新成１００％。
			var tmp = TransferredLength;
			TransferredLength = Length;

			var delta = Length - tmp;
			Progressed?.Invoke(this, delta);
			if (Parent != null)
				((IProgressable)Parent).Progress((int)delta);

			//要放在最后，因为上面Progress会引起父状态变化，而这里会导致递归检查父状态。
			TransferState = state;
		}

		void IProgressable.Progress(int len)
		{
			//传送文件的时候，如果文件的状态已经变成了Confirm或者Completed, 再调用Progress是不对的。
			if (IsTransferEnd()) return;
			//即使len == 0, 只要用户有意调用这个函数，还是要更改TransferState,这对于空文件是有意义的。会在下面直接把文件变成Completed状态。
			if (TransferredLength == 0) TransferState = TransferState.Transferring;

			TransferredLength += len;
			Progressed?.Invoke(this, len);

			if (TransferredLength >= Length)
			{
				TransferState = TransferState.Completed;
				if (TransferredLength > Length)
				{
					var msg = "Transfered Lenght > Total Length. some time this does happens, and the transfer is success.";
					Env.Logger.Log(msg);
				}
			}

			((IProgressable)TopLevelDir)?.Progress(len);
			TransferBundle?.Progress(len);
		}
		public abstract object Clone();
	}

	internal interface ICloneable
	{
	}
}
