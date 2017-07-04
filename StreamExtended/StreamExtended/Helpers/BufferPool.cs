using System.Collections.Concurrent;

namespace StreamExtended.Helpers
{
    public static class BufferPool
    {
        private static readonly ConcurrentQueue<byte[]> buffers = new ConcurrentQueue<byte[]>();

        public static byte[] GetBuffer(int bufferSize)
        {
            byte[] buffer;
            if (!buffers.TryDequeue(out buffer) || buffer.Length != bufferSize)
            {
                buffer = new byte[bufferSize];
            }

            return buffer;
        }

        public static void ReturnBuffer(byte[] buffer)
        {
            if (buffer != null)
            {
                buffers.Enqueue(buffer);
            }
        }
    }
}
