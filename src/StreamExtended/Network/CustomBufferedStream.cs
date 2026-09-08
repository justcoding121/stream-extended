using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using StreamExtended.BufferPool;

namespace StreamExtended.Network
{
    /// <summary>
    ///     A custom network stream inherited from stream
    ///     with an underlying read buffer supporting both read/write
    ///     of UTF-8 encoded string or raw bytes asynchronously from last read position.
    /// </summary>
    /// <seealso cref="System.IO.Stream" />
    public class CustomBufferedStream : Stream, ICustomStreamReader, IPeekStream
    {
        private readonly Stream baseStream;
        private readonly bool leaveOpen;
        private byte[] streamBuffer;

        // default to UTF-8
        private static readonly Encoding encoding = Encoding.UTF8;

        private int bufferLength;

        private int bufferPos;

        private bool disposed;

        private bool closed;

        private readonly IBufferPool bufferPool;

        public int BufferSize { get; }

        public event EventHandler<DataEventArgs>? DataRead;

        public event EventHandler<DataEventArgs>? DataWrite;

        public bool IsClosed => closed;

        /// <summary>
        /// Initializes a new instance of the <see cref="CustomBufferedStream"/> class.
        /// </summary>
        /// <param name="baseStream">The base stream.</param>
        /// <param name="bufferPool">Bufferpool.</param>
        /// <param name="bufferSize">Size of the buffer.</param>
        /// <param name="leaveOpen"><see langword="true" /> to leave the stream open after disposing the <see cref="T:CustomBufferedStream" /> object; otherwise, <see langword="false" />.</param>
        public CustomBufferedStream(Stream baseStream, IBufferPool bufferPool, int bufferSize, bool leaveOpen = false)
        {
            this.baseStream = baseStream;
            BufferSize = bufferSize;
            this.leaveOpen = leaveOpen;
            streamBuffer = bufferPool.GetBuffer(bufferSize);
            this.bufferPool = bufferPool;
        }

        /// <inheritdoc />
        public override void Flush()
        {
            baseStream.Flush();
        }

        /// <inheritdoc />
        public override long Seek(long offset, SeekOrigin origin)
        {
            bufferLength = 0;
            bufferPos = 0;
            return baseStream.Seek(offset, origin);
        }

        /// <inheritdoc />
        public override void SetLength(long value)
        {
            baseStream.SetLength(value);
        }

        /// <inheritdoc />
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (bufferLength == 0)
            {
                FillBuffer();
            }

            int available = Math.Min(bufferLength, count);
            if (available > 0)
            {
                Buffer.BlockCopy(streamBuffer, bufferPos, buffer, offset, available);
                bufferPos += available;
                bufferLength -= available;
            }

            return available;
        }

        /// <inheritdoc />
        [DebuggerStepThrough]
        public override void Write(byte[] buffer, int offset, int count)
        {
            OnDataWrite(buffer, offset, count);
            baseStream.Write(buffer, offset, count);
        }

        /// <inheritdoc />
        public override async Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
        {
            if (bufferLength > 0)
            {
                await destination.WriteAsync(streamBuffer.AsMemory(bufferPos, bufferLength), cancellationToken);

                bufferLength = 0;
            }

            await base.CopyToAsync(destination, bufferSize, cancellationToken);
        }

