using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClassCast.Common.Protocol;

/// <summary>
/// Serialisable data-transfer object representing a single ClassCast protocol
/// message. A single DTO is used for every <see cref="MessageType"/>; only the
/// fields relevant to a given type are populated, and null fields are omitted
/// from the JSON to keep payloads compact.
/// </summary>
public sealed class ControlMessage
{
    /// <summary>The message type. Always present.</summary>
    [JsonPropertyName("type")]
    public MessageType Type { get; set; }

    /// <summary>
    /// Monotonically incrementing sequence number used by the Student Client to
    /// discard out-of-order / replayed messages (see specification section 12).
    /// Zero for UDP discovery traffic where ordering is not enforced.
    /// </summary>
    [JsonPropertyName("seq")]
    public long Seq { get; set; }

    /// <summary>NetBIOS / DNS machine name of the student PC (BEACON, HELLO).</summary>
    [JsonPropertyName("machineName")]
    public string? MachineName { get; set; }

    /// <summary>Logged-in Active Directory username, without the domain prefix (BEACON, HELLO).</summary>
    [JsonPropertyName("adUser")]
    public string? AdUser { get; set; }

    /// <summary>Active Directory domain portion of the logged-in identity (HELLO).</summary>
    [JsonPropertyName("adDomain")]
    public string? AdDomain { get; set; }

    /// <summary>Client application version string (BEACON).</summary>
    [JsonPropertyName("version")]
    public string? Version { get; set; }

    /// <summary>Server application version string (UDP ACK reply).</summary>
    [JsonPropertyName("serverVersion")]
    public string? ServerVersion { get; set; }

    /// <summary>Primary screen width in pixels, reported at HELLO time.</summary>
    [JsonPropertyName("screenWidth")]
    public int? ScreenWidth { get; set; }

    /// <summary>Primary screen height in pixels, reported at HELLO time.</summary>
    [JsonPropertyName("screenHeight")]
    public int? ScreenHeight { get; set; }

    /// <summary>Desired lock state for a LOCK message (<c>true</c> = locked).</summary>
    [JsonPropertyName("locked")]
    public bool? Locked { get; set; }

    /// <summary>Base64-encoded JPEG image for a THUMBNAIL message.</summary>
    [JsonPropertyName("thumbnail")]
    public string? Thumbnail { get; set; }

    /// <summary>
    /// The <see cref="MessageType"/> that an ACK is acknowledging. Lets the server
    /// correlate a control-channel ACK with the command that triggered it.
    /// </summary>
    [JsonPropertyName("ackType")]
    public MessageType? AckType { get; set; }

    /// <summary>Shared <see cref="JsonSerializerOptions"/> for all protocol traffic.</summary>
    public static readonly JsonSerializerOptions JsonOptions = CreateOptions();

    /// <summary>Builds the canonical serializer options: camelCase, string enums, skip nulls.</summary>
    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    /// <summary>Serialises this message to a compact UTF-8 JSON string.</summary>
    /// <returns>The JSON representation of the message.</returns>
    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    /// <summary>Serialises this message to a UTF-8 JSON byte array.</summary>
    /// <returns>UTF-8 encoded JSON bytes ready for length-prefix framing.</returns>
    public byte[] ToUtf8() => JsonSerializer.SerializeToUtf8Bytes(this, JsonOptions);

    /// <summary>Parses a JSON string into a <see cref="ControlMessage"/>.</summary>
    /// <param name="json">The JSON text to parse.</param>
    /// <returns>The deserialised message, or <c>null</c> if the text is invalid.</returns>
    public static ControlMessage? FromJson(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<ControlMessage>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Parses a UTF-8 JSON byte span into a <see cref="ControlMessage"/>.</summary>
    /// <param name="utf8">The UTF-8 encoded JSON bytes.</param>
    /// <returns>The deserialised message, or <c>null</c> if the bytes are invalid.</returns>
    public static ControlMessage? FromUtf8(ReadOnlySpan<byte> utf8)
    {
        try
        {
            return JsonSerializer.Deserialize<ControlMessage>(utf8, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // ----- Convenience factory methods ------------------------------------

    /// <summary>Creates a UDP BEACON message.</summary>
    public static ControlMessage Beacon(string machineName, string adUser, string version) => new()
    {
        Type = MessageType.BEACON,
        MachineName = machineName,
        AdUser = adUser,
        Version = version
    };

    /// <summary>Creates a UDP ACK reply containing the server version.</summary>
    public static ControlMessage UdpAck(string serverVersion) => new()
    {
        Type = MessageType.ACK,
        ServerVersion = serverVersion
    };

    /// <summary>Creates a control-channel HELLO message.</summary>
    public static ControlMessage Hello(string machineName, string adDomain, string adUser, int screenWidth, int screenHeight) => new()
    {
        Type = MessageType.HELLO,
        MachineName = machineName,
        AdDomain = adDomain,
        AdUser = adUser,
        ScreenWidth = screenWidth,
        ScreenHeight = screenHeight
    };

    /// <summary>Creates a HEARTBEAT keepalive message.</summary>
    public static ControlMessage Heartbeat() => new() { Type = MessageType.HEARTBEAT };

    /// <summary>Creates a THUMBNAIL message carrying a base64-encoded JPEG.</summary>
    public static ControlMessage ThumbnailMessage(string base64Jpeg) => new()
    {
        Type = MessageType.THUMBNAIL,
        Thumbnail = base64Jpeg
    };

    /// <summary>Creates a LOCK message instructing the client to lock or unlock input.</summary>
    public static ControlMessage Lock(bool locked) => new() { Type = MessageType.LOCK, Locked = locked };

    /// <summary>Creates a BROADCAST_START message.</summary>
    public static ControlMessage BroadcastStart() => new() { Type = MessageType.BROADCAST_START };

    /// <summary>Creates a BROADCAST_STOP message.</summary>
    public static ControlMessage BroadcastStop() => new() { Type = MessageType.BROADCAST_STOP };

    /// <summary>Creates a LOGOFF message.</summary>
    public static ControlMessage Logoff() => new() { Type = MessageType.LOGOFF };

    /// <summary>Creates a control-channel ACK confirming receipt of <paramref name="ackType"/>.</summary>
    public static ControlMessage ControlAck(MessageType ackType) => new()
    {
        Type = MessageType.ACK,
        AckType = ackType
    };
}
