using System.Diagnostics;
using System.Net;
using System.Security.Principal;
using ClassCast.Common;
using ClassCast.Common.Config;
using ClassCast.Common.Logging;
using ClassCast.Student.Forms;

namespace ClassCast.Student.Services;

/// <summary>
/// Top-level coordinator for the Student Client. Runs a supervised loop that
/// discovers the Teacher Server, maintains the control connection, and reacts to
/// commands by toggling input lock, showing/hiding the broadcast overlay, suspending
/// thumbnails and performing logoff. When the control connection drops it resumes
/// beaconing (specification sections 2.3, 5.x, 6).
/// </summary>
public sealed class StudentClient : IDisposable
{
    private readonly AppConfig _config;
    private readonly string _version;
    private readonly InputLocker _inputLocker;
    private readonly BroadcastOverlay _overlay;

    private readonly string _machineName;
    private readonly string _adDomain;
    private readonly string _adUser;
    private readonly int _screenWidth;
    private readonly int _screenHeight;

    private CancellationTokenSource? _runCts;
    private Task? _runTask;

    private ControlClient? _control;
    private ThumbnailService? _thumbnails;
    private BroadcastReceiver? _receiver;
    private TaskCompletionSource? _disconnected;

    private bool _explicitLock;
    private bool _broadcasting;

    /// <summary>Gets a short status string for display in the tray tooltip/menu.</summary>
    public string Status { get; private set; } = "Starting...";

    /// <summary>
    /// Initialises the coordinator and gathers local identity (machine name, AD user,
    /// primary screen size).
    /// </summary>
    /// <param name="config">Application configuration.</param>
    /// <param name="version">Client version string.</param>
    /// <param name="inputLocker">The shared input locker (hooks already started).</param>
    /// <param name="overlay">The pre-created broadcast overlay form.</param>
    public StudentClient(AppConfig config, string version, InputLocker inputLocker, BroadcastOverlay overlay)
    {
        _config = config;
        _version = version;
        _inputLocker = inputLocker;
        _overlay = overlay;

        _machineName = Environment.MachineName;
        (_adDomain, _adUser) = ResolveIdentity();

        Rectangle bounds = Screen.PrimaryScreen?.Bounds ?? new Rectangle(0, 0, 1920, 1080);
        _screenWidth = bounds.Width;
        _screenHeight = bounds.Height;
    }

    /// <summary>
    /// Reads the current Windows identity and splits it into domain and username.
    /// </summary>
    /// <returns>A tuple of (domain, username).</returns>
    private static (string Domain, string User) ResolveIdentity()
    {
        try
        {
            string full = WindowsIdentity.GetCurrent().Name; // DOMAIN\username
            int slash = full.IndexOf('\\');
            return slash > 0
                ? (full[..slash], full[(slash + 1)..])
                : (string.Empty, full);
        }
        catch
        {
            return (string.Empty, Environment.UserName);
        }
    }

    /// <summary>Starts the supervised discovery/connect loop on a background task.</summary>
    public void Start()
    {
        _runCts = new CancellationTokenSource();
        _runTask = Task.Run(() => RunLoopAsync(_runCts.Token));
    }

    /// <summary>The supervised loop: discover, connect, serve until dropped, then repeat.</summary>
    private async Task RunLoopAsync(CancellationToken token)
    {
        var beacon = new BeaconService(_config, _machineName, _adUser, _version);

        while (!token.IsCancellationRequested)
        {
            Status = "Searching for teacher...";
            IPAddress? server = await beacon.DiscoverServerAsync(token).ConfigureAwait(false);
            if (server is null)
            {
                break; // cancelled
            }

            await ServeSessionAsync(server, token).ConfigureAwait(false);
            // Session ended; tidy up and beacon again.
            await TeardownSessionAsync().ConfigureAwait(false);
        }
    }

