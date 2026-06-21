using System.Runtime.Versioning;
using ClassCast.Common;
using ClassCast.Common.Config;
using ClassCast.Common.Logging;
using ClassCast.Common.Network;
using ClassCast.Student.Forms;
using ClassCast.Student.Services;

namespace ClassCast.Student;

/// <summary>
/// The Student Client's application context. Owns the system-tray icon, the input
/// locker, the broadcast overlay and the <see cref="StudentClient"/> coordinator,
/// and provides the protected exit flow. There is no visible main window
/// (specification section 5.1).
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class StudentApplicationContext : ApplicationContext
{
    private readonly AppConfig _config;
    private readonly NotifyIcon _trayIcon;
    private readonly InputLocker _inputLocker;
    private readonly BroadcastOverlay _overlay;
    private readonly StudentClient _client;
    private readonly System.Windows.Forms.Timer _statusTimer;
    private readonly ToolStripMenuItem _statusItem;

    /// <summary>
    /// Builds the tray context, starts the input hooks and the discovery loop, and
    /// attempts to create the required firewall rules.
    /// </summary>
    /// <param name="config">Application configuration.</param>
    /// <param name="version">Client version string.</param>
    public StudentApplicationContext(AppConfig config, string version)
    {
        _config = config;

        // Attempt firewall rule creation; warn once on failure.
        if (!FirewallManager.EnsureRules(config))
        {
            _ = MessageBox.Show(FirewallManager.BuildManualInstructions(config),
                "ClassCast", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        _inputLocker = new InputLocker();
        _inputLocker.Start();

        _overlay = new BroadcastOverlay();

        _client = new StudentClient(config, version, _inputLocker, _overlay);

        _statusItem = new ToolStripMenuItem("Starting...") { Enabled = false };
        var menu = new ContextMenuStrip();
        menu.Items.Add(_statusItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit ClassCast", null, OnExitClicked);

        _trayIcon = new NotifyIcon
        {
            Icon = AppIcon.Load(16),
            Text = "ClassCast Student",
            Visible = true,
            ContextMenuStrip = menu
        };

        _client.Start();

        // Refresh the tray tooltip/menu with the current status periodically.
        _statusTimer = new System.Windows.Forms.Timer { Interval = 2000 };
        _statusTimer.Tick += (_, _) => UpdateStatus();
        _statusTimer.Start();

        Logger.Info($"ClassCast Student {version} running in the system tray.");
    }

    /// <summary>Refreshes the tray tooltip and status menu item.</summary>
    private void UpdateStatus()
    {
        string status = _client.Status;
        _statusItem.Text = status;
        // NotifyIcon.Text is limited to 63 characters.
        _trayIcon.Text = status.Length > 60 ? status[..60] : status;
    }

    /// <summary>Handles the tray Exit action behind the administrator credential check.</summary>
    private void OnExitClicked(object? sender, EventArgs e)
    {
        if (!ExitGuard.AuthoriseExit(_overlay, _config))
        {
            return;
        }

        Logger.Info("ClassCast Student exiting.");
        Shutdown();
        ExitThread();
    }

    /// <summary>Disposes all owned resources.</summary>
    private void Shutdown()
    {
        _statusTimer.Stop();
        _statusTimer.Dispose();
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        _client.Dispose();
        _inputLocker.Dispose();
        _overlay.Dispose();
    }

    /// <summary>Releases resources held by the context.</summary>
    /// <param name="disposing">true to release managed resources.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try { Shutdown(); } catch { /* already shut down */ }
        }
        base.Dispose(disposing);
    }
}
