using ConnectTo.Foundation.Common;
using ConnectTo.Foundation.Messages;
using ConnectTo.Foundation.Protocol;
using System;
using ConnectTo.Foundation.Core;
using Connect2.Foundation.Security;

namespace ConnectTo.Foundation.Channel
{
    public interface IChannel : IDisposable
    {
        bool IsInitiative { get; }
        string RemoteIP { get; set; }

        void Send(Packet packet);
        //void StartHeartbeat();

        event Action<ErrorType> ErrorHappened;
        event Action<Packet> PacketReceived;
    }
    public enum ErrorType
    {
        Parsing,
        Exception,
        Timeout,
        SocketError,
    }
}