namespace ClassCast.Common.Media;

/// <summary>
/// A selectable broadcast quality preset: the resolution, frame rate and MJPEG
/// quality used when encoding the teacher's screen. The presets form a ladder tuned
/// to leave headroom on the typical school network speeds, since MJPEG is
/// bandwidth-heavy. The teacher chooses one from the toolbar to match their network.
/// </summary>
/// <param name="Key">Stable identifier persisted in settings (not shown to the user).</param>
/// <param name="Label">Human-readable name shown on the toolbar and in the menu.</param>
/// <param name="Width">Encoded frame width in pixels.</param>
/// <param name="Height">Encoded frame height in pixels.</param>
/// <param name="Fps">Target frame rate.</param>
/// <param name="JpegQuality">FFmpeg <c>-q:v</c> value (2 = best, 31 = most compressed).</param>
public sealed record BroadcastQuality(
    string Key, string Label, int Width, int Height, int Fps, int JpegQuality)
{
    /// <summary>Lowest tier: for a congested or 10 Mb link. ~2-3 Mbps.</summary>
    public static readonly BroadcastQuality TenMb = new("10Mb", "10 Mb network", 640, 360, 10, 6);

    /// <summary>Default tier: a typical 56 Mb Wi-Fi classroom. ~5 Mbps.</summary>
    public static readonly BroadcastQuality WiFi56 = new("56Mb", "56 Mb Wi-Fi", 854, 480, 15, 5);

    /// <summary>Wired 100 Mb tier: 720p. ~20 Mbps.</summary>
    public static readonly BroadcastQuality HundredMb = new("100Mb", "100 Mb network", 1280, 720, 20, 4);

    /// <summary>Gigabit tier: full 1080p. ~45 Mbps.</summary>
    public static readonly BroadcastQuality Gigabit = new("1000Mb", "1000 Mb network", 1920, 1080, 30, 3);

    /// <summary>All presets, ordered from lowest to highest bandwidth.</summary>
    public static readonly IReadOnlyList<BroadcastQuality> All = new[]
    {
        TenMb, WiFi56, HundredMb, Gigabit
    };

    /// <summary>The preset used when none has been chosen (the 56 Mb Wi-Fi baseline).</summary>
    public static BroadcastQuality Default => WiFi56;

    /// <summary>
    /// Resolves a preset by its <see cref="Key"/>, falling back to <see cref="Default"/>
    /// when the key is null or unrecognised.
    /// </summary>
    /// <param name="key">The stored preset key.</param>
    /// <returns>The matching preset, or the default.</returns>
    public static BroadcastQuality FromKey(string? key)
    {
        foreach (BroadcastQuality quality in All)
        {
            if (string.Equals(quality.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                return quality;
            }
        }
        return Default;
    }

    /// <summary>A menu caption combining the label with the resolution and frame rate.</summary>
    public string MenuText => $"{Label}  ({Width}×{Height} @ {Fps}fps)";
}
