using System;
using System.Net;
using System.Threading;
using System.Net.Sockets;
using System.Threading.Tasks;
using NetworkLibrary.Interfaces;

namespace NetworkLibrary.Tcp
{
    public class TcpServer<TPackage> : IThreaded, IDisposable
        where TPackage : IPackage, new()
    {
        #region Fields

        private int _backlog;
        private int _bufferSize;
        private int _maxPackageSize;

        private Socket _socket;

        private Thread _serverThread;
        private volatile bool _isRunning;

        private event EventHandler<TcpClient<TPackage>> _clientConnected;
        private readonly object _clientConnectedLock;

        private event EventHandler<Exception> _stopped;
        private readonly object _stoppedLock;

        #endregion

        #region Constructors

        public TcpServer(EndPoint listeningEndPoint)
        {
            if (listeningEndPoint == null)
            {
                throw new ArgumentNullException(nameof(listeningEndPoint));
            }

            _clientConnectedLock = new object();
            _stoppedLock = new object();

            _backlog = 10;
            _bufferSize = 1024;
            _maxPackageSize = 1024 * 1024;

            _socket = new Socket(listeningEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            _socket.Bind(listeningEndPoint);
        }

        public TcpServer(IPAddress listeningAddress, int listeningPort) : this(new IPEndPoint(listeningAddress, listeningPort))
        {
        }

        #endregion

        #region Accessors

        public bool IsRunning
        {
            get
            {
                return _isRunning;
            }
        }

        public int Backlog
        {
            get
            {
                return _backlog;
            }
            set
            {
                if (value < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), "Value must be greater than or equal to zero.");
                }

                _backlog = value;
            }
        }

        public int BufferSize
        {
            get
            {
                return _bufferSize;
            }
            set
            {
                if (value <= 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), "Value must be greater than zero.");
                }

                _bufferSize = value;
            }
        }

        public int MaxPackageSize
        {
            get
            {
                return _maxPackageSize;
            }
            set
            {
                if (value <= 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), "Value must be greater than zero.");
                }

                _maxPackageSize = value;
            }
        }

        public EndPoint LocalEndPoint
        {
            get
            {
                return _socket.LocalEndPoint;
            }
        }

        public event EventHandler<TcpClient<TPackage>> ClientConnected
        {
            add
            {
                lock (_clientConnectedLock)
                {
                    _clientConnected += value;
                }
            }
            remove
            {
                lock (_clientConnectedLock)
                {
                    _clientConnected -= value;
                }
            }
        }

        public event EventHandler<Exception> Stopped
        {
            add
            {
                lock (_stoppedLock)
                {
                    _stopped += value;
                }
            }
            remove
            {
                lock (_stoppedLock)
                {
                    _stopped -= value;
                }
            }
        }

        #endregion

        #region Methods

        #region Public

        public void Listen()
        {
            _socket.Listen(_backlog);
        }

        public void Start(CancellationToken cancellationToken, bool isBackground = false)
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            if (_isRunning)
            {
                throw new InvalidOperationException("You cannot start a server that is already running.");
            }

            _isRunning = true;
            _serverThread = new Thread(() => RunBackgroundWork(cancellationToken))
            {
                IsBackground = isBackground
            };
            _serverThread.Start();
        }

        public virtual TcpClient<TPackage> AcceptClient()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            var clientSocket = _socket.Accept();
            return new TcpClient<TPackage>(clientSocket)
            {
                BufferSize = _bufferSize,
                MaxPackageSize = _maxPackageSize
            };
        }

        public virtual async Task<TcpClient<TPackage>> AcceptClientAsync()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            var clientSocket = await Task.Factory.FromAsync((ac, s) => _socket.BeginAccept(ac, s),
                (ar) => _socket.EndAccept(ar), null);
            return new TcpClient<TPackage>(clientSocket)
            {
                BufferSize = _bufferSize,
                MaxPackageSize = _maxPackageSize
            };
        }

        public void Close()
        {
            Dispose();
        }

        #endregion

        #region Private

        private void RunBackgroundWork(CancellationToken cancellationToken)
        {
            Exception exception = null;

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var tcpClient = AcceptClient();
                    _clientConnected?.Invoke(this, tcpClient);
                }
                catch (SocketException socketException) when (socketException.NativeErrorCode == 10004)
                {
                    exception = socketException;
                    break;
                }
            }

            _isRunning = false;
            _stopped?.Invoke(this, exception);
        }

        #endregion

        #region Dispose

        private bool _isDisposed;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool isDisposing)
        {
            if (_isDisposed)
            {
                return;
            }

            if (isDisposing)
            {
                _socket?.Dispose();
            }

            _isDisposed = true;
        }

        #endregion

        #endregion
    }
}
