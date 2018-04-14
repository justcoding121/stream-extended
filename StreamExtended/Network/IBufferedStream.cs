using System.Threading;
using System.Threading.Tasks;

namespace StreamExtended.Network
{
    public interface IBufferedStream
    {
        bool DataAvailable { get; }

        Task<bool> FillBufferAsync(CancellationToken cancellationToken);

        byte ReadByteFromBuffer();

        Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken);
    }
}