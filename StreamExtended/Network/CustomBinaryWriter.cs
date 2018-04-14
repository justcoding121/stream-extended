using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace StreamExtended.Network
{
    public abstract class CustomBinaryWriter
    {
        private readonly Stream stream;

        protected CustomBinaryWriter(Stream stream)
        {
            this.stream = stream;
        }

        public Task WriteAsync(byte[] data, int offset, int count, CancellationToken cancellationToken = default(CancellationToken))
        {
            return stream.WriteAsync(data, offset, count, cancellationToken);
        }

        protected Task FlushAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            return stream.FlushAsync(cancellationToken);
        }
    }
}
