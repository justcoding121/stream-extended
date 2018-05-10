using System.Collections.Concurrent;

namespace StreamExtended
{
    /// <summary>
    ///     Use this interface to implement custom buffer pool.
    ///     To use the default buffer pool implementation use DefaultBufferPool class.
    /// </summary>
    public interface IBufferPool
    {
        byte[] GetBuffer(int bufferSize);
        void ReturnBuffer(byte[] buffer);
    }


    /// <summary>
    ///     A concrete IBufferPool implementation using concurrent stack.
    ///     Works well for fixed buffer sizes, where if size does change then it would be global for all applications using this pool.
    ///     If your application would use variable size buffers consider implementing IBufferPool using System.Buffers from Microsoft.
    /// </summary>
    public class DefaultBufferPool : IBufferPool
    {
        private readonly ConcurrentStack<byte[]> buffers = new ConcurrentStack<byte[]>();

        /// <summary>
        /// Gets a buffer.
        /// </summary>
        /// <param name="bufferSize">Size of the buffer.</param>
        /// <returns></returns>
        public byte[] GetBuffer(int bufferSize)
        {
            if (!buffers.TryPop(out var buffer) || buffer.Length != bufferSize)
            {
                buffer = new byte[bufferSize];
            }

            return buffer;
        }

        /// <summary>
        /// Returns the buffer.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        public void ReturnBuffer(byte[] buffer)
        {
            if (buffer != null)
            {
                buffers.Push(buffer);
            }
        }
    }
}
