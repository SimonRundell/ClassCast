using System.Collections.Concurrent;
using ClassCast.Common.Config;
using ClassCast.Common.Logging;
using ClassCast.Common.Media;
using ClassCast.Common.Network;
using ClassCast.Teacher.Services;

namespace ClassCast.Teacher.Forms;

/// <summary>
/// The Teacher Server main control panel. Hosts the live grid of student tiles and
/// the toolbar/status bar, and owns the discovery, control and broadcast services.
/// Network events are marshalled onto the UI thread before touching controls
/// (specification section 4.1).
/// </summary>
public partial class MainForm : Form
{
    private readonly AppConfig _config;
    private readonly string _teacherName;
    private readonly string _version;

    private readonly DiscoveryService _discovery;
    private readonly ControlServer _control;
    private readonly BroadcastServer _broadcast;

    private readonly ConcurrentDictionary<StudentSession, StudentTile> _tiles = new();

    /// <summary>The currently selected broadcast quality preset.</summary>
    private BroadcastQuality _quality = BroadcastQuality.Default;

    /// <summary>The screen currently (or most recently) being broadcast, for quality restarts.</summary>
    private Screen? _currentScreen;

    /// <summary>
    /// Initialises the control panel and constructs (but does not start) the services.
    /// </summary>
    /// <param name="config">Application configuration.</param>
    /// <param name="teacherName">Display name of the authenticated teacher.</param>
    /// <param name="version">Server version string.</param>
    public MainForm(AppConfig config, string teacherName, string version)
    {
        _config = config;
        _teacherName = teacherName;
        _version = version;

        InitializeComponent();
        Icon = ClassCast.Common.AppIcon.Load();

        _discovery = new DiscoveryService(config, version);
        _control = new ControlServer(config);
        _broadcast = new BroadcastServer(config);

        _control.StudentConnected += OnStudentConnected;
        _control.StudentDisconnected += OnStudentDisconnected;
        _control.StudentUpdated += OnStudentUpdated;
        _control.ThumbnailReceived += OnThumbnailReceived;
        _broadcast.ClientCountChanged += _ => UpdateStatus();

        lblTeacher.Text = $"Signed in: {teacherName}";

        // Start from the teacher's remembered choice, falling back to the site default.
        _quality = BroadcastQuality.FromKey(TeacherPreferences.Load().BroadcastQuality ?? config.BroadcastQuality);
        BuildQualityMenu();
        UpdateQualityButton();
    }

    // ----- Broadcast quality ----------------------------------------------

    /// <summary>(Re)builds the Quality dropdown, ticking the active preset.</summary>
    private void BuildQualityMenu()
    {
        tsbQuality.DropDownItems.Clear();
        foreach (BroadcastQuality quality in BroadcastQuality.All)
        {
            BroadcastQuality preset = quality;   // fresh variable per iteration for the closure
            var item = new ToolStripMenuItem(preset.MenuText)
            {
                Checked = preset.Key == _quality.Key,
                CheckOnClick = false
            };
            item.Click += (_, _) => OnQualitySelected(preset);
            tsbQuality.DropDownItems.Add(item);
        }
    }

    /// <summary>Updates the Quality button caption to show the active preset.</summary>
    private void UpdateQualityButton() => tsbQuality.Text = $"Quality: {_quality.Label}";

    /// <summary>Applies a newly chosen quality preset and remembers it for next launch.</summary>
    private async void OnQualitySelected(BroadcastQuality quality)
    {
        _quality = quality;
        new TeacherPreferences { BroadcastQuality = quality.Key }.Save();
        BuildQualityMenu();
        UpdateQualityButton();

        // If a broadcast is live, restart it so the new quality takes effect at once.
        if (_broadcast.IsBroadcasting && _currentScreen is Screen screen)
        {
            await _control.BroadcastCommandAllAsync(start: false);
            _broadcast.StopBroadcast();
            _broadcast.StartBroadcast(screen.Bounds, _quality);
            await _control.BroadcastCommandAllAsync(start: true);
            UpdateBroadcastUi();
        }
    }

    /// <summary>Starts all network services and creates firewall rules on first show.</summary>
    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        if (!FirewallManager.EnsureRules(_config))
        {
            MessageBox.Show(this, FirewallManager.BuildManualInstructions(_config),
                "ClassCast firewall", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        try
        {
            _broadcast.Start();
            _control.Start();
            _discovery.Start();
            Logger.Info("Teacher services started.");
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to start services.", ex);
            MessageBox.Show(this, $"Failed to start network services:\r\n{ex.Message}",
                "ClassCast", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        UpdateStatus();
    }

    // ----- Control server event handlers (raised off the UI thread) --------

    /// <summary>Adds a tile when a student completes HELLO.</summary>
    private void OnStudentConnected(StudentSession session)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => OnStudentConnected(session));
            return;
        }

        var tile = new StudentTile(session);
        tile.LockRequested += OnTileLockRequested;
        tile.LogoffRequested += OnTileLogoffRequested;
        _tiles[session] = tile;
        flowStudents.Controls.Add(tile);
        UpdateStatus();
    }

