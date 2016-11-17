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

    internal abstract class TcpChannel : IChannel
    {
        public bool IsInitiative { get; set; }

        public string RemoteIP { get; set; }


        public event Action<ErrorType> ErrorHappened;
        public event Action<Packet> PacketReceived;

        public abstract void Send(Packet packet);

        public void Dispose()
        {
            
        }
    }
}