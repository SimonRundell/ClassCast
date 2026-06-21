using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Windows.Forms;
using ClassCast.Common.Config;
using ClassCast.Common.Logging;
using ClassCast.Common.Media;
using ClassCast.Common.Protocol;

namespace ClassCast.Teacher.Services;

/// <summary>
/// Hosts the one-to-many broadcast video channel (TCP 45680). A persistent listener
/// accepts a connection from each student that receives BROADCAST_START; while a
/// broadcast is active, the teacher's primary screen is captured, scaled and encoded
/// to MJPEG by <see cref="FFmpegWrapper"/>, and each encoded frame is fanned out to
/// every connected client as a length-prefixed frame (specification sections 4.2&#8211;4.4).
/// </summary>
public sealed class BroadcastServer : IDisposable
{
    private readonly AppConfig _config;
    private readonly ConcurrentDictionary<BroadcastClient, byte> _clients = new();

    private TcpListener? _listener;
    private CancellationTokenSource? _serverCts;
    private Task? _acceptLoop;

    private FFmpegWrapper? _encoder;
    private CancellationTokenSource? _captureCts;
    private Task? _captureLoop;
    private volatile bool _isBroadcasting;
    private Rectangle _captureBounds;
    private int _captureFps = 15;

    /// <summary>Raised whenever the number of connected broadcast clients changes.</summary>
    public event Action<int>? ClientCountChanged;

    /// <summary>Initialises the broadcast server.</summary>
    /// <param name="config">Configuration providing ports, frame size and ffmpeg path.</param>
    public BroadcastServer(AppConfig config) => _config = config;

    /// <summary>Gets a value indicating whether a broadcast is currently active.</summary>
    public bool IsBroadcasting => _isBroadcasting;

    /// <summary>Gets the number of currently connected broadcast clients.</summary>
    public int ClientCount => _clients.Count;

    /// <summary>Starts the broadcast TCP listener (clients may connect at any time).</summary>
    public void Start()
    {
        _listener = new TcpListener(IPAddress.Any, _config.TcpBroadcastPort);
        _listener.Start();
        _serverCts = new CancellationTokenSource();
        _acceptLoop = Task.Run(() => AcceptLoopAsync(_serverCts.Token));
        Logger.Info($"Broadcast server listening on TCP {_config.TcpBroadcastPort}.");
    }

