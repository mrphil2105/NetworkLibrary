using System;
using System.Net;
using System.Threading;
using System.Net.Sockets;
using System.Threading.Tasks;
using NetworkLibrary.Interfaces;

namespace NetworkLibrary.Udp
{
    /// <summary>
    /// A class used to communicate with multiple <see cref="UdpClient{TPackage}"/>.
    /// </summary>
    /// <typeparam name="TPackage">The custom package to communicate with.</typeparam>
    public class UdpClient<TPackage> : IThreaded, IDisposable
        where TPackage : IPackage, new()
    {
        #region Fields

        private int _bufferSize;

        private Socket _socket;

        private Thread _receiveThread;
        private volatile bool _isRunning;

        private event EventHandler<PackageReceivedEventArgs> _packageReceived;
        private readonly object _packageReceivedLock;

        private event EventHandler<Exception> _stopped;
        private readonly object _stoppedLock;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="UdpClient{TPackage}"/> class on any ip address and port.
        /// </summary>
        public UdpClient() : this(IPAddress.Any, 0)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="UdpClient{TPackage}"/> class on the specified endpoint.
        /// </summary>
        /// <param name="localEndPoint">The local endpoint to bind to.</param>
        public UdpClient(EndPoint localEndPoint)
        {
            if (localEndPoint == null)
            {
                throw new ArgumentNullException(nameof(localEndPoint));
            }

            _packageReceivedLock = new object();
            _stoppedLock = new object();

            _bufferSize = 1024;

            _socket = new Socket(localEndPoint.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
            _socket.Bind(localEndPoint);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="UdpClient{TPackage}"/> class on the specified ip address and port.
        /// </summary>
        /// <param name="localAddress">The local ip address to bind to.</param>
        /// <param name="localPort">The local port to bind to.</param>
        public UdpClient(IPAddress localAddress, int localPort) : this(new IPEndPoint(localAddress, localPort))
        {
        }

        #endregion

        #region Accessors

        /// <summary>
        /// Indicates whether the <see cref="UdpClient{TPackage}"/> is receiving packages.
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
        /// The local endpoint that the <see cref="UdpClient{TPackage}"/> is bound to.
        /// </summary>
        public EndPoint LocalEndPoint
        {
            get
            {
                return _socket.LocalEndPoint;
            }
        }

        /// <summary>
        /// The internal socket used by the <see cref="UdpClient{TPackage}"/>.
        /// </summary>
        public Socket Socket
        {
            get
            {
                return _socket;
            }
        }

        /// <summary>
        /// An event that gets invoked when the <see cref="UdpClient{TPackage}"/> has received a package.
        /// </summary>
        public event EventHandler<PackageReceivedEventArgs> PackageReceived
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
        /// An event that gets invoked when the <see cref="UdpClient{TPackage}"/> has stopped receiving packages.
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
        /// Sends a package to a <see cref="UdpClient{TPackage}"/> on the specified endpoint.
        /// </summary>
        /// <param name="package">The package to send.</param>
        /// <param name="remoteEndPoint">The remote endpoint to send to.</param>
        public virtual void SendPackage(TPackage package, EndPoint remoteEndPoint)
        {
            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }

            if (remoteEndPoint == null)
            {
                throw new ArgumentNullException(nameof(remoteEndPoint));
            }

            if (_isDisposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            var packageBytes = package.ToBytes();
            _socket.SendTo(packageBytes, 0, packageBytes.Length, SocketFlags.None, remoteEndPoint);
        }

        /// <summary>
        /// Sends a package to a <see cref="UdpClient{TPackage}"/> on the specified ip address and port.
        /// </summary>
        /// <param name="package">The package to send.</param>
        /// <param name="remoteAddress">The remote ip address to send to.</param>
        /// <param name="remotePort">The remote port to send to.</param>
        public virtual void SendPackage(TPackage package, IPAddress remoteAddress, int remotePort)
        {
            SendPackage(package, new IPEndPoint(remoteAddress, remotePort));
        }

        /// <summary>
        /// Asynchronously sends a package to a <see cref="UdpClient{TPackage}"/> on the specified endpoint.
        /// </summary>
        /// <param name="package">The package to send.</param>
        /// <param name="remoteEndPoint">The remote endpoint to send to.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public virtual async Task SendPackageAsync(TPackage package, EndPoint remoteEndPoint)
        {
            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }

            if (remoteEndPoint == null)
            {
                throw new ArgumentNullException(nameof(remoteEndPoint));
            }

            if (_isDisposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            var packageBytes = package.ToBytes();
            await Task.Factory.FromAsync((ac, s) => _socket.BeginSendTo(packageBytes, 0, packageBytes.Length,
                SocketFlags.None, remoteEndPoint, ac, s), (ar) => _socket.EndSend(ar), null);
        }

        /// <summary>
        /// Asynchronously sends a package to a <see cref="UdpClient{TPackage}"/> on the specified ip address and port.
        /// </summary>
        /// <param name="package">The package to send.</param>
        /// <param name="remoteAddress">The remote ip address to send to.</param>
        /// <param name="remotePort">The remote port to send to.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public virtual async Task SendPackageAsync(TPackage package, IPAddress remoteAddress, int remotePort)
        {
            await SendPackageAsync(package, new IPEndPoint(remoteAddress, remotePort));
        }

        /// <summary>
        /// Releases all resources used by the <see cref="UdpClient{TPackage}"/>.
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
                    var remoteEndPoint = (EndPoint)new IPEndPoint(IPAddress.Any, 0);
                    var buffer = new byte[_bufferSize];
                    int bytesRead = _socket.ReceiveFrom(buffer, 0, buffer.Length, SocketFlags.None, ref remoteEndPoint);

                    if (bytesRead != 0)
                    {
                        var package = new TPackage();
                        package.Populate(buffer, bytesRead);
                        _packageReceived?.Invoke(this, new PackageReceivedEventArgs(package, remoteEndPoint));
                    }
                }
                catch (SocketException socketException)
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
        /// Releases all resources used by the <see cref="UdpClient{TPackage}"/>.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases all or only unmanaged resources used by the <see cref="UdpClient{TPackage}"/>.
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
                _socket?.Dispose();
            }

            _isDisposed = true;
        }

        #endregion

        #endregion

        #region Nested Types

        /// <summary>
        /// An event argument class used when a package has been received.
        /// </summary>
        public class PackageReceivedEventArgs : EventArgs
        {
            #region Fields

            private TPackage _package;
            private EndPoint _remoteEndPoint;

            #endregion

            #region Constructors

            /// <summary>
            /// Initializes a new instance of the <see cref="PackageReceivedEventArgs"/> class.
            /// </summary>
            /// <param name="package">The package that has been received.</param>
            /// <param name="remoteEndPoint">The remote endpoint the package came from.</param>
            public PackageReceivedEventArgs(TPackage package, EndPoint remoteEndPoint)
            {
                if (package == null)
                {
                    throw new ArgumentNullException(nameof(package));
                }

                if (remoteEndPoint == null)
                {
                    throw new ArgumentNullException(nameof(remoteEndPoint));
                }

                _package = package;
                _remoteEndPoint = remoteEndPoint;
            }

            #endregion

            #region Accessors

            /// <summary>
            /// The package that has been received.
            /// </summary>
            public TPackage Package
            {
                get
                {
                    return _package;
                }
            }

            /// <summary>
            /// The remote endpoint the package came from.
            /// </summary>
            public EndPoint RemoteEndPoint
            {
                get
                {
                    return _remoteEndPoint;
                }
            }

            #endregion
        }

        #endregion
    }
}
