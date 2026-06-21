using System.Drawing;
using System.Reflection;
using System.Runtime.Versioning;

namespace ClassCast.Common;

/// <summary>
/// Loads the shared ClassCast application icon, which is embedded in this assembly
/// (see the EmbeddedResource in <c>ClassCast.Common.csproj</c>). Used to set the window
/// and system-tray icons at runtime; the same <c>.ico</c> is also compiled into each
/// executable via <c>&lt;ApplicationIcon&gt;</c> so Explorer and shortcuts show it.
/// </summary>
[SupportedOSPlatform("windows")]
public static class AppIcon
{
    private const string ResourceName = "ClassCast.Common.classcast.ico";

    /// <summary>Loads the icon at its default (large) size.</summary>
    /// <returns>The application icon, or the system default if the resource is missing.</returns>
    public static Icon Load()
    {
        using Stream? stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName);
        return stream is null ? SystemIcons.Application : new Icon(stream);
    }

    /// <summary>Loads the icon at the requested square size (e.g. 16 for the tray).</summary>
    /// <param name="size">The desired width/height in pixels.</param>
    /// <returns>The closest available icon frame, or the system default if missing.</returns>
    public static Icon Load(int size)
    {
        using Stream? stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName);
        return stream is null ? SystemIcons.Application : new Icon(stream, new Size(size, size));
    }
}