    /// <summary>Accepts broadcast client connections until cancelled.</summary>
    private async Task AcceptLoopAsync(CancellationToken token)
    {
        TcpListener listener = _listener!;
        while (!token.IsCancellationRequested)
        {
            try
            {
                TcpClient tcp = await listener.AcceptTcpClientAsync(token).ConfigureAwait(false);
                tcp.NoDelay = true;
                var client = new BroadcastClient(tcp);
                _clients[client] = 0;
                Logger.Info($"Broadcast client connected: {client.Address} (total {_clients.Count}).");
                ClientCountChanged?.Invoke(_clients.Count);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex) when (ex is SocketException or ObjectDisposedException)
            {
                if (token.IsCancellationRequested)
                {
                    break;
                }
                Logger.Warn($"Broadcast accept error: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Begins capturing and encoding the given screen region at the chosen quality.
    /// Call after instructing students (via the control channel) to connect.
    /// </summary>
    /// <param name="screenBounds">Virtual-desktop bounds of the screen to broadcast.</param>
    /// <param name="quality">The selected resolution / frame rate / compression preset.</param>
    public void StartBroadcast(Rectangle screenBounds, BroadcastQuality quality)
    {
        if (_isBroadcasting)
        {
            return;
        }

        _captureBounds = screenBounds;
        _captureFps = quality.Fps;

        _encoder = new FFmpegWrapper(
            _config.ResolveFfmpegPath(),
            screenBounds.Width, screenBounds.Height,
            quality.Width, quality.Height,
            quality.Fps,
            quality.JpegQuality);
        _encoder.FrameReady += OnEncodedFrame;
        _encoder.Start();

        _captureCts = new CancellationTokenSource();
        _captureLoop = Task.Run(() => CaptureLoopAsync(_captureCts.Token));
        _isBroadcasting = true;
        Logger.Info("Broadcast started.");
    }

    /// <summary>Stops capturing/encoding and disconnects all broadcast clients.</summary>
    public void StopBroadcast()
    {
        if (!_isBroadcasting)
        {
            return;
        }
        _isBroadcasting = false;

        try { _captureCts?.Cancel(); } catch { /* ignore */ }
        try { _captureLoop?.Wait(2000); } catch { /* ignore */ }

        if (_encoder is not null)
        {
            _encoder.FrameReady -= OnEncodedFrame;
            _encoder.Dispose();
            _encoder = null;
        }

        foreach (BroadcastClient client in _clients.Keys)
        {
            RemoveClient(client);
        }

        _captureCts?.Dispose();
        _captureCts = null;
        Logger.Info("Broadcast stopped.");
    }

    /// <summary>Captures and feeds raw BGR24 frames to the encoder at the configured fps.</summary>
    private async Task CaptureLoopAsync(CancellationToken token)
    {
        int intervalMs = Math.Max(1, 1000 / Math.Max(1, _captureFps));
        var sw = new Stopwatch();

        while (!token.IsCancellationRequested)
        {
            sw.Restart();
            try
            {
                byte[] frame = ScreenCapture.CaptureScreenBgr24(_captureBounds, out _, out _);
                FFmpegWrapper? encoder = _encoder;
                if (encoder is not null)
                {
                    await encoder.WriteFrameAsync(frame, token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Logger.Warn($"Capture loop error: {ex.Message}");
            }

            int remaining = intervalMs - (int)sw.ElapsedMilliseconds;
            if (remaining > 0)
            {
                try { await Task.Delay(remaining, token).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
            }
        }
    }

    /// <summary>Fans an encoded JPEG frame out to every connected broadcast client.</summary>
    private void OnEncodedFrame(byte[] jpeg)
    {
        foreach (BroadcastClient client in _clients.Keys)
        {
            // Fire-and-forget per client; slow clients drop frames rather than stall others.
            _ = SendFrameAsync(client, jpeg);
        }
    }

    /// <summary>Writes one frame to a client, dropping it if a prior write is still in flight.</summary>
    private async Task SendFrameAsync(BroadcastClient client, byte[] frame)
    {
        // Skip this frame for the client if it is still busy with the previous one.
        if (!await client.Gate.WaitAsync(0).ConfigureAwait(false))
        {
            return;
        }

        try
        {
            await FrameWriter.WriteFrameAsync(client.Stream, frame).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or SocketException or InvalidOperationException)
        {
            Logger.Debug($"Broadcast client {client.Address} dropped: {ex.Message}");
            RemoveClient(client);
        }
        finally
        {
            client.Gate.Release();
        }
    }

    /// <summary>Removes and disposes a broadcast client, notifying listeners.</summary>
    private void RemoveClient(BroadcastClient client)
    {
        if (_clients.TryRemove(client, out _))
        {
            client.Dispose();
            ClientCountChanged?.Invoke(_clients.Count);
        }
    }

    /// <summary>Stops everything and releases all resources.</summary>
    public void Dispose()
    {
        StopBroadcast();
        try { _serverCts?.Cancel(); } catch { /* ignore */ }
        try { _listener?.Stop(); } catch { /* ignore */ }

        foreach (BroadcastClient client in _clients.Keys)
        {
            client.Dispose();
        }
        _clients.Clear();
        _serverCts?.Dispose();
    }

    /// <summary>A single connected broadcast client and its write-serialisation gate.</summary>
    private sealed class BroadcastClient : IDisposable
    {
        private readonly TcpClient _tcp;

        /// <summary>The network stream used to write frames to the client.</summary>
        public NetworkStream Stream { get; }

        /// <summary>Serialises writes and lets the server drop frames for a busy client.</summary>
        public SemaphoreSlim Gate { get; } = new(1, 1);

        /// <summary>The client's remote endpoint as text.</summary>
        public string Address { get; }

        /// <summary>Wraps an accepted broadcast client connection.</summary>
        /// <param name="tcp">The accepted TCP client.</param>
        public BroadcastClient(TcpClient tcp)
        {
            _tcp = tcp;
            Stream = tcp.GetStream();
            Address = tcp.Client.RemoteEndPoint?.ToString() ?? "?";
        }

        /// <summary>Closes the connection and releases resources.</summary>
        public void Dispose()
        {
            try { Stream.Dispose(); } catch { /* ignore */ }
            try { _tcp.Close(); } catch { /* ignore */ }
            Gate.Dispose();
        }
    }
}
