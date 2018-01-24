using System;
using System.Net;
using System.Threading.Tasks;

namespace NetworkLibrary.Interfaces
{
    public interface ITcpClient<TPackage> : IThreaded
        where TPackage : IPackage, new()
    {
        #region Accessors

        int BufferSize
        {
            get;
            set;
        }

        int MaxPackageSize
        {
            get;
            set;
        }

        EndPoint LocalEndPoint
        {
            get;
        }

        EndPoint RemoteEndPoint
        {
            get;
        }

        event EventHandler<TPackage> PackageReceived;

        #endregion

        #region Methods

        void Connect(EndPoint remoteEndPoint);

        void Connect(IPAddress remoteAddress, int remotePort);

        Task ConnectAsync(EndPoint remoteEndPoint);

        Task ConnectAsync(IPAddress remoteAddress, int remotePort);

        void SendPackage(TPackage package);

        Task SendPackageAsync(TPackage package);

        void Close();

        #endregion
    }
}
