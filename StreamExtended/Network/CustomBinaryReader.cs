using StreamExtended.Helpers;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace StreamExtended.Network
{
    /// <summary>
    /// A custom binary reader that would allo us to read string line by line
    /// using the specified encoding
    /// as well as to read bytes as required
    /// </summary>
    public class CustomBinaryReader : IDisposable
    {
        private readonly IBufferedStream stream;
        private readonly Encoding encoding;

        private bool disposed;

        public byte[] Buffer { get; }

        public CustomBinaryReader(IBufferedStream stream, int bufferSize)
        {
            this.stream = stream;
            Buffer = BufferPool.GetBuffer(bufferSize);

            //default to UTF-8
            encoding = Encoding.UTF8;
        }

        /// <summary>
        /// Read a line from the byte stream
        /// </summary>
        /// <returns></returns>
        public async Task<string> ReadLineAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            byte lastChar = default(byte);

            int bufferDataLength = 0;

            // try to use the thread static buffer, usually it is enough
            var buffer = Buffer;

            while (stream.DataAvailable || await stream.FillBufferAsync(cancellationToken))
            {
                byte newChar = stream.ReadByteFromBuffer();
                buffer[bufferDataLength] = newChar;

                //if new line
                if (newChar == '\n')
                {
                    if (lastChar == '\r')
                    {
                        return encoding.GetString(buffer, 0, bufferDataLength - 1);
                    }

                    return encoding.GetString(buffer, 0, bufferDataLength);
                }

                //end of stream
                if (newChar == '\0')
                {
                    return encoding.GetString(buffer, 0, bufferDataLength);
                }

                bufferDataLength++;

                //store last char for new line comparison
                lastChar = newChar;

                if (bufferDataLength == buffer.Length)
                {
                    ResizeBuffer(ref buffer, bufferDataLength * 2);
                }
            }

            if (bufferDataLength == 0)
            {
                return null;
            }

            return encoding.GetString(buffer, 0, bufferDataLength);
        }

        /// <summary>
        /// Read until the last new line
        /// </summary>
        /// <returns></returns>
        public async Task<List<string>> ReadAllLinesAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            string tmpLine;
            var requestLines = new List<string>();
            while (!string.IsNullOrEmpty(tmpLine = await ReadLineAsync(cancellationToken)))
            {
                requestLines.Add(tmpLine);
            }

            return requestLines;
        }

        /// <summary>
        /// Read until the last new line, ignores the result
        /// </summary>
        /// <returns></returns>
        public async Task ReadAndIgnoreAllLinesAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            while (!string.IsNullOrEmpty(await ReadLineAsync(cancellationToken)))
            {
            }
        }

        /// <summary>
        /// Read the specified number (or less) of raw bytes from the base stream to the given buffer
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="bytesToRead"></param>
        /// <param name="cancellationToken"></param>
        /// <returns>The number of bytes read</returns>
        public Task<int> ReadBytesAsync(byte[] buffer, int bytesToRead, CancellationToken cancellationToken = default(CancellationToken))
        {
            return stream.ReadAsync(buffer, 0, bytesToRead, cancellationToken);
        }

        /// <summary>
        /// Read the specified number (or less) of raw bytes from the base stream to the given buffer to the specified offset
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="bytesToRead"></param>
        /// <param name="cancellationToken"></param>
        /// <returns>The number of bytes read</returns>
        public Task<int> ReadBytesAsync(byte[] buffer, int offset, int bytesToRead, CancellationToken cancellationToken = default(CancellationToken))
        {
            return stream.ReadAsync(buffer, offset, bytesToRead, cancellationToken);
        }

        public bool DataAvailable => stream.DataAvailable;

        /// <summary>
        /// Fills the buffer asynchronous.
        /// </summary>
        /// <returns></returns>
        public Task<bool> FillBufferAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            return stream.FillBufferAsync(cancellationToken);
        }

        public byte ReadByteFromBuffer()
        {
            return stream.ReadByteFromBuffer();
        }

        public void Dispose()
        {
            if (!disposed)
            {
                disposed = true;
                BufferPool.ReturnBuffer(Buffer);
            }
        }

        /// <summary>
        /// Increase size of buffer and copy existing content to new buffer
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="size"></param>
        private void ResizeBuffer(ref byte[] buffer, long size)
        {
            var newBuffer = new byte[size];
            System.Buffer.BlockCopy(buffer, 0, newBuffer, 0, buffer.Length);
            buffer = newBuffer;
        }
    }
}
