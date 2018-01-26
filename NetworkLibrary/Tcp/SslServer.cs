using System;
using System.Net;
using System.Threading;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Security.Cryptography.X509Certificates;
using NetworkLibrary.Interfaces;

namespace NetworkLibrary.Tcp
{
    /// <summary>
    /// A class used to accept and communicate with multiple <see cref="SslClient{TPackage}"/>.
    /// </summary>
    /// <typeparam name="TPackage">The custom package to communicate with.</typeparam>
    public class SslServer<TPackage> : IThreaded, IDisposable
        where TPackage : IPackage, new()
    {
        #region Fields

        private int _backlog;
        private int _bufferSize;
        private int _maxPackageSize;

        private Socket _socket;
        private X509Certificate _certificate;

        private Thread _serverThread;
        private volatile bool _isRunning;

        private event EventHandler<SslClient<TPackage>> _clientConnected;
        private readonly object _clientConnectedLock;

        private event EventHandler<Exception> _stopped;
        private readonly object _stoppedLock;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="SslServer{TPackage}"/> class on the specified endpoint, with the specified certificate.
        /// </summary>
        /// <param name="listeningEndPoint">The local endpoint to listen on.</param>
        /// <param name="certificate">The certificate to use.</param>
        public SslServer(EndPoint listeningEndPoint, X509Certificate certificate)
        {
            if (listeningEndPoint == null)
            {
                throw new ArgumentNullException(nameof(listeningEndPoint));
            }

            if (certificate == null)
            {
                throw new ArgumentNullException(nameof(certificate));
            }

            _clientConnectedLock = new object();
            _stoppedLock = new object();

            _backlog = 10;
            _bufferSize = 1024;
            _maxPackageSize = 1024 * 1024;

            _socket = new Socket(listeningEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            _socket.Bind(listeningEndPoint);
            _certificate = certificate;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SslServer{TPackage}"/> class on the specified ip address and port, with the specified certificate.
        /// </summary>
        /// <param name="listeningAddress">The local ip address to listen on.</param>
        /// <param name="listeningPort">The local port to listen on.</param>
        /// <param name="certificate">The certificate to use.</param>
        public SslServer(IPAddress listeningAddress, int listeningPort, X509Certificate certificate) :
            this(new IPEndPoint(listeningAddress, listeningPort), certificate)
        {
        }

        #endregion

        #region Accessors

        /// <summary>
        /// Indicates whether the <see cref="SslServer{TPackage}"/> is accepting clients.
        /// </summary>
        public bool IsRunning
        {
            get
            {
                return _isRunning;
            }
        }

        /// <summary>
        /// Gets or sets the backlog when listening.
        /// </summary>
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

        /// <summary>
        /// Gets or sets the buffer size on new clients.
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
        /// Gets or sets the maximum amount of bytes allowed on new clients.
        /// </summary>
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

        /// <summary>
        /// The local endpoint that the <see cref="SslServer{TPackage}"/> is listening on.
        /// </summary>
        public EndPoint LocalEndPoint
        {
            get
            {
                return _socket.LocalEndPoint;
            }
        }

        /// <summary>
        /// The internal socket used by the <see cref="SslServer{TPackage}"/>.
        /// </summary>
        public Socket Socket
        {
            get
            {
                return _socket;
            }
        }

        /// <summary>
        /// An event that gets invoked when a <see cref="SslClient{TPackage}"/> has connected.
        /// </summary>
        public event EventHandler<SslClient<TPackage>> ClientConnected
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

        /// <summary>
        /// An event that gets invoked when the <see cref="SslServer{TPackage}"/> has stopped accepting clients.
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
        /// Listens on the specified local endpoint.
        /// </summary>
        public void Listen()
        {
            _socket.Listen(_backlog);
        }

        /// <summary>
        /// Starts accepting clients in a new thread.
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
                throw new InvalidOperationException("You cannot start a server that is already accepting clients.");
            }

            _isRunning = true;
            _serverThread = new Thread(() => RunBackgroundWork(cancellationToken))
            {
                IsBackground = isBackground
            };
            _serverThread.Start();
        }

        /// <summary>
        /// Manually accepts the next incoming <see cref="SslClient{TPackage}"/>. This method should not be called if <see cref="IsRunning"/> is true.
        /// </summary>
        /// <returns>The newly connected <see cref="SslClient{TPackage}"/>.</returns>
        public virtual SslClient<TPackage> AcceptClient()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            var clientSocket = _socket.Accept();
            var sslClient = SslClient<TPackage>.Create(clientSocket, _certificate);
            sslClient.BufferSize = _bufferSize;
            sslClient.MaxPackageSize = _maxPackageSize;
            return sslClient;
        }

        /// <summary>
        /// Asynchronously and manually accepts the next incoming <see cref="SslClient{TPackage}"/>. This method should not be called if <see cref="IsRunning"/> is true.
        /// </summary>
        /// <returns>A task with the newly connected <see cref="SslClient{TPackage}"/>, that represents the asynchronous operation.</returns>
        public virtual async Task<SslClient<TPackage>> AcceptClientAsync()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            var clientSocket = await Task.Factory.FromAsync((ac, s) => _socket.BeginAccept(ac, s),
                (ar) => _socket.EndAccept(ar), null);
            var sslClient = await SslClient<TPackage>.CreateAsync(clientSocket, _certificate);
            sslClient.BufferSize = _bufferSize;
            sslClient.MaxPackageSize = _maxPackageSize;
            return sslClient;
        }

        /// <summary>
        /// Releases all resources used by the <see cref="SslServer{TPackage}"/>.
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
                    var sslClient = AcceptClient();
                    _clientConnected?.Invoke(this, sslClient);
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

        /// <summary>
        /// Releases all resources used by the <see cref="SslServer{TPackage}"/>.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases all or only unmanaged resources used by the <see cref="SslServer{TPackage}"/>.
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
                _certificate?.Dispose();
                _socket?.Dispose();
            }

            _isDisposed = true;
        }

        #endregion

        #endregion
    }
}
