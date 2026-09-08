using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using StreamExtended;
using StreamExtended.BufferPool;
using StreamExtended.Network;

namespace StreamExtended.Tests;

[TestClass]
public class CustomBufferedStreamCoverageTests
{
    private static DefaultBufferPool CreatePool() => new();

    [TestMethod]
    public void SyncReadWriteSeekFlush_AndEvents()
    {
        var payload = Encoding.ASCII.GetBytes("0123456789");
        using var inner = new MemoryStream();
        using var stream = new CustomBufferedStream(inner, CreatePool(), 32);

        int writeCount = 0;
        int readCount = 0;
        stream.DataWrite += (_, e) => writeCount += e.Count;
        stream.DataRead += (_, e) => readCount += e.Count;

        stream.Write(payload, 0, payload.Length);
        stream.Flush();
        Assert.AreEqual(payload.Length, writeCount);
        Assert.AreEqual(payload.Length, stream.Length);
        Assert.IsTrue(stream.CanRead);
        Assert.IsTrue(stream.CanWrite);
        Assert.IsTrue(stream.CanSeek);

        stream.Position = 0;
        stream.Seek(0, SeekOrigin.Begin);
        var buffer = new byte[4];
        var n = stream.Read(buffer, 0, 4);
        Assert.AreEqual(4, n);
        CollectionAssert.AreEqual(Encoding.ASCII.GetBytes("0123"), buffer);
        Assert.IsTrue(readCount >= 4);

        Assert.AreEqual((int)'4', stream.ReadByte());
        stream.WriteByte((byte)'Z');
        stream.SetLength(stream.Length);
    }

    [TestMethod]
    public async Task CopyToAsync_FlushesBufferedData()
    {
        var payload = Encoding.ASCII.GetBytes("copy-me-please");
        await using var inner = new MemoryStream(payload);
        await using var stream = new CustomBufferedStream(inner, CreatePool(), 256);
        Assert.IsTrue(await stream.FillBufferAsync());

        await using var dest = new MemoryStream();
        await stream.CopyToAsync(dest);
        CollectionAssert.AreEqual(payload, dest.ToArray());
    }

    [TestMethod]
    public async Task PeekBytes_AndPeekIntoBuffer_Work()
    {
        var payload = new byte[] { 1, 2, 3, 4, 5 };
        await using var inner = new MemoryStream(payload);
        await using var stream = new CustomBufferedStream(inner, CreatePool(), 64);

        var peeked = await stream.PeekBytesAsync(1, 2);
        CollectionAssert.AreEqual(new byte[] { 2, 3 }, peeked);

        var into = new byte[2];
        var n = await stream.PeekBytesAsync(into, 0, 0, 2);
        Assert.AreEqual(2, n);
        CollectionAssert.AreEqual(new byte[] { 1, 2 }, into);
    }

    [TestMethod]
    public async Task PeekBeyondAvailable_ReturnsSentinel()
    {
        var payload = new byte[] { 9 };
        await using var inner = new MemoryStream(payload);
        await using var stream = new CustomBufferedStream(inner, CreatePool(), 64);

        Assert.AreEqual(-1, await stream.PeekByteAsync(5));
        Assert.IsNull(await stream.PeekBytesAsync(0, 5));
        var into = new byte[4];
        Assert.AreEqual(0, await stream.PeekBytesAsync(into, 0, 2, 2));
    }

    [TestMethod]
    public async Task PeekIndexExceedsBufferSize_Throws()
    {
        await using var inner = new MemoryStream(new byte[] { 1 });
        await using var stream = new CustomBufferedStream(inner, CreatePool(), 8);

        await Assert.ThrowsExceptionAsync<ArgumentOutOfRangeException>(
            () => stream.PeekByteAsync(16));
        await Assert.ThrowsExceptionAsync<ArgumentOutOfRangeException>(
            () => stream.PeekBytesAsync(0, 16)!);
        await Assert.ThrowsExceptionAsync<ArgumentOutOfRangeException>(async () =>
        {
            var into = new byte[16];
            await stream.PeekBytesAsync(into, 0, 0, 16);
        });
    }

    [TestMethod]
    public void PeekByteFromBuffer_AndEmptyRead_Throw()
    {
        using var inner = new MemoryStream(new byte[] { 1, 2 });
        using var stream = new CustomBufferedStream(inner, CreatePool(), 32);
        Assert.IsTrue(stream.FillBuffer());
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => stream.PeekByteFromBuffer(10));
        _ = stream.ReadByteFromBuffer();
        _ = stream.ReadByteFromBuffer();
        Assert.ThrowsException<InvalidOperationException>(() => stream.ReadByteFromBuffer());
    }

    [TestMethod]
    public async Task ReadAndIgnoreAllLinesAsync_ConsumesLines()
    {
        var payload = Encoding.UTF8.GetBytes("one\r\ntwo\r\n\r\n");
        await using var inner = new MemoryStream(payload);
        await using var stream = new CustomBufferedStream(inner, CreatePool(), 64);
        await stream.ReadAndIgnoreAllLinesAsync();
        Assert.IsTrue(stream.IsClosed || stream.Available == 0 || !stream.DataAvailable);
    }

    [TestMethod]
    public async Task ReadLineAsync_LfOnly_AndPartialWithoutNewline()
    {
        await using (var inner = new MemoryStream(Encoding.UTF8.GetBytes("lf-only\n")))
        await using (var stream = new CustomBufferedStream(inner, CreatePool(), 32))
        {
            Assert.AreEqual("lf-only", await stream.ReadLineAsync());
        }

        await using var inner2 = new MemoryStream(Encoding.UTF8.GetBytes("no-nl"));
        await using var stream2 = new CustomBufferedStream(inner2, CreatePool(), 8);
        Assert.AreEqual("no-nl", await stream2.ReadLineAsync());
    }

    [TestMethod]
    public async Task ReadLineAsync_ResizesWhenLineExceedsBuffer()
    {
        var longLine = new string('x', 40) + "\r\n";
        await using var inner = new MemoryStream(Encoding.UTF8.GetBytes(longLine));
        await using var stream = new CustomBufferedStream(inner, CreatePool(), 8);
        Assert.AreEqual(new string('x', 40), await stream.ReadLineAsync());
    }

    [TestMethod]
    public async Task MemoryBasedReadWriteAsync_RoundTrip()
    {
        await using var inner = new MemoryStream();
        await using var stream = new CustomBufferedStream(inner, CreatePool(), 32);
        var payload = Encoding.ASCII.GetBytes("mem-async");
        await stream.WriteAsync(payload.AsMemory());
        await stream.FlushAsync();
        stream.Position = 0;
        var dest = new byte[payload.Length];
        var n = await stream.ReadAsync(dest.AsMemory());
        Assert.AreEqual(payload.Length, n);
        CollectionAssert.AreEqual(payload, dest);
    }

    [TestMethod]
    public void FillBuffer_WhenClosed_ReturnsFalse()
    {
        using var inner = new MemoryStream();
        using var stream = new CustomBufferedStream(inner, CreatePool(), 16);
        Assert.IsFalse(stream.FillBuffer());
        Assert.IsTrue(stream.IsClosed);
        Assert.IsFalse(stream.FillBuffer());
    }
}
