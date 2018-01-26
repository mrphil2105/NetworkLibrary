using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace NetworkLibrary.Interfaces
{
    /// <summary>
    /// An interface that implements clients that communicate via TCP.
    /// </summary>
    /// <typeparam name="TPackage">The custom package to communicate with.</typeparam>
    public interface ITcpClient<TPackage> : IThreaded
        where TPackage : IPackage, new()
    {
        #region Accessors

        /// <summary>
        /// Gets or sets the buffer size when receiving packages.
        /// </summary>
        int BufferSize
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the maximum amount of bytes allowed when receiving packages.
        /// </summary>
        int MaxPackageSize
        {
            get;
            set;
        }

        /// <summary>
        /// The local endpoint that the <see cref="ITcpClient{TPackage}"/> is bound to.
        /// </summary>
        EndPoint LocalEndPoint
        {
            get;
        }

        /// <summary>
        /// The remote endpoint if the <see cref="ITcpClient{TPackage}"/> is connected to a server.
        /// </summary>
        EndPoint RemoteEndPoint
        {
            get;
        }

        /// <summary>
        /// The internal socket used by the <see cref="ITcpClient{TPackage}"/>.
        /// </summary>
        Socket Socket
        {
            get;
        }

        /// <summary>
        /// An event that gets invoked when the <see cref="ITcpClient{TPackage}"/> has received a package.
        /// </summary>
        event EventHandler<TPackage> PackageReceived;

        #endregion

        #region Methods

        /// <summary>
        /// Connects to a server with the specified endpoint.
        /// </summary>
        /// <param name="remoteEndPoint">The remote endpoint to connect to.</param>
        void Connect(EndPoint remoteEndPoint);

        /// <summary>
        /// Connects to a server with the specified ip address and port.
        /// </summary>
        /// <param name="remoteAddress">The remote ip address to connect to.</param>
        /// <param name="remotePort">The remote port to connect to.</param>
        void Connect(IPAddress remoteAddress, int remotePort);

        /// <summary>
        /// Asynchronously connects to a server with the specified endpoint.
        /// </summary>
        /// <param name="remoteEndPoint">The remote endpoint to connect to.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task ConnectAsync(EndPoint remoteEndPoint);

        /// <summary>
        /// Asynchronously connects to a server with the specified ip address and port.
        /// </summary>
        /// <param name="remoteAddress">The remote ip address to connect to.</param>
        /// <param name="remotePort">The remote port to connect to.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task ConnectAsync(IPAddress remoteAddress, int remotePort);

        /// <summary>
        /// Sends a package to the server.
        /// </summary>
        /// <param name="package">The package to send.</param>
        void SendPackage(TPackage package);

        /// <summary>
        /// Asynchronously sends a package to the server.
        /// </summary>
        /// <param name="package">The package to send.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task SendPackageAsync(TPackage package);

        /// <summary>
        /// Releases all resources used by the <see cref="ITcpClient{TPackage}"/>.
        /// </summary>
        void Close();

        #endregion
    }
}
