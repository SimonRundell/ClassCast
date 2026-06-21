using ClassCast.Common.Protocol;

namespace ClassCast.Tests;

/// <summary>
/// Verifies that <see cref="FrameWriter"/> and <see cref="FrameReader"/> round-trip
/// payloads and control messages byte-for-byte (specification section 11).
/// </summary>
public class FrameTests
{
    [Fact]
    public async Task RawPayload_RoundTrips_ByteForByte()
    {
        byte[] original = [0x00, 0x01, 0x02, 0xFF, 0xFE, 0x7F, 0x80, 0x42];

        using var stream = new MemoryStream();
        await FrameWriter.WriteFrameAsync(stream, original);
        stream.Position = 0;
        byte[]? read = await FrameReader.ReadFrameAsync(stream);

        Assert.NotNull(read);
        Assert.Equal(original, read);
    }

    [Fact]
    public async Task EmptyPayload_RoundTrips()
    {
        using var stream = new MemoryStream();
        await FrameWriter.WriteFrameAsync(stream, Array.Empty<byte>());
        stream.Position = 0;
        byte[]? read = await FrameReader.ReadFrameAsync(stream);

        Assert.NotNull(read);
        Assert.Empty(read);
    }

    [Fact]
    public async Task MultipleFrames_ReadBackInOrder()
    {
        byte[] a = [1, 2, 3];
        byte[] b = [9, 8, 7, 6, 5];

        using var stream = new MemoryStream();
        await FrameWriter.WriteFrameAsync(stream, a);
        await FrameWriter.WriteFrameAsync(stream, b);
        stream.Position = 0;

        Assert.Equal(a, await FrameReader.ReadFrameAsync(stream));
        Assert.Equal(b, await FrameReader.ReadFrameAsync(stream));
        Assert.Null(await FrameReader.ReadFrameAsync(stream)); // end of stream
    }

    [Fact]
    public async Task ControlMessage_RoundTrips_OverFrame()
    {
        ControlMessage original = ControlMessage.Hello("WKST-12", "SCHOOL", "jsmith", 1920, 1080);
        original.Seq = 42;

        using var stream = new MemoryStream();
        await FrameWriter.WriteMessageAsync(stream, original);
        stream.Position = 0;
        ControlMessage? read = await FrameReader.ReadMessageAsync(stream);

        Assert.NotNull(read);
        Assert.Equal(MessageType.HELLO, read!.Type);
        Assert.Equal("WKST-12", read.MachineName);
        Assert.Equal("SCHOOL", read.AdDomain);
        Assert.Equal("jsmith", read.AdUser);
        Assert.Equal(1920, read.ScreenWidth);
        Assert.Equal(1080, read.ScreenHeight);
        Assert.Equal(42, read.Seq);
    }

    [Fact]
    public async Task BigEndianLengthPrefix_IsUsed()
    {
        byte[] payload = new byte[258]; // 0x0102
        using var stream = new MemoryStream();
        await FrameWriter.WriteFrameAsync(stream, payload);

        byte[] buffer = stream.ToArray();
        // First four bytes are the big-endian length: 258 = 0x00 0x00 0x01 0x02.
        Assert.Equal(0x00, buffer[0]);
        Assert.Equal(0x00, buffer[1]);
        Assert.Equal(0x01, buffer[2]);
        Assert.Equal(0x02, buffer[3]);
    }
}
