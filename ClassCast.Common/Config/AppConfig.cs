using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClassCast.Common.Config;

/// <summary>
/// Strongly-typed view over the <c>config.json</c> file that lives alongside each
/// ClassCast executable. A single class covers both the Teacher and Student schemas;
/// fields not present in a given file simply retain their documented defaults.
/// </summary>
public sealed class AppConfig
{
    /// <summary>Active Directory domain (NetBIOS short name), e.g. <c>"SCHOOL"</c>. Teacher only.</summary>
    [JsonPropertyName("adDomain")]
    public string AdDomain { get; set; } = "SCHOOL";

    /// <summary>Active Directory group whose members may use the Teacher Server.</summary>
    [JsonPropertyName("adTeacherGroup")]
    public string AdTeacherGroup { get; set; } = "ClassCast-Teachers";

    /// <summary>
    /// How the Teacher Server authenticates the teacher at startup. <c>"ActiveDirectory"</c>
    /// (the default) validates against the domain; <c>"Workgroup"</c> (or <c>"Local"</c>)
    /// uses a single shared teacher password held in <see cref="TeacherPasswordHash"/>,
    /// for machines that are not domain-joined. Teacher only.
    /// </summary>
    [JsonPropertyName("authMode")]
    public string AuthMode { get; set; } = "ActiveDirectory";

    /// <summary>
    /// Salted PBKDF2 hash of the shared teacher password used in workgroup mode. Empty
    /// until a password is set on first run. Never stores the password in clear text.
    /// Teacher only.
    /// </summary>
    [JsonPropertyName("teacherPasswordHash")]
    public string TeacherPasswordHash { get; set; } = "";

    /// <summary>
    /// True when <see cref="AuthMode"/> selects shared-password (non-domain) authentication.
    /// </summary>
    [JsonIgnore]
    public bool UseWorkgroupAuth =>
        AuthMode.Equals("Workgroup", StringComparison.OrdinalIgnoreCase) ||
        AuthMode.Equals("Local", StringComparison.OrdinalIgnoreCase);

    /// <summary>UDP port used for student discovery beacons.</summary>
    [JsonPropertyName("udpDiscoveryPort")]
    public int UdpDiscoveryPort { get; set; } = 45678;

    /// <summary>TCP port used for the persistent control channel.</summary>
    [JsonPropertyName("tcpControlPort")]
    public int TcpControlPort { get; set; } = 45679;

    /// <summary>TCP port used for the one-to-many broadcast video channel.</summary>
    [JsonPropertyName("tcpBroadcastPort")]
    public int TcpBroadcastPort { get; set; } = 45680;

    /// <summary>Broadcast frame width in pixels. Teacher only.</summary>
    [JsonPropertyName("broadcastWidth")]
    public int BroadcastWidth { get; set; } = 854;

    /// <summary>Broadcast frame height in pixels. Teacher only.</summary>
    [JsonPropertyName("broadcastHeight")]
    public int BroadcastHeight { get; set; } = 480;

    /// <summary>Target broadcast frame rate (frames per second). Teacher only.</summary>
    [JsonPropertyName("broadcastFps")]
    public int BroadcastFps { get; set; } = 15;

    /// <summary>
    /// Site default broadcast quality preset key (see <c>BroadcastQuality.Key</c>), e.g.
    /// <c>"56Mb"</c>. Used as the starting selection until the teacher picks another from
    /// the toolbar; their per-machine choice is then remembered separately. Teacher only.
    /// </summary>
    [JsonPropertyName("broadcastQuality")]
    public string BroadcastQuality { get; set; } = "56Mb";

    /// <summary>Thumbnail capture rate (frames per second). Student capture / Teacher display.</summary>
    [JsonPropertyName("thumbnailFps")]
    public int ThumbnailFps { get; set; } = 1;

    /// <summary>Thumbnail width in pixels.</summary>
    [JsonPropertyName("thumbnailWidth")]
    public int ThumbnailWidth { get; set; } = 320;

    /// <summary>Thumbnail height in pixels.</summary>
    [JsonPropertyName("thumbnailHeight")]
    public int ThumbnailHeight { get; set; } = 180;

    /// <summary>Relative or absolute path to the bundled <c>ffmpeg.exe</c>. Teacher only.</summary>
    [JsonPropertyName("ffmpegPath")]
    public string FfmpegPath { get; set; } = "ffmpeg\\ffmpeg.exe";

    /// <summary>Default config file name expected next to each executable.</summary>
    public const string DefaultFileName = "config.json";

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true
    };

    /// <summary>
    /// Loads configuration from the given path, falling back to documented defaults
    /// for any missing file or unreadable content.
    /// </summary>
    /// <param name="path">
    /// Path to <c>config.json</c>. When <c>null</c>, the file is sought next to the
    /// running executable.
    /// </param>
    /// <returns>A populated <see cref="AppConfig"/> (never <c>null</c>).</returns>
    public static AppConfig Load(string? path = null)
    {
        path ??= Path.Combine(AppContext.BaseDirectory, DefaultFileName);

        if (!File.Exists(path))
        {
            return new AppConfig();
        }

        try
        {
            string json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<AppConfig>(json, Options) ?? new AppConfig();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            // A malformed or unreadable config must not crash startup; use defaults.
            return new AppConfig();
        }
    }

    /// <summary>
    /// Resolves <see cref="FfmpegPath"/> to an absolute path relative to the
    /// executable directory when it is not already rooted.
    /// </summary>
    /// <returns>The absolute path to the ffmpeg executable.</returns>
    public string ResolveFfmpegPath()
        => Path.IsPathRooted(FfmpegPath)
            ? FfmpegPath
            : Path.Combine(AppContext.BaseDirectory, FfmpegPath);

    /// <summary>Serialises this configuration to indented JSON.</summary>
    /// <returns>The JSON representation of the configuration.</returns>
    public string ToJson() => JsonSerializer.Serialize(this, Options);

    /// <summary>
    /// Writes this configuration back to disk as indented JSON. Used by the workgroup
    /// login flow to persist a newly set teacher password hash.
    /// </summary>
    /// <param name="path">
    /// Destination path. When <c>null</c>, the file is written next to the running
    /// executable (matching <see cref="Load"/>).
    /// </param>
    public void Save(string? path = null)
    {
        path ??= Path.Combine(AppContext.BaseDirectory, DefaultFileName);
        File.WriteAllText(path, ToJson());
    }
}
