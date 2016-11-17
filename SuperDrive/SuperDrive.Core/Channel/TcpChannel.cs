using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using SuperDrive.Core.Channel.Protocol;
using SuperDrive.Core.Support;
using Util = SuperDrive.Core.Support.Util;

namespace SuperDrive.Core.Channel
{

	internal class TcpChannel : IChannel
	{
		private TcpClient _client;
		private TaskWrapper _readTask;
		public bool IsInitiative { get; set; }

		public string RemoteIP { get; set; }

		internal TcpChannel(TcpClient client)
		{
			Util.CheckParam(client != null);
			this._client = client;
			var iep = client?.Client.RemoteEndPoint as IPEndPoint;
			RemoteIP = iep?.Address.ToString();

#pragma warning disable 1998
			_readTask = new TaskWrapper("Channel Listener", ReadWorker_DoWork, TaskCreationOptions.LongRunning);
#pragma warning restore 1998
			_readTask.Start();
		}


		public event Action<ErrorType> ErrorHappened;
		public event Action<Packet> PacketReceived;

		public void Send(Packet packet)
		{
			try
			{
				byte[] buffer = packet.ToBytes();

				if (buffer == null) return;
				////TODO 应该检查packet的长度，禁止发送太大的packet,强制上层发送大对象的时候用SequencableItem.
				int sentCount = 0;
				do
				{
					//await _client.Client.SendAsync();
					sentCount += _client.Client.Send(buffer, sentCount, buffer.Length - sentCount, SocketFlags.None);
				} while (sentCount < buffer.Length);
				//Env.Logger.Log($"Packet Sent {packet}");
			}
			catch (Exception e)
			{
				ErrorHappened?.Invoke(ErrorType.SocketError);
			}

		}

		public void Dispose()
		{
			_readTask.Stop();
			_client?.Dispose();
			_client = null;
		}
		bool IsValidHeader(PacketHeader header)
		{
			bool isValid = header != null && header.MessageType != MessageType.Unknown && header.BodyLength > 0;
#if DEBUG
			if (!isValid)
			{
				Env.ShowMessage("Wrong packet header, check message sent from other side.");
			}
#endif
			return isValid;
		}

		Packet ReadPacket()
		{
			Packet result = null;
			PacketHeader header = ReadPacketHeader();
			if (header?.Flag == PacketHeader.PLAIN_FLAG)
			{
				var readedCount = 0;
				var bytes = Receive(header.BodyLength, out readedCount);
				if (readedCount == header.BodyLength)
				{
					result = new Packet(header, bytes);
				}
			}
			//Env.Logger.Log($"Receive packet {result}");
			return result;
		}
		PacketHeader ReadPacketHeader()
		{
			PacketHeader header = null;
			int readedCount = 0;
			var bytes = Receive(PacketHeader.DefaultLength, out readedCount);
			if (readedCount == PacketHeader.DefaultLength)
			{
				header = PacketHeader.FromBytes(bytes);
				if (!IsValidHeader(header))
				{
					header = null;
				}
			}
			return header;
		}

#pragma warning disable CS1998 // 异步方法缺少 "await" 运算符，将以同步方式运行
		private async Task ReadWorker_DoWork(CancellationToken token)
#pragma warning restore CS1998 // 异步方法缺少 "await" 运算符，将以同步方式运行
		{
			Action cleanup = () =>
			{
				_client?.Dispose();
				_client = null;
			};
			token.Register(cleanup);
			while (!token.IsCancellationRequested && _client.Client != null)
			{
				Packet packet = null;
				try
				{
					packet = ReadPacket();
				}
				catch (SocketException se)
				{
					//如果是网络异常，退出。
#if DEBUG
					//Env1.Instance.ShowMessage($"desc={se.Message}\nStack={se.StackTrace}");
#endif
					//如果在事件句柄里面DisposeTcpChannel，它会wait这个循环结束。而这里又在等待这个事件句柄执行完毕，所以就死锁了。
#pragma warning disable 4014
					Task.Run(() =>
					{
						ErrorHappened?.Invoke(ErrorType.SocketError);
					}, token);
#pragma warning restore 4014
					break;
				}
				catch (Exception ex)
				{
					Env.ShowMessage($"desc={ex.Message}\nStack={ex.StackTrace}");
				}

				if (packet != null)
				{
					try
					{
						//Env.Logger.Log($"Packet received {packet}");
						PacketReceived?.Invoke(packet);
					}
					catch (Exception e)
					{
#if DEBUG
						Env.ShowMessage("逻辑层处理消息异常。请检查\n" + e.Message + "\n" + e.StackTrace);
#else
                        Env.Logger.Log(e.StackTrace);
#endif
					}
				}
			}//end while
			cleanup();
			Env.Logger.Log("ReceiveMessage: Receiving thread end...");
		}



		private byte[] Receive(int length, out int count)
		{
			Util.Check(length > 0);
			count = 0;
			var bytes = new byte[length];
			var socket = _client.Client;


			while (count < length && socket != null)
			{
				count += socket.Receive(bytes, count, length - count, SocketFlags.None);
				if (count == 0)
				{
					//Thread.Sleep(200);
					//throw new SocketException(); //TODO 这个地方再仔细研究一下。为什么会收到0?到底能不能重用？socket receive 可以收到0字节，error = connectionreset.
				}
			}
			return bytes;
		}
	}
}