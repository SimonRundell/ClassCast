using System.Runtime.Versioning;
using ClassCast.Common.Config;
using ClassCast.Common.Logging;

namespace ClassCast.Student;

/// <summary>
/// Entry point for the ClassCast Student Client. Runs headless in the system tray
/// (no visible main window) and starts the discovery/control loop immediately
/// (specification section 5).
/// </summary>
[SupportedOSPlatform("windows")]
internal static class Program
{
    /// <summary>The Student Client version reported in beacons and HELLO.</summary>
    public const string Version = "1.0.0";

    /// <summary>
    /// Application entry point. Ensures only a single instance runs, then starts the
    /// tray application context.
    /// </summary>
    [STAThread]
    private static void Main()
    {
        // Prevent multiple student clients running concurrently on one machine.
        using var mutex = new Mutex(initiallyOwned: true, "ClassCast.Student.SingleInstance", out bool createdNew);
        if (!createdNew)
        {
            return;
        }

        ApplicationConfiguration.Initialize();
        Logger.Configure("Student");
        Logger.Info($"ClassCast Student {Version} starting.");

        AppConfig config = AppConfig.Load();
        Application.Run(new StudentApplicationContext(config, Version));

        Logger.Info("ClassCast Student exited.");
        GC.KeepAlive(mutex);
    }
}
