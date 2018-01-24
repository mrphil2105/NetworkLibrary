namespace NetworkLibrary.Interfaces
{
    public interface IPackage
    {
        #region Methods

        byte[] ToBytes();

        void Populate(byte[] packageBytes);

        void Populate(byte[] buffer, int bytesRead);

        #endregion
    }
}
