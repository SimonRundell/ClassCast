namespace ClassCast.Common.Protocol;

/// <summary>
/// Enumerates every message type that can travel across the ClassCast control
/// (TCP 45679) and discovery (UDP 45678) channels.
/// </summary>
/// <remarks>
/// The member names are written in UPPER_SNAKE_CASE because they are serialised
/// verbatim onto the wire (via <c>JsonStringEnumConverter</c>) and must match the
/// protocol strings defined in the ClassCast Agent Specification, e.g.
/// <c>"BROADCAST_START"</c>. Do not rename members without updating both ends of
/// the protocol.
/// </remarks>
public enum MessageType
{
    /// <summary>UDP broadcast announcing a Student Client's presence on the subnet.</summary>
    BEACON,

    /// <summary>Acknowledgement. Used both as the UDP beacon reply (server &#8594; client)
    /// and as the control-channel receipt confirmation (client &#8594; server).</summary>
    ACK,

    /// <summary>Client &#8594; Server. First control-channel message after TCP connect.</summary>
    HELLO,

    /// <summary>Bidirectional keepalive sent every 10 seconds.</summary>
    HEARTBEAT,

    /// <summary>Client &#8594; Server. Carries a base64-encoded JPEG thumbnail.</summary>
    THUMBNAIL,

    /// <summary>Server &#8594; Client. Locks or unlocks keyboard and mouse.</summary>
    LOCK,

    /// <summary>Server &#8594; Client. Connect to the broadcast channel and display fullscreen.</summary>
    BROADCAST_START,

    /// <summary>Server &#8594; Client. Disconnect from the broadcast channel and restore the desktop.</summary>
    BROADCAST_STOP,

    /// <summary>Server &#8594; Client. Trigger a full Windows logoff.</summary>
    LOGOFF
}
