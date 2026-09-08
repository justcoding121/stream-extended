using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using StreamExtended.BufferPool;
using StreamExtended.Network;

namespace StreamExtended.Tests;

[TestClass]
public class CopyStreamAndBufferPoolTests
{
    private sealed class CapturingWriter : ICustomStreamWriter
    {
        public MemoryStream Sink { get; } = new();

        public void Write(byte[] buffer, int i, int bufferLength)
        {
            Sink.Write(buffer, i, bufferLength);
        }

        public Task WriteAsync(byte[] buffer, int i, int bufferLength, CancellationToken cancellationToken)
        {
            Sink.Write(buffer, i, bufferLength);
            return Task.CompletedTask;
        }
    }

    [TestMethod]
    public void DefaultBufferPool_GetBuffer_Return_AndDispose()
    {
        var pool = new DefaultBufferPool { BufferSize = 128 };
        var a = pool.GetBuffer();
        var b = pool.GetBuffer(64);
        Assert.IsTrue(a.Length >= 128);
        Assert.IsTrue(b.Length >= 64);
        pool.ReturnBuffer(a);
        pool.ReturnBuffer(b);
        pool.Dispose();
    }

    [TestMethod]
    public void DataEventArgs_ExposesBufferSlice()
    {
        var buf = new byte[] { 1, 2, 3, 4 };
        var args = new DataEventArgs(buf, 1, 2);
        Assert.AreSame(buf, args.Buffer);
        Assert.AreEqual(1, args.Offset);
        Assert.AreEqual(2, args.Count);
    }

    [TestMethod]
    public async Task CopyStream_ReadAsync_CopiesToWriter_AndTracksReadBytes()
    {
        var payload = Encoding.ASCII.GetBytes("hello-copy");
        await using var inner = new MemoryStream(payload);
        await using var reader = new CustomBufferedStream(inner, new DefaultBufferPool(), 256);
        var writer = new CapturingWriter();
        using var copy = new CopyStream(reader, writer, new DefaultBufferPool(), 256);

        var buffer = new byte[payload.Length];
        var read = await copy.ReadAsync(buffer, 0, buffer.Length);

        Assert.AreEqual(payload.Length, read);
        Assert.AreEqual(payload.Length, copy.ReadBytes);
        CollectionAssert.AreEqual(payload, buffer);
        CollectionAssert.AreEqual(payload, writer.Sink.ToArray());
    }

    [TestMethod]
    public void CopyStream_Read_AndPeek_AndReadLine()
    {
        var payload = Encoding.ASCII.GetBytes("abc\r\ndef");
        using var inner = new MemoryStream(payload);
        using var reader = new CustomBufferedStream(inner, new DefaultBufferPool(), 256);
        var writer = new CapturingWriter();
        using var copy = new CopyStream(reader, writer, new DefaultBufferPool(), 8);

        Assert.IsTrue(copy.FillBufferAsync().GetAwaiter().GetResult());
        Assert.IsTrue(copy.DataAvailable);
        Assert.IsTrue(copy.Available > 0);
        Assert.AreEqual((byte)'a', copy.PeekByteFromBuffer(0));
        Assert.AreEqual((byte)'a', copy.PeekByteAsync(0).GetAwaiter().GetResult());
        Assert.IsNotNull(copy.PeekBytesAsync(0, 2).GetAwaiter().GetResult());

        var line = copy.ReadLineAsync().GetAwaiter().GetResult();
        Assert.AreEqual("abc", line);

        var rest = new byte[3];
        var n = copy.Read(rest, 0, 3);
        Assert.AreEqual(3, n);
        CollectionAssert.AreEqual(Encoding.ASCII.GetBytes("def"), rest);
        Assert.IsTrue(copy.ReadBytes >= 6);
    }
}
