using System;
using System.Threading;

namespace NetworkLibrary.Interfaces
{
    /// <summary>
    /// An interface that implements basic multithreading with cancellation.
    /// </summary>
    public interface IThreaded
    {
        #region Accessors

        /// <summary>
        /// Indicates whether the thread is running.
        /// </summary>
        bool IsRunning
        {
            get;
        }

        /// <summary>
        /// An event that gets invoked when the thread has stopped.
        /// </summary>
        event EventHandler<Exception> Stopped;

        #endregion

        #region Methods

        /// <summary>
        /// Starts a new thread.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token used to cancel the operation.</param>
        /// <param name="isBackground">Set this to true to mark the thread as a background thread.</param>
        void Start(CancellationToken cancellationToken, bool isBackground);

        #endregion
    }
}
