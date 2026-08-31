using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using StreamExtended;
using StreamExtended.BufferPool;
using StreamExtended.Network;

namespace StreamExtended.Tests;

[TestClass]
public class CustomBufferedStreamTests
{
    private static DefaultBufferPool CreatePool() => new();

    [TestMethod]
    public async Task PeekByteAsync_BeforeRead_ReturnsSameBytesThenReadConsumesThem()
    {
        var payload = new byte[] { 0x10, 0x20, 0x30, 0x40 };
        await using var inner = new MemoryStream(payload);
        await using var stream = new CustomBufferedStream(inner, CreatePool(), 256);

        Assert.AreEqual(0x10, await stream.PeekByteAsync(0));
        Assert.AreEqual(0x20, await stream.PeekByteAsync(1));
        Assert.AreEqual(0x30, await stream.PeekByteAsync(2));

        var buffer = new byte[4];
        var read = await stream.ReadAsync(buffer, 0, 4);

        Assert.AreEqual(4, read);
        CollectionAssert.AreEqual(payload, buffer);
        Assert.AreEqual(0, stream.Available);
        Assert.IsFalse(stream.DataAvailable);
    }

    [TestMethod]
    public void FillBuffer_DataAvailable_AndAvailable()
    {
        var payload = Encoding.ASCII.GetBytes("abcdef");
        using var inner = new MemoryStream(payload);
        using var stream = new CustomBufferedStream(inner, CreatePool(), 256);

        Assert.IsFalse(stream.DataAvailable);
        Assert.AreEqual(0, stream.Available);

        Assert.IsTrue(stream.FillBuffer());
        Assert.IsTrue(stream.DataAvailable);
        Assert.AreEqual(payload.Length, stream.Available);

        var first = stream.ReadByteFromBuffer();
        Assert.AreEqual((byte)'a', first);
        Assert.AreEqual(payload.Length - 1, stream.Available);
        Assert.IsTrue(stream.DataAvailable);
    }

    [TestMethod]
    public async Task ReadLineAsync_SimpleCrLfLine()
    {
        var payload = Encoding.UTF8.GetBytes("hello\r\n");
        await using var inner = new MemoryStream(payload);
        await using var stream = new CustomBufferedStream(inner, CreatePool(), 256);

        var line = await stream.ReadLineAsync();

        Assert.AreEqual("hello", line);
    }

    [TestMethod]
    public void LeaveOpen_True_LeavesInnerMemoryStreamOpenAfterDispose()
    {
        var inner = new MemoryStream();
        inner.Write(new byte[] { 1, 2, 3 });
        inner.Position = 0;
        var stream = new CustomBufferedStream(inner, CreatePool(), 256, leaveOpen: true);
        stream.Dispose();

        Assert.IsTrue(inner.CanRead);
        Assert.IsTrue(inner.CanWrite);
        // Writing confirms the stream was not disposed.
        inner.Position = inner.Length;
        inner.WriteByte(4);
        Assert.AreEqual(4, inner.Length);

        inner.Dispose();
    }

    [TestMethod]
    public async Task WorksAsIPeekStream_IntoSslToolsPeekClientHello()
    {
        var helloBytes = BuildMinimalTls12ClientHello();
        await using var inner = new MemoryStream(helloBytes);
        await using var stream = new CustomBufferedStream(inner, CreatePool(), 4096);

        var hello = await SslTools.PeekClientHello(stream, CreatePool());

        Assert.IsNotNull(hello);
        Assert.AreEqual(3, hello!.HandshakeVersion);
        Assert.AreEqual(3, hello.MajorVersion);
        Assert.AreEqual(3, hello.MinorVersion);
        Assert.IsNotNull(hello.Extensions);
        Assert.IsTrue(hello.Extensions!.ContainsKey("server_name"));
        Assert.AreEqual("example.com", hello.Extensions["server_name"].Data);
    }

    private static byte[] BuildSni(string host)
    {
        var hostBytes = Encoding.ASCII.GetBytes(host);
        var entry = new byte[3 + hostBytes.Length];
        entry[0] = 0;
        entry[1] = (byte)(hostBytes.Length >> 8);
        entry[2] = (byte)hostBytes.Length;
        Buffer.BlockCopy(hostBytes, 0, entry, 3, hostBytes.Length);
        var payload = new byte[2 + entry.Length];
        payload[0] = (byte)(entry.Length >> 8);
        payload[1] = (byte)entry.Length;
        Buffer.BlockCopy(entry, 0, payload, 2, entry.Length);
        return payload;
    }

    private static byte[] Ext(ushort type, byte[] data)
    {
        var buf = new byte[4 + data.Length];
        buf[0] = (byte)(type >> 8);
        buf[1] = (byte)type;
        buf[2] = (byte)(data.Length >> 8);
        buf[3] = (byte)data.Length;
        Buffer.BlockCopy(data, 0, buf, 4, data.Length);
        return buf;
    }

    private static byte[] BuildMinimalTls12ClientHello()
    {
        var sni = Ext(0, BuildSni("example.com"));
        var extBlock = new byte[2 + sni.Length];
        extBlock[0] = (byte)(sni.Length >> 8);
        extBlock[1] = (byte)sni.Length;
        Buffer.BlockCopy(sni, 0, extBlock, 2, sni.Length);

        var body = new MemoryStream();
        body.WriteByte(0x03);
        body.WriteByte(0x03);
        body.Write(new byte[32]);
        body.WriteByte(0);
        body.WriteByte(0);
        body.WriteByte(2);
        body.WriteByte(0x13);
        body.WriteByte(0x01);
        body.WriteByte(1);
        body.WriteByte(0);
        body.Write(extBlock);

        var handshake = body.ToArray();
        var hsLen = handshake.Length;
        var recordPayloadLen = 4 + hsLen;
        var record = new byte[5 + recordPayloadLen];
        record[0] = 0x16;
        record[1] = 0x03;
        record[2] = 0x03;
        record[3] = (byte)(recordPayloadLen >> 8);
        record[4] = (byte)recordPayloadLen;
        record[5] = 0x01;
        record[6] = (byte)(hsLen >> 16);
        record[7] = (byte)(hsLen >> 8);
        record[8] = (byte)hsLen;
        Buffer.BlockCopy(handshake, 0, record, 9, handshake.Length);
        return record;
    }
}
