using System;
using System.ComponentModel;
using System.IO;
using System.Net.Sockets;
using NLog;
using ConnectTo.Foundation.Helper;
using ConnectTo.Foundation.Common;
using ConnectTo.Foundation.Protocol;
using System.Windows;
using ConnectTo.Foundation.Business;
using ConnectTo.Foundation.Messages;
using System.Net;
using System.ServiceModel;
using System.Threading;
using Connect2.Foundation.Security;
using ConnectTo.Foundation.Core;

namespace ConnectTo.Foundation.Channel
{

    internal class TcpChannel : IChannel
    {
        private readonly TcpClient client;

        private readonly BackgroundWorker readWorker;

        public ISecureCrypto Crypto { get; set; }

        public bool IsInitiative { get; set; }

        public string RemoteIP { get; set; }

        internal TcpChannel(TcpClient client)
        {
            Preconditions.ArgumentNullException(client != null);
            this.client = client;

            if (client.Client.RemoteEndPoint is IPEndPoint)
            {
                var iep = client.Client.RemoteEndPoint as IPEndPoint;
                RemoteIP = iep.Address.ToString();
            }
            //client.Client.ReceiveTimeout = 6000;
            readWorker = new BackgroundWorker { WorkerSupportsCancellation = true };
            readWorker.DoWork += ReadWorker_DoWork;
            readWorker.RunWorkerAsync();
        }


        public event Action<ErrorType> ErrorHappened;
        public event Action<Packet> PacketReceived;

        public void Send(Packet packet)
        {
            try
            {
                byte[] buffer = packet.ToBytes(Crypto);

                if (buffer == null) return;
                ////TODO 应该检查packet的长度，禁止发送太大的packet,强制上层发送大对象的时候用SequencableItem.
                int sentCount = 0;
                do
                {
                    sentCount += client.Client.Send(buffer, sentCount, buffer.Length - sentCount, SocketFlags.None);
                } while (sentCount < buffer.Length);
            }
            catch (Exception e)
            {
                ErrorHappened?.Invoke(ErrorType.SocketError);
            }

        }

        public void Dispose()
        {
            readWorker.CancelAsync();
        }
        bool IsValidHeader(PacketHeader header)
        {
            bool isValid = header != null && header.MessageType != MessageType.Unknown && header.BodyLength > 0;
#if DEBUG
            if(!isValid)
            {
                Env.Instance.ShowMessage("Wrong packet header, check message sent from other side.");
            }
#endif
            return isValid;
        }

        Packet ReadPacket()
        {
            Packet result = null;
            int readedCount = 0;
            PacketHeader header = ReadPacketHeader();
            if (header != null)
            {
                if(header.Flag == PacketHeader.PLAIN_FLAG)
                {
                    
                    var bytes = Receive(header.BodyLength, out readedCount);
                    if (readedCount == header.BodyLength)
                    {
                        result = Crypto == null ? new Packet(header, bytes) : null;
                    }
                }
                else if(header.Flag == PacketHeader.SECURE_FLAG)
                {
                    var bytes = Receive(header.BodyLength, out readedCount);
                    if (readedCount != header.BodyLength)
                    {
                        return null;
                    }
                    uint saltBytesCount = 0;
                    uint initiaVectorBytesCount = 0;
                    SecureHead.DecodeByteCounts(bytes, 0, out saltBytesCount, out initiaVectorBytesCount);

                    
                    int secureHeadTotalBytes = (int)(SecureHead.FixedUnitFieldsBytesCount + saltBytesCount + initiaVectorBytesCount);
                    SecureHead secureHead = SecureHead.Decode(bytes, 2 * sizeof(uint), saltBytesCount, initiaVectorBytesCount);

                    byte[] body = Crypto.Decrypt(bytes, 2*sizeof(uint)+(uint)secureHeadTotalBytes, secureHead.SecureBytesCount, secureHead);
                    if(body != null)
                    {
                        return new Packet(header, body);
                    }
                }
            }
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
        private bool IsWorking = false;
        private void ReadWorker_DoWork(object sender, DoWorkEventArgs args)
        {
            var worker = sender as BackgroundWorker;
            if (worker == null) return;

            Env.Instance.Logger.Trace("ReceiveMessage: Receiving thread start...");
            IsWorking = true;
            
            while (!worker.CancellationPending && client.Client != null)
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
                    //Env.Instance.ShowMessage($"desc={se.Message}\nStack={se.StackTrace}");
#endif
                    ErrorHappened?.Invoke(ErrorType.SocketError);
                    break;
                }
                catch (Exception ex)
                {
                    Env.Instance.ShowMessage($"desc={ex.Message}\nStack={ex.StackTrace}");
                }
                    
                if(packet != null)
                {
                    try
                    {
                        PacketReceived?.Invoke(packet);
                    }
                    catch (Exception e)
                    {
#if DEBUG
                        Env.Instance.ShowMessage("逻辑层处理消息异常。请检查\n" + e.Message + "\n" + e.StackTrace);
#else
                        Env.Instance.Logger.Error(e);
#endif
                    }
                }
            }//end while
            IsWorking = false;
            Env.Instance.Logger.Trace("ReceiveMessage: Receiving thread end...");
        }

        
        private byte[] Receive(int length,out int count)
        {
            Preconditions.Check(length > 0);
            count = 0;
            var bytes = new byte[length];
            var socket = client.Client;
            

            while (count < length && socket != null)
            {
                count += socket.Receive(bytes, count, length - count, SocketFlags.None);
                if(count == 0)
                {
                    //Thread.Sleep(200);
                    //throw new SocketException(); //TODO 这个地方再仔细研究一下。为什么会收到0?到底能不能重用？socket receive 可以收到0字节，error = connectionreset.
                }
            }
            return bytes;
        }
    }
}