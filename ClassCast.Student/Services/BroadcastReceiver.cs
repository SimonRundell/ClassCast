using System.Net;
using System.Net.Sockets;
using ClassCast.Common.Config;
using ClassCast.Common.Logging;
using ClassCast.Common.Protocol;

namespace ClassCast.Student.Services;

/// <summary>
/// Connects to the Teacher Server broadcast channel (port 45680) and reads
/// length-prefixed JPEG frames, raising <see cref="FrameReceived"/> for each one
/// (specification sections 2.3.3, 5.3). The frames are decoded and displayed by the
/// fullscreen overlay.
/// </summary>
public sealed class BroadcastReceiver : IDisposable
{
    private readonly AppConfig _config;
    private readonly IPAddress _server;
    private TcpClient? _client;
    private CancellationTokenSource? _cts;
    private Task? _readLoop;

    /// <summary>Raised on a background thread for each complete JPEG frame received.</summary>
    public event Action<byte[]>? FrameReceived;

    /// <summary>Raised when the broadcast connection ends.</summary>
    public event Action? Stopped;

    /// <summary>
    /// Initialises the broadcast receiver.
    /// </summary>
    /// <param name="config">Configuration providing the broadcast port.</param>
    /// <param name="server">The Teacher Server IP address to connect to.</param>
    public BroadcastReceiver(AppConfig config, IPAddress server)
    {
        _config = config;
        _server = server;
    }

    /// <summary>Connects to the broadcast channel and begins reading frames.</summary>
    /// <returns>A task that completes once connected (or having failed to connect).</returns>
    public async Task StartAsync()
    {
        try
        {
            _client = new TcpClient { NoDelay = true };
            await _client.ConnectAsync(_server, _config.TcpBroadcastPort).ConfigureAwait(false);
            _cts = new CancellationTokenSource();
            _readLoop = Task.Run(() => ReadLoopAsync(_cts.Token));
            Logger.Info($"Broadcast receiver connected to {_server}:{_config.TcpBroadcastPort}.");
        }
        catch (Exception ex)
        {
            Logger.Warn($"Broadcast connect failed: {ex.Message}");
            Stopped?.Invoke();
        }
    }

    /// <summary>Reads frames until the connection closes or is stopped.</summary>
    private async Task ReadLoopAsync(CancellationToken token)
    {
        NetworkStream stream = _client!.GetStream();
        try
        {
            while (!token.IsCancellationRequested)
            {
                byte[]? frame = await FrameReader.ReadFrameAsync(stream, token).ConfigureAwait(false);
                if (frame is null)
                {
                    break; // server closed the broadcast
                }
                if (frame.Length > 0)
                {
                    FrameReceived?.Invoke(frame);
                }
            }
        }
        catch (OperationCanceledException) { /* stopping */ }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or SocketException or InvalidDataException)
        {
            Logger.Debug($"Broadcast read ended: {ex.Message}");
        }
        finally
        {
            Stopped?.Invoke();
        }
    }

    /// <summary>Stops reading and closes the broadcast connection.</summary>
    public void Stop()
    {
        try { _cts?.Cancel(); } catch { /* ignore */ }
        try { _client?.Close(); } catch { /* ignore */ }
    }

    /// <summary>Releases all resources held by the receiver.</summary>
    public void Dispose()
    {
        Stop();
        _cts?.Dispose();
        _client?.Dispose();
    }
}
