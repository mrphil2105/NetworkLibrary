using System;
using System.Net;

namespace NetworkLibrary.Tcp
{
    public class LengthPrefixProtocol
    {
        #region Fields

        private int _maxDataSize;

        private byte[] _lengthBuffer;
        private byte[] _dataBuffer;
        private int _bytesReceived;

        private event EventHandler<byte[]> _dataReceived;
        private readonly object _dataReceivedLock;

        #endregion

        #region Constructors

        public LengthPrefixProtocol(int maxDataSize)
        {
            if (maxDataSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxDataSize), "Value must be greater than zero.");
            }

            _dataReceivedLock = new object();

            _maxDataSize = maxDataSize;
            _lengthBuffer = new byte[sizeof(int)];
        }

        #endregion

        #region Accessors

        public int MaxDataSize
        {
            get
            {
                return _maxDataSize;
            }
            set
            {
                if (value <= 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), "Value must be greater than zero.");
                }

                _maxDataSize = value;
            }
        }

        public event EventHandler<byte[]> DataReceived
        {
            add
            {
                lock (_dataReceivedLock)
                {
                    _dataReceived += value;
                }
            }
            remove
            {
                lock (_dataReceivedLock)
                {
                    _dataReceived -= value;
                }
            }
        }

        #endregion

        #region Methods

        #region Public

        public static byte[] WrapData(byte[] data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            var lengthPrefixBytes = BitConverter.GetBytes(data.Length);

            var wrappedBytes = new byte[lengthPrefixBytes.Length + data.Length];
            lengthPrefixBytes.CopyTo(wrappedBytes, 0);
            data.CopyTo(wrappedBytes, lengthPrefixBytes.Length);

            return wrappedBytes;
        }

        public static byte[] WrapKeepAliveMessage()
        {
            return BitConverter.GetBytes(0);
        }

        public void ChunkReceived(byte[] chunkBytes)
        {
            if (chunkBytes == null)
            {
                throw new ArgumentNullException(nameof(chunkBytes));
            }

            int i = 0;

            while (chunkBytes.Length > i)
            {
                int chunkBytesLeft = chunkBytes.Length - i;
                int bytesLeft = (_dataBuffer?.Length ?? _lengthBuffer.Length) - _bytesReceived;

                int bytesRead = Math.Min(bytesLeft, chunkBytesLeft);
                Array.Copy(chunkBytes, i, _dataBuffer ?? _lengthBuffer,
                    _bytesReceived, bytesRead);
                i += bytesRead;

                ReadCompleted(bytesRead);
            }
        }

        public void ChunkReceived(byte[] buffer, int bytesRead)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            if (bytesRead < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(bytesRead), "Value must be greater than or equal to zero.");
            }

            var chunkBytes = new byte[bytesRead];
            Array.Copy(buffer, chunkBytes, bytesRead);
            ChunkReceived(chunkBytes);
        }

        #endregion

        #region Private

        private void ReadCompleted(int bytesRead)
        {
            _bytesReceived += bytesRead;

            if (_dataBuffer == null)
            {
                if (_bytesReceived == sizeof(int))
                {
                    int length = BitConverter.ToInt32(_lengthBuffer, 0);

                    if (length < 0)
                    {
                        throw new ProtocolViolationException("Data length is less than zero.");
                    }

                    if (length > _maxDataSize)
                    {
                        throw new ProtocolViolationException(string.Format("Data length {0} is greater than the maximum data size {1}.", length, _maxDataSize));
                    }

                    if (length == 0)
                    {
                        _dataReceived?.Invoke(this, new byte[0]);
                    }
                    else
                    {
                        _dataBuffer = new byte[length];
                    }

                    _bytesReceived = 0;
                }
            }
            else
            {
                if (_bytesReceived == _dataBuffer.Length)
                {
                    _dataReceived?.Invoke(this, _dataBuffer);

                    _dataBuffer = null;
                    _bytesReceived = 0;
                }
            }
        }

        #endregion

        #endregion
    }
}
