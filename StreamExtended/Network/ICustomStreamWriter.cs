using System.Threading;
using System.Threading.Tasks;

namespace StreamExtended.Network
{
    public interface ICustomStreamWriter
    {
        void Write(byte[] buffer, int i, int bufferLength);

        Task WriteAsync(byte[] buffer, int i, int bufferLength, CancellationToken cancellationToken);
    }
}