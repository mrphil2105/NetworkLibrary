using System;
using System.Threading;

namespace NetworkLibrary.Interfaces
{
    public interface IThreaded
    {
        #region Accessors

        bool IsRunning
        {
            get;
        }

        event EventHandler<Exception> Stopped;

        #endregion

        #region Methods

        void Start(CancellationToken cancellationToken, bool isBackground);

        #endregion
    }
}
