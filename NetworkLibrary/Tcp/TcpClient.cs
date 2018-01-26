using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Net.Sockets;
using System.Threading.Tasks;
using NetworkLibrary.Interfaces;

namespace NetworkLibrary.Tcp
{
    /// <summary>
    /// A class used to communicate with a <see cref="TcpServer{TPackage}"/>.
    /// </summary>
    /// <typeparam name="TPackage">The custom package to communicate with.</typeparam>
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

        /// <summary>
        /// Initializes a new instance of the <see cref="TcpClient{TPackage}"/> class on any ip address and port.
        /// </summary>
        public TcpClient() : this(IPAddress.Any, 0)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TcpClient{TPackage}"/> class on the specified endpoint.
        /// </summary>
        /// <param name="localEndPoint">The local endpoint to bind to.</param>
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

        /// <summary>
        /// Initializes a new instance of the <see cref="TcpClient{TPackage}"/> class on the specified ip address and port.
        /// </summary>
        /// <param name="localAddress">The local ip address to bind to.</param>
        /// <param name="localPort">The local port to bind to.</param>
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

        /// <summary>
        /// Indicates whether the <see cref="TcpClient{TPackage}"/> is receiving packages.
        /// </summary>
        public bool IsRunning
        {
            get
            {
                return _isRunning;
            }
        }

        /// <summary>
        /// Gets or sets the buffer size when receiving packages.
        /// </summary>
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

        /// <summary>
        /// Gets or sets the maximum amount of bytes allowed when receiving packages.
        /// </summary>
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

        /// <summary>
        /// The local endpoint that the <see cref="TcpClient{TPackage}"/> is bound to.
        /// </summary>
        public EndPoint LocalEndPoint
        {
            get
            {
                return _socket.LocalEndPoint;
            }
        }

        /// <summary>
        /// The remote endpoint if the <see cref="TcpClient{TPackage}"/> is connected to a <see cref="TcpServer{TPackage}"/>.
        /// </summary>
        public EndPoint RemoteEndPoint
        {
            get
            {
                return _socket.RemoteEndPoint;
            }
        }

        /// <summary>
        /// The internal socket used by the <see cref="TcpClient{TPackage}"/>.
        /// </summary>
        public Socket Socket
        {
            get
            {
                return _socket;
            }
        }

        /// <summary>
        /// An event that gets invoked when the <see cref="TcpClient{TPackage}"/> has received a package.
        /// </summary>
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

        /// <summary>
        /// An event that gets invoked when the <see cref="TcpClient{TPackage}"/> has stopped receiving packages.
        /// </summary>
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

        /// <summary>
        /// Connects to a <see cref="TcpServer{TPackage}"/> with the specified endpoint.
        /// </summary>
        /// <param name="remoteEndPoint">The remote endpoint to connect to.</param>
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

        /// <summary>
        /// Connects to a <see cref="TcpServer{TPackage}"/> with the specified ip address and port.
        /// </summary>
        /// <param name="remoteAddress">The remote ip address to connect to.</param>
        /// <param name="remotePort">The remote port to connect to.</param>
        public virtual void Connect(IPAddress remoteAddress, int remotePort)
        {
            Connect(new IPEndPoint(remoteAddress, remotePort));
        }

        /// <summary>
        /// Asynchronously connects to a <see cref="TcpServer{TPackage}"/> with the specified endpoint.
        /// </summary>
        /// <param name="remoteEndPoint">The remote endpoint to connect to.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
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

        /// <summary>
        /// Asynchronously connects to a <see cref="TcpServer{TPackage}"/> with the specified ip address and port.
        /// </summary>
        /// <param name="remoteAddress">The remote ip address to connect to.</param>
        /// <param name="remotePort">The remote port to connect to.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public virtual async Task ConnectAsync(IPAddress remoteAddress, int remotePort)
        {
            await ConnectAsync(new IPEndPoint(remoteAddress, remotePort));
        }

        /// <summary>
        /// Starts receiving packages in a new thread.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token used to cancel the operation.</param>
        /// <param name="isBackground">Set this to true to mark the thread as a background thread.</param>
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

        /// <summary>
        /// Sends a package to the <see cref="TcpServer{TPackage}"/>.
        /// </summary>
        /// <param name="package">The package to send.</param>
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

        /// <summary>
        /// Asynchronously sends a package to the <see cref="TcpServer{TPackage}"/>.
        /// </summary>
        /// <param name="package">The package to send.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
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

        /// <summary>
        /// Releases all resources used by the <see cref="TcpClient{TPackage}"/>.
        /// </summary>
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

        /// <summary>
        /// Releases all resources used by the <see cref="TcpClient{TPackage}"/>.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases all or only unmanaged resources used by the <see cref="TcpClient{TPackage}"/>.
        /// </summary>
        /// <param name="isDisposing">Set this to true to also release managed resources.</param>
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
