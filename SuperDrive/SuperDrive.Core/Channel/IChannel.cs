using System;
using SuperDrive.Core.Channel.Protocol;

namespace SuperDrive.Core.Channel
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