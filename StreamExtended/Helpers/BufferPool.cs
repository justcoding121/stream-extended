using System.Collections.Concurrent;

namespace StreamExtended.Helpers
{
    //TODO remote static
    public static class BufferPool
    {
        private static readonly ConcurrentStack<byte[]> buffers = new ConcurrentStack<byte[]>();

        /// <summary>
        /// Gets a buffer.
        /// </summary>
        /// <param name="bufferSize">Size of the buffer.</param>
        /// <returns></returns>
        public static byte[] GetBuffer(int bufferSize)
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
        public static void ReturnBuffer(byte[] buffer)
        {
            if (buffer != null)
            {
                buffers.Push(buffer);
            }
        }
    }
}
