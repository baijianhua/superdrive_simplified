using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace SuperDrive.Core.Abstractions
{
    partial class TcpListenerAdapter
    {
        public TcpListenerAdapter(IPEndPoint localEndpoint)
        {
            LocalEndpoint = localEndpoint;

            Initialize();
        }

        public IPEndPoint LocalEndpoint { get; private set; }
       
        public Task<TcpClientAdapter> AcceptTcpClientAsync() 
        {
            return acceptTcpClientAsyncInternal();
        }
    }

    partial class TcpClientAdapter
    {
        public IPEndPoint LocalEndPoint
        {
            get;
            private set;
        }

        public IPEndPoint RemoteEndPoint
        {
            get;
            private set;
        }
    }

    partial class TcpListenerAdapter
    {
        private TcpListener _tcpListener;

        private void Initialize()
        {
            _tcpListener = new TcpListener(LocalEndpoint);
        }

        private async Task<TcpClientAdapter> acceptTcpClientAsyncInternal()
        {
            var tcpClient = await _tcpListener.AcceptTcpClientAsync();
            return new TcpClientAdapter(tcpClient);
        }

        public void Start()
        {
            _tcpListener.Start();
        }

        public void Stop()
        {
            _tcpListener.Stop();
        }

        public Socket Socket
        {
            get
            {
                return _tcpListener.Server;
            }
        }

    }

    partial class TcpClientAdapter
    {
        private TcpClient tcpClient;

        public TcpClientAdapter(TcpClient tcpClient)
        {
            this.tcpClient = tcpClient;

            LocalEndPoint = (IPEndPoint)tcpClient.Client.LocalEndPoint;
            RemoteEndPoint = (IPEndPoint)tcpClient.Client.RemoteEndPoint;
        }

        public Stream GetInputStream()
        {
            return this.tcpClient.GetStream();
        }

        public Stream GetOutputStream()
        {
            return this.tcpClient.GetStream();
        }

        public void Dispose()
        {
            this.tcpClient.Dispose();
        }
    }
}