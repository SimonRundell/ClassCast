using ClassCast.Common.Protocol;

namespace ClassCast.Tests;

/// <summary>
/// Verifies that every <see cref="ControlMessage"/> type serialises and deserialises
/// without data loss and that the on-the-wire enum strings match the protocol
/// (specification sections 2.3.2, 11).
/// </summary>
public class ProtocolTests
{
    [Fact]
    public void Beacon_RoundTrips()
    {
        ControlMessage msg = ControlMessage.Beacon("WKST-12", "jsmith", "1.0.0");
        ControlMessage? back = ControlMessage.FromJson(msg.ToJson());

        Assert.NotNull(back);
        Assert.Equal(MessageType.BEACON, back!.Type);
        Assert.Equal("WKST-12", back.MachineName);
        Assert.Equal("jsmith", back.AdUser);
        Assert.Equal("1.0.0", back.Version);
    }

    [Fact]
    public void UdpAck_RoundTrips()
    {
        ControlMessage? back = ControlMessage.FromJson(ControlMessage.UdpAck("1.0.0").ToJson());
        Assert.NotNull(back);
        Assert.Equal(MessageType.ACK, back!.Type);
        Assert.Equal("1.0.0", back.ServerVersion);
    }

    [Fact]
    public void Lock_RoundTrips_WithState()
    {
        ControlMessage? locked = ControlMessage.FromJson(ControlMessage.Lock(true).ToJson());
        ControlMessage? unlocked = ControlMessage.FromJson(ControlMessage.Lock(false).ToJson());

        Assert.Equal(true, locked!.Locked);
        Assert.Equal(false, unlocked!.Locked);
    }

    [Fact]
    public void Thumbnail_RoundTrips_Base64Payload()
    {
        const string payload = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAAB";
        ControlMessage? back = ControlMessage.FromJson(ControlMessage.ThumbnailMessage(payload).ToJson());

        Assert.NotNull(back);
        Assert.Equal(MessageType.THUMBNAIL, back!.Type);
        Assert.Equal(payload, back.Thumbnail);
    }

    [Theory]
    [InlineData(MessageType.BROADCAST_START, "BROADCAST_START")]
    [InlineData(MessageType.BROADCAST_STOP, "BROADCAST_STOP")]
    [InlineData(MessageType.LOGOFF, "LOGOFF")]
    [InlineData(MessageType.HEARTBEAT, "HEARTBEAT")]
    public void EnumValues_SerialiseToWireStrings(MessageType type, string expectedWire)
    {
        var msg = new ControlMessage { Type = type };
        string json = msg.ToJson();

        Assert.Contains($"\"type\":\"{expectedWire}\"", json);
        Assert.Equal(type, ControlMessage.FromJson(json)!.Type);
    }

    [Fact]
    public void ControlAck_PreservesAckType()
    {
        ControlMessage? back = ControlMessage.FromJson(ControlMessage.ControlAck(MessageType.LOGOFF).ToJson());

        Assert.NotNull(back);
        Assert.Equal(MessageType.ACK, back!.Type);
        Assert.Equal(MessageType.LOGOFF, back.AckType);
    }

    [Fact]
    public void Sequence_IsPreserved()
    {
        var msg = ControlMessage.Lock(true);
        msg.Seq = 9999;
        ControlMessage? back = ControlMessage.FromJson(msg.ToJson());

        Assert.Equal(9999, back!.Seq);
    }

    [Fact]
    public void InvalidJson_ReturnsNull()
    {
        Assert.Null(ControlMessage.FromJson("{ this is not valid json"));
    }
}
