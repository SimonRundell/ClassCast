using ClassCast.Common.Network;

namespace ClassCast.Teacher.Services;

/// <summary>
/// Represents a single connected Student Client on the Teacher Server. Pairs the
/// managed control connection with the identity and live state shown on the
/// corresponding student tile.
/// </summary>
public sealed class StudentSession
{
    /// <summary>The managed control-channel connection to this student.</summary>
    public TcpClientManager Connection { get; }

    /// <summary>The student PC's machine name (from HELLO).</summary>
    public string MachineName { get; set; } = "(unknown)";

    /// <summary>The logged-in Active Directory username (from HELLO).</summary>
    public string AdUser { get; set; } = "(unknown)";

    /// <summary>The Active Directory domain reported by the student (from HELLO).</summary>
    public string AdDomain { get; set; } = string.Empty;

    /// <summary>The remote IP address of the student, recorded at connect time.</summary>
    public string RemoteIp { get; }

    /// <summary>The student's primary screen width in pixels (from HELLO).</summary>
    public int ScreenWidth { get; set; }

    /// <summary>The student's primary screen height in pixels (from HELLO).</summary>
    public int ScreenHeight { get; set; }

    /// <summary>Whether this student's input is currently locked.</summary>
    public bool IsLocked { get; set; }

    /// <summary>Whether a valid HELLO has been received and the tile should be shown.</summary>
    public bool HasIdentity { get; set; }

    /// <summary>UTC timestamp of the most recent inbound message; used for heartbeat timeout.</summary>
    public DateTime LastActivityUtc { get; set; } = DateTime.UtcNow;

    /// <summary>The next outbound sequence number for replay protection.</summary>
    private long _sequence;

    /// <summary>
    /// Serialises outbound sends so that a message's sequence number is assigned and
    /// written to the socket as one atomic step. Without this, a command stamped on the
    /// UI thread can be overtaken on the wire by a heartbeat stamped a moment later on
    /// the maintenance thread, causing the student's replay guard to discard the command.
    /// </summary>
    public SemaphoreSlim SendGate { get; } = new(1, 1);

    /// <summary>
    /// Initialises a session around an accepted control connection.
    /// </summary>
    /// <param name="connection">The managed control connection.</param>
    public StudentSession(TcpClientManager connection)
    {
        Connection = connection;
        // RemoteAddress is "ip:port"; keep only the IP for the trust check.
        string addr = connection.RemoteAddress;
        int colon = addr.LastIndexOf(':');
        RemoteIp = colon > 0 ? addr[..colon] : addr;
    }

    /// <summary>Returns the next monotonically increasing sequence number.</summary>
    /// <returns>A unique, increasing sequence value for an outbound message.</returns>
    public long NextSequence() => Interlocked.Increment(ref _sequence);

    /// <summary>A short label used for the student tile caption.</summary>
    public string DisplayLabel => $"{MachineName}  ({AdUser})";
}