        /// <inheritdoc />
        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            return baseStream.FlushAsync(cancellationToken);
        }

        /// <inheritdoc />
        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return await ReadAsync(buffer.AsMemory(offset, count), cancellationToken);
        }

        /// <inheritdoc />
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (bufferLength == 0)
            {
                await FillBufferAsync(cancellationToken);
            }

            int available = Math.Min(bufferLength, buffer.Length);
            if (available > 0)
            {
                streamBuffer.AsSpan(bufferPos, available).CopyTo(buffer.Span);
                bufferPos += available;
                bufferLength -= available;
            }

            return available;
        }

        /// <inheritdoc />
        public override int ReadByte()
        {
            if (bufferLength == 0)
            {
                FillBuffer();
            }

            if (bufferLength == 0)
            {
                return -1;
            }

            bufferLength--;
            return streamBuffer[bufferPos++];
        }

        /// <summary>
        /// Peeks a byte asynchronous.
        /// </summary>
        public async Task<int> PeekByteAsync(int index, CancellationToken cancellationToken = default)
        {
            while (Available <= index)
            {
                if (!await FillBufferAsync(cancellationToken))
                {
                    break;
                }
            }

            if (streamBuffer.Length <= index)
            {
                throw new ArgumentOutOfRangeException(nameof(index),
                    "Requested Peek index exceeds the buffer size. Consider increasing the buffer size.");
            }

            if (Available <= index)
            {
                return -1;
            }

            return streamBuffer[bufferPos + index];
        }

        async ValueTask<int> IPeekStream.PeekByteAsync(int index, CancellationToken cancellationToken)
        {
            return await PeekByteAsync(index, cancellationToken);
        }

        /// <summary>
        /// Peeks bytes asynchronous.
        /// </summary>
        public async Task<byte[]?> PeekBytesAsync(int index, int size, CancellationToken cancellationToken = default)
        {
            while (Available <= index + size)
            {
                if (!await FillBufferAsync(cancellationToken))
                {
                    break;
                }
            }

            if (streamBuffer.Length <= (index + size))
            {
                throw new ArgumentOutOfRangeException(nameof(size),
                    "Requested Peek index and size exceeds the buffer size. Consider increasing the buffer size.");
            }

            if (Available <= (index + size))
            {
                return null;
            }

            var vRet = new byte[size];
            Array.Copy(streamBuffer, bufferPos + index, vRet, 0, size);
            return vRet;
        }

        /// <summary>
        ///     Peeks bytes into the supplied buffer (IPeekStream).
        /// </summary>
        public async ValueTask<int> PeekBytesAsync(byte[] buffer, int offset, int index, int count,
            CancellationToken cancellationToken = default)
        {
            while (Available <= index)
            {
                if (!await FillBufferAsync(cancellationToken))
                {
                    break;
                }
            }

            if (streamBuffer.Length <= index + count)
            {
                throw new ArgumentOutOfRangeException(nameof(count),
                    "Requested Peek index and size exceeds the buffer size. Consider increasing the buffer size.");
            }

            if (Available <= index)
            {
                return 0;
            }

            var toCopy = Math.Min(count, Available - index);
            Buffer.BlockCopy(streamBuffer, bufferPos + index, buffer, offset, toCopy);
            return toCopy;
        }

        /// <summary>
        /// Peeks a byte from buffer.
        /// </summary>
        public byte PeekByteFromBuffer(int index)
        {
            if (bufferLength <= index)
            {
                throw new ArgumentOutOfRangeException(nameof(index), "Index is out of buffer size");
            }

            return streamBuffer[bufferPos + index];
        }

        /// <summary>
        /// Reads a byte from buffer.
        /// </summary>
        public byte ReadByteFromBuffer()
        {
            if (bufferLength == 0)
            {
                throw new InvalidOperationException("Buffer is empty");
            }

            bufferLength--;
            return streamBuffer[bufferPos++];
        }

        /// <inheritdoc />
        [DebuggerStepThrough]
        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            await WriteAsync(buffer.AsMemory(offset, count), cancellationToken);
        }

        /// <inheritdoc />
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (!buffer.IsEmpty)
            {
                // Events still expose array-based buffers for compatibility.
                if (System.Runtime.InteropServices.MemoryMarshal.TryGetArray(buffer, out ArraySegment<byte> segment)
                    && segment.Array != null)
                {
                    OnDataWrite(segment.Array, segment.Offset, segment.Count);
                }
                else
                {
                    var rented = buffer.ToArray();
                    OnDataWrite(rented, 0, rented.Length);
                }
            }

            await baseStream.WriteAsync(buffer, cancellationToken);
        }

        /// <inheritdoc />
        public override void WriteByte(byte value)
        {
            var buffer = bufferPool.GetBuffer(BufferSize);
            try
            {
                buffer[0] = value;
                OnDataWrite(buffer, 0, 1);
                baseStream.Write(buffer, 0, 1);
            }
            finally
            {
                bufferPool.ReturnBuffer(buffer);
            }
        }

        protected virtual void OnDataWrite(byte[] buffer, int offset, int count)
        {
            DataWrite?.Invoke(this, new DataEventArgs(buffer, offset, count));
        }

        protected virtual void OnDataRead(byte[] buffer, int offset, int count)
        {
            DataRead?.Invoke(this, new DataEventArgs(buffer, offset, count));
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            if (!disposed)
            {
                disposed = true;
                closed = true;
                if (disposing)
                {
                    if (!leaveOpen)
                    {
                        baseStream.Dispose();
                    }

                    var buffer = streamBuffer;
                    streamBuffer = null!;
                    bufferPool.ReturnBuffer(buffer);
                }
            }

            base.Dispose(disposing);
        }

        /// <inheritdoc />
        public override bool CanRead => baseStream.CanRead;

        /// <inheritdoc />
        public override bool CanSeek => baseStream.CanSeek;

        /// <inheritdoc />
        public override bool CanWrite => baseStream.CanWrite;

        /// <inheritdoc />
        public override bool CanTimeout => baseStream.CanTimeout;

        /// <inheritdoc />
        public override long Length => baseStream.Length;

        /// <summary>
        /// Gets a value indicating whether data is available.
        /// </summary>
        public bool DataAvailable => bufferLength > 0;

        /// <summary>
        /// Gets the available data size.
        /// </summary>
        public int Available => bufferLength;

        /// <inheritdoc />
        public override long Position
        {
            get => baseStream.Position;
            set => baseStream.Position = value;
        }

        /// <inheritdoc />
        public override int ReadTimeout
        {
            get => baseStream.ReadTimeout;
            set => baseStream.ReadTimeout = value;
        }

        /// <inheritdoc />
        public override int WriteTimeout
        {
            get => baseStream.WriteTimeout;
            set => baseStream.WriteTimeout = value;
        }

        /// <summary>
        /// Fills the buffer.
        /// </summary>
        public bool FillBuffer()
        {
            if (closed)
            {
                return false;
            }

            if (bufferLength > 0)
            {
                //normally we fill the buffer only when it is empty, but sometimes we need more data
                //move the remanining data to the beginning of the buffer
                Buffer.BlockCopy(streamBuffer, bufferPos, streamBuffer, 0, bufferLength);
            }

            bufferPos = 0;

            int readBytes = baseStream.Read(streamBuffer, bufferLength, streamBuffer.Length - bufferLength);
            bool result = readBytes > 0;
            if (result)
            {
                OnDataRead(streamBuffer, bufferLength, readBytes);
                bufferLength += readBytes;
            }
            else
            {
                closed = true;
            }

            return result;
        }

        /// <summary>
        /// Fills the buffer asynchronous.
        /// </summary>
        public async Task<bool> FillBufferAsync(CancellationToken cancellationToken = default)
        {
            if (closed)
            {
                return false;
            }

            if (bufferLength > 0)
            {
                //normally we fill the buffer only when it is empty, but sometimes we need more data
                //move the remanining data to the beginning of the buffer
                Buffer.BlockCopy(streamBuffer, bufferPos, streamBuffer, 0, bufferLength);
            }

            int bytesToRead = streamBuffer.Length - bufferLength;
            if (bytesToRead == 0)
            {
                return false;
            }

            bufferPos = 0;

            int readBytes = await baseStream.ReadAsync(streamBuffer.AsMemory(bufferLength, bytesToRead), cancellationToken);
            bool result = readBytes > 0;
            if (result)
            {
                OnDataRead(streamBuffer, bufferLength, readBytes);
                bufferLength += readBytes;
            }
            else
            {
                closed = true;
            }

            return result;
        }

        /// <summary>
        /// Read a line from the byte stream
        /// </summary>
        public Task<string?> ReadLineAsync(CancellationToken cancellationToken = default)
        {
            return ReadLineInternalAsync(this, bufferPool, cancellationToken);
        }

        /// <summary>
        /// Read a line from the byte stream
        /// </summary>
        internal static async Task<string?> ReadLineInternalAsync(ICustomStreamReader reader, IBufferPool bufferPool, CancellationToken cancellationToken = default)
        {
            byte lastChar = default;

            int bufferDataLength = 0;

            // try to use buffer from the buffer pool, usually it is enough
            var bufferPoolBuffer = bufferPool.GetBuffer(reader.BufferSize);
            var buffer = bufferPoolBuffer;

            try
            {
                while (reader.DataAvailable || await reader.FillBufferAsync(cancellationToken))
                {
                    byte newChar = reader.ReadByteFromBuffer();
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

                    bufferDataLength++;

                    //store last char for new line comparison
                    lastChar = newChar;

                    if (bufferDataLength == buffer.Length)
                    {
                        ResizeBuffer(ref buffer, bufferDataLength * 2);
                    }
                }
            }
            finally
            {
                bufferPool.ReturnBuffer(bufferPoolBuffer);
            }

            if (bufferDataLength == 0)
            {
                return null;
            }

            return encoding.GetString(buffer, 0, bufferDataLength);
        }

        /// <summary>
        /// Read until the last new line, ignores the result
        /// </summary>
        public async Task ReadAndIgnoreAllLinesAsync(CancellationToken cancellationToken = default)
        {
            while (true)
            {
                var line = await ReadLineAsync(cancellationToken);
                if (string.IsNullOrEmpty(line))
                {
                    break;
                }
            }
        }

        /// <summary>
        /// Increase size of buffer and copy existing content to new buffer
        /// </summary>
        private static void ResizeBuffer(ref byte[] buffer, long size)
        {
            var newBuffer = new byte[size];
            Buffer.BlockCopy(buffer, 0, newBuffer, 0, buffer.Length);
            buffer = newBuffer;
        }
    }
}