    /// <summary>Connects to a discovered server and runs until the connection drops.</summary>
    private async Task ServeSessionAsync(IPAddress server, CancellationToken token)
    {
        _disconnected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        _control = new ControlClient(_config, server, _machineName, _adDomain, _adUser, _screenWidth, _screenHeight);
        _control.LockReceived += OnLockReceived;
        _control.BroadcastStartReceived += OnBroadcastStart;
        _control.BroadcastStopReceived += OnBroadcastStop;
        _control.LogoffReceived += OnLogoff;
        _control.Disconnected += () => _disconnected?.TrySetResult();

        if (!await _control.ConnectAsync(token).ConfigureAwait(false))
        {
            // Brief back-off before re-beaconing.
            try { await Task.Delay(TimeSpan.FromSeconds(3), token).ConfigureAwait(false); }
            catch (OperationCanceledException) { /* exiting */ }
            return;
        }

        Status = $"Connected to {server}";
        _thumbnails = new ThumbnailService(_config, b64 => _control!.SendThumbnailAsync(b64));
        _thumbnails.Start();

        // Wait until the control connection drops or we are asked to stop.
        using (token.Register(() => _disconnected?.TrySetResult()))
        {
            await _disconnected.Task.ConfigureAwait(false);
        }
    }

    /// <summary>Tears down all per-session services and restores normal desktop state.</summary>
    private Task TeardownSessionAsync()
    {
        _broadcasting = false;
        _explicitLock = false;

        _receiver?.Dispose();
        _receiver = null;

        _overlay.HideOverlay();
        _inputLocker.SetLocked(false);

        _thumbnails?.Dispose();
        _thumbnails = null;

        _control?.Dispose();
        _control = null;

        Status = "Disconnected";
        return Task.CompletedTask;
    }

    // ----- Command handlers ------------------------------------------------

    /// <summary>Applies an explicit LOCK command.</summary>
    private void OnLockReceived(bool locked)
    {
        _explicitLock = locked;
        ApplyLockState();
    }

    /// <summary>Begins displaying the broadcast and locks input.</summary>
    private void OnBroadcastStart()
    {
        if (_broadcasting || _control is null)
        {
            return;
        }
        _broadcasting = true;

        _receiver = new BroadcastReceiver(_config, _control.ServerAddress);
        _receiver.FrameReceived += _overlay.RenderFrame;
        _receiver.Stopped += OnReceiverStopped;
        _ = _receiver.StartAsync();

        _overlay.ShowOverlay();
        _thumbnails?.SetSuspended(true);
        ApplyLockState();
        Status = "Receiving broadcast";
    }

    /// <summary>Stops displaying the broadcast and restores the desktop.</summary>
    private void OnBroadcastStop()
    {
        if (!_broadcasting)
        {
            return;
        }
        _broadcasting = false;

        _receiver?.Dispose();
        _receiver = null;

        _overlay.HideOverlay();
        _thumbnails?.SetSuspended(false);
        ApplyLockState();
        Status = _control is null ? "Disconnected" : $"Connected to {_control.ServerAddress}";
    }

    /// <summary>Handles the broadcast connection closing from the server side.</summary>
    private void OnReceiverStopped()
    {
        // If the server tore down the broadcast socket without a STOP command, restore.
        if (_broadcasting)
        {
            OnBroadcastStop();
        }
    }

    /// <summary>Performs a forced Windows logoff in response to a LOGOFF command.</summary>
    private void OnLogoff()
    {
        try
        {
            Logger.Info("Executing forced logoff (shutdown /l /f).");
            Process.Start(new ProcessStartInfo("shutdown.exe", "/l /f") { CreateNoWindow = true, UseShellExecute = false });
        }
        catch (Exception ex)
        {
            Logger.Error("Logoff failed.", ex);
        }
    }

    /// <summary>Locks input if either an explicit lock or a broadcast is active.</summary>
    private void ApplyLockState() => _inputLocker.SetLocked(_explicitLock || _broadcasting);

    /// <summary>Stops the loop and tears down everything.</summary>
    public void Dispose()
    {
        try { _runCts?.Cancel(); } catch { /* ignore */ }
        _disconnected?.TrySetResult();
        try { _runTask?.Wait(2000); } catch { /* ignore */ }

        _receiver?.Dispose();
        _thumbnails?.Dispose();
        _control?.Dispose();
        _runCts?.Dispose();
    }
}
