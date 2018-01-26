namespace NetworkLibrary.Interfaces
{
    /// <summary>
    /// An interface that implements packages.
    /// </summary>
    public interface IPackage
    {
        #region Methods

        /// <summary>
        /// Converts the package to bytes.
        /// </summary>
        /// <returns>The package bytes.</returns>
        byte[] ToBytes();

        /// <summary>
        /// Converts the bytes back to a package.
        /// </summary>
        /// <param name="packageBytes">The package bytes.</param>
        void Populate(byte[] packageBytes);

        /// <summary>
        /// Converts the bytes back to a package.
        /// </summary>
        /// <param name="buffer">The buffer containing the package bytes.</param>
        /// <param name="bytesRead">The length of the packages bytes.</param>
        void Populate(byte[] buffer, int bytesRead);

        #endregion
    }
}
