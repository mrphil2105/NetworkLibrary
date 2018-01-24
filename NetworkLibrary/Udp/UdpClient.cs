using System;
using System.Net;
using System.Threading;
using System.Net.Sockets;
using System.Threading.Tasks;
using NetworkLibrary.Interfaces;

namespace NetworkLibrary.Udp
{
    public class UdpClient<TPackage> : IThreaded, IDisposable
        where TPackage : IPackage, new()
    {
        #region Fields

        private int _bufferSize;

        private Socket _socket;

        private Thread _receiveThread;
        private volatile bool _isRunning;

        private event EventHandler<TPackage> _packageReceived;
        private readonly object _packageReceivedLock;

        private event EventHandler<Exception> _stopped;
        private readonly object _stoppedLock;

        #endregion

        #region Constructors

        public UdpClient() : this(IPAddress.Any, 0)
        {
        }

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

        public UdpClient(IPAddress localAddress, int localPort) : this(new IPEndPoint(localAddress, localPort))
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

        public EndPoint LocalEndPoint
        {
            get
            {
                return _socket.LocalEndPoint;
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

        public virtual void SendPackage(TPackage package, IPAddress remoteAddress, int remotePort)
        {
            SendPackage(package, new IPEndPoint(remoteAddress, remotePort));
        }

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

        public virtual async Task SendPackageAsync(TPackage package, IPAddress remoteAddress, int remotePort)
        {
            await SendPackageAsync(package, new IPEndPoint(remoteAddress, remotePort));
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
                    int bytesRead = _socket.Receive(buffer, 0, buffer.Length, SocketFlags.None);

                    if (bytesRead != 0)
                    {
                        var package = new TPackage();
                        package.Populate(buffer, bytesRead);
                        _packageReceived?.Invoke(this, package);
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
