using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Net.Sockets;
using System.Threading.Tasks;
using NetworkLibrary.Interfaces;

namespace NetworkLibrary.Tcp
{
    public class TcpClient<TPackage> : ITcpClient<TPackage>, IDisposable
        where TPackage : IPackage, new()
    {
        #region Fields

        private int _bufferSize;

        private Socket _socket;
        private NetworkStream _networkStream;
        private LengthPrefixProtocol _lengthPrefixProtocol;

        private Thread _receiveThread;
        private volatile bool _isRunning;

        private event EventHandler<TPackage> _packageReceived;
        private readonly object _packageReceivedLock;

        private event EventHandler<Exception> _stopped;
        private readonly object _stoppedLock;

        #endregion

        #region Constructors

        public TcpClient() : this(IPAddress.Any, 0)
        {
        }

        public TcpClient(EndPoint localEndPoint)
        {
            if (localEndPoint == null)
            {
                throw new ArgumentNullException(nameof(localEndPoint));
            }

            _packageReceivedLock = new object();
            _stoppedLock = new object();

            _bufferSize = 1024;

            _socket = new Socket(localEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            _socket.Bind(localEndPoint);
            
            _lengthPrefixProtocol = new LengthPrefixProtocol(1024 * 1024);
            _lengthPrefixProtocol.DataReceived += OnDataReceived;
        }

        public TcpClient(IPAddress localAddress, int localPort) : this(new IPEndPoint(localAddress, localPort))
        {
        }

        internal TcpClient(Socket socket)
        {
            if (socket == null)
            {
                throw new ArgumentNullException(nameof(socket));
            }

            _packageReceivedLock = new object();
            _stoppedLock = new object();

            _bufferSize = 1024;

            _socket = socket;
            _networkStream = new NetworkStream(_socket);

            _lengthPrefixProtocol = new LengthPrefixProtocol(1024 * 1024);
            _lengthPrefixProtocol.DataReceived += OnDataReceived;
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
                return _lengthPrefixProtocol.MaxDataSize;
            }
            set
            {
                _lengthPrefixProtocol.MaxDataSize = value;
            }
        }

        public EndPoint LocalEndPoint
        {
            get
            {
                return _socket.LocalEndPoint;
            }
        }

        public EndPoint RemoteEndPoint
        {
            get
            {
                return _socket.RemoteEndPoint;
            }
        }

        public event EventHandler<TPackage> PackageReceived
        {
            add
            {
                lock (_packageReceivedLock)
                {
                    _packageReceived += value;
                }
            }
            remove
            {
                lock (_packageReceivedLock)
                {
                    _packageReceived -= value;
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

        public virtual void Connect(EndPoint remoteEndPoint)
        {
            if (remoteEndPoint == null)
            {
                throw new ArgumentNullException(nameof(remoteEndPoint));
            }

            if (_isDisposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            _socket.Connect(remoteEndPoint);
            _networkStream = new NetworkStream(_socket);
        }

        public virtual void Connect(IPAddress remoteAddress, int remotePort)
        {
            Connect(new IPEndPoint(remoteAddress, remotePort));
        }

        public virtual async Task ConnectAsync(EndPoint remoteEndPoint)
        {
            if (remoteEndPoint == null)
            {
                throw new ArgumentNullException(nameof(remoteEndPoint));
            }

            if (_isDisposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            await Task.Factory.FromAsync((ac, s) => _socket.BeginConnect(remoteEndPoint, ac, s),
                (ar) => _socket.EndConnect(ar), null);
            _networkStream = new NetworkStream(_socket);
        }

        public virtual async Task ConnectAsync(IPAddress remoteAddress, int remotePort)
        {
            await ConnectAsync(new IPEndPoint(remoteAddress, remotePort));
        }

        public void Start(CancellationToken cancellationToken, bool isBackground = false)
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            if (_isRunning)
            {
                throw new InvalidOperationException("You cannot start a client that is already receiving.");
            }

            _isRunning = true;
            _receiveThread = new Thread(() => RunBackgroundWork(cancellationToken))
            {
                IsBackground = isBackground
            };
            _receiveThread.Start();
        }

        public virtual void SendPackage(TPackage package)
        {
            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }

            if (_isDisposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            var packageBytes = LengthPrefixProtocol.WrapData(package.ToBytes());
            _networkStream.Write(packageBytes, 0, packageBytes.Length);
        }

        public virtual async Task SendPackageAsync(TPackage package)
        {
            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }

            if (_isDisposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            var packageBytes = LengthPrefixProtocol.WrapData(package.ToBytes());
            await _networkStream.WriteAsync(packageBytes, 0, packageBytes.Length);
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
                    var buffer = new byte[_bufferSize];
                    int bytesRead = _networkStream.Read(buffer, 0, buffer.Length);

                    if (bytesRead != 0)
                    {
                        _lengthPrefixProtocol.ChunkReceived(buffer, bytesRead);
                    }
                }
                catch (IOException ioException) when (ioException.InnerException is SocketException socketException)
                {
                    exception = socketException;
                    break;
                }
            }

            _isRunning = false;
            _stopped?.Invoke(this, exception);
        }

        private void OnDataReceived(object sender, byte[] data)
        {
            var package = new TPackage();
            package.Populate(data);
            _packageReceived?.Invoke(this, package);
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
                _networkStream?.Dispose();
                _socket?.Dispose();
            }

            _isDisposed = true;
        }

        #endregion

        #endregion
    }
}