    /// <summary>Removes a tile when a student disconnects.</summary>
    private void OnStudentDisconnected(StudentSession session)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => OnStudentDisconnected(session));
            return;
        }

        if (_tiles.TryRemove(session, out StudentTile? tile))
        {
            flowStudents.Controls.Remove(tile);
            tile.Dispose();
        }
        UpdateStatus();
    }

    /// <summary>Refreshes a tile when the session state changes.</summary>
    private void OnStudentUpdated(StudentSession session)
    {
        if (_tiles.TryGetValue(session, out StudentTile? tile))
        {
            tile.RefreshState();
        }
    }

    /// <summary>Decodes an inbound thumbnail and assigns it to the matching tile.</summary>
    private void OnThumbnailReceived(StudentSession session, byte[] jpeg)
    {
        if (!_tiles.TryGetValue(session, out StudentTile? tile))
        {
            return;
        }

        try
        {
            // Copy into a standalone Bitmap so the backing stream can be released.
            using var ms = new MemoryStream(jpeg);
            using var decoded = Image.FromStream(ms);
            var bitmap = new Bitmap(decoded);
            tile.SetThumbnail(bitmap);
        }
        catch (Exception ex)
        {
            Logger.Debug($"Failed to decode thumbnail from {session.MachineName}: {ex.Message}");
        }
    }

    // ----- Per-tile actions ------------------------------------------------

    /// <summary>Handles a per-student lock request.</summary>
    private async void OnTileLockRequested(StudentSession session, bool locked)
    {
        await _control.SetLockAsync(session, locked);
    }

    /// <summary>Handles a per-student log off request.</summary>
    private async void OnTileLogoffRequested(StudentSession session)
    {
        await _control.LogoffAsync(session);
    }

    // ----- Toolbar actions -------------------------------------------------

    /// <summary>Toggles the broadcast: stop if running, otherwise broadcast the primary screen.</summary>
    private void OnBroadcastButtonClick(object? sender, EventArgs e)
    {
        if (_broadcast.IsBroadcasting)
        {
            StopBroadcasting();
        }
        else
        {
            StartBroadcastForScreen(Screen.PrimaryScreen ?? Screen.AllScreens[0]);
        }
    }

    /// <summary>Rebuilds the broadcast dropdown: one entry per screen, or a stop entry while live.</summary>
    private void OnBroadcastDropDownOpening(object? sender, EventArgs e)
    {
        tsbBroadcast.DropDownItems.Clear();

        if (_broadcast.IsBroadcasting)
        {
            var stop = new ToolStripMenuItem("Stop Broadcast");
            stop.Click += (_, _) => StopBroadcasting();
            tsbBroadcast.DropDownItems.Add(stop);
            return;
        }

        Screen[] screens = Screen.AllScreens;
        for (int i = 0; i < screens.Length; i++)
        {
            Screen screen = screens[i];   // fresh variable per iteration for the closure
            string label = $"Broadcast Screen {i + 1} ({screen.Bounds.Width}×{screen.Bounds.Height})"
                         + (screen.Primary ? " — primary" : "");
            var item = new ToolStripMenuItem(label);
            item.Click += (_, _) => StartBroadcastForScreen(screen);
            tsbBroadcast.DropDownItems.Add(item);
        }
    }

    /// <summary>Starts broadcasting the given screen to all connected students.</summary>
    private async void StartBroadcastForScreen(Screen screen)
    {
        if (_control.ConnectedCount == 0)
        {
            MessageBox.Show(this, "No students are connected.", "ClassCast",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        _currentScreen = screen;
        _broadcast.StartBroadcast(screen.Bounds, _quality);
        await _control.BroadcastCommandAllAsync(start: true);
        UpdateBroadcastUi();
    }

    /// <summary>Stops the active broadcast and restores all students.</summary>
    private async void StopBroadcasting()
    {
        await _control.BroadcastCommandAllAsync(start: false);
        _broadcast.StopBroadcast();
        UpdateBroadcastUi();
    }

    /// <summary>Updates the broadcast button caption and the status bar.</summary>
    private void UpdateBroadcastUi()
    {
        tsbBroadcast.Text = _broadcast.IsBroadcasting ? "Stop Broadcast" : "Start Broadcast";
        UpdateStatus();
    }

    /// <summary>Locks every connected student.</summary>
    private async void OnLockAllClick(object? sender, EventArgs e) => await _control.SetLockAllAsync(true);

    /// <summary>Unlocks every connected student.</summary>
    private async void OnUnlockAllClick(object? sender, EventArgs e) => await _control.SetLockAllAsync(false);

    /// <summary>Logs off every connected student after confirmation.</summary>
    private async void OnLogoffAllClick(object? sender, EventArgs e)
    {
        if (_control.ConnectedCount == 0)
        {
            return;
        }

        DialogResult confirm = MessageBox.Show(this,
            $"Log off all {_control.ConnectedCount} connected students?",
            "Confirm log off all", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

        if (confirm == DialogResult.Yes)
        {
            await _control.LogoffAllAsync();
        }
    }

    /// <summary>Updates the status bar text. Safe to call from any thread.</summary>
    private void UpdateStatus()
    {
        if (InvokeRequired)
        {
            BeginInvoke(UpdateStatus);
            return;
        }

        lblConnected.Text = $"Students: {_control.ConnectedCount}";
        lblBroadcast.Text = _broadcast.IsBroadcasting
            ? $"Broadcast: ON ({_broadcast.ClientCount} receiving)"
            : "Broadcast: off";
        lblTeacher.Text = $"Signed in: {_teacherName}";
    }

    /// <summary>Tears down all services when the window closes.</summary>
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        try
        {
            _broadcast.Dispose();
            _control.Dispose();
            _discovery.Dispose();
            Logger.Info("Teacher services stopped.");
        }
        catch (Exception ex)
        {
            Logger.Warn($"Error during shutdown: {ex.Message}");
        }
        base.OnFormClosing(e);
    }
}
