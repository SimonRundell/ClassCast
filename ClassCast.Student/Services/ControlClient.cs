using System.Net;
using ClassCast.Common.Config;
using ClassCast.Common.Logging;
using ClassCast.Common.Network;
using ClassCast.Common.Protocol;

namespace ClassCast.Student.Services;

/// <summary>
/// Maintains the persistent TCP control connection to the Teacher Server
/// (port 45679). Sends HELLO and periodic heartbeats, enforces replay protection by
/// discarding out-of-order messages, restricts traffic to the single trusted server
/// IP, and raises events for each command (specification sections 2.3.2, 3.1, 12).
/// </summary>
public sealed class ControlClient : IDisposable
{
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(10);

    private readonly AppConfig _config;
    private readonly string _machineName;
    private readonly string _adDomain;
    private readonly string _adUser;
    private readonly int _screenWidth;
    private readonly int _screenHeight;

    private readonly TcpClientManager _manager = new();
    private readonly IPAddress _trustedServer;
    private CancellationTokenSource? _cts;
    private Task? _heartbeatLoop;
    private long _lastServerSeq;

    /// <summary>Raised when a LOCK command arrives, with the requested lock state.</summary>
    public event Action<bool>? LockReceived;

    /// <summary>Raised when a BROADCAST_START command arrives.</summary>
    public event Action? BroadcastStartReceived;

    /// <summary>Raised when a BROADCAST_STOP command arrives.</summary>
    public event Action? BroadcastStopReceived;

    /// <summary>Raised when a LOGOFF command arrives.</summary>
    public event Action? LogoffReceived;

    /// <summary>Raised when the control connection drops.</summary>
    public event Action? Disconnected;

    /// <summary>Gets the IP address of the trusted Teacher Server.</summary>
    public IPAddress ServerAddress => _trustedServer;

    /// <summary>
    /// Initialises the control client for a specific, already-discovered server.
    /// </summary>
    /// <param name="config">Configuration providing the control port.</param>
    /// <param name="server">The trusted Teacher Server IP address.</param>
    /// <param name="machineName">This PC's machine name.</param>
    /// <param name="adDomain">The logged-in AD domain.</param>
    /// <param name="adUser">The logged-in AD username.</param>
    /// <param name="screenWidth">Primary screen width in pixels.</param>
    /// <param name="screenHeight">Primary screen height in pixels.</param>
    public ControlClient(AppConfig config, IPAddress server, string machineName,
                         string adDomain, string adUser, int screenWidth, int screenHeight)
    {
        _config = config;
        _trustedServer = server;
        _machineName = machineName;
        _adDomain = adDomain;
        _adUser = adUser;
        _screenWidth = screenWidth;
        _screenHeight = screenHeight;

        _manager.MessageReceived += OnMessage;
        _manager.Disconnected += () => Disconnected?.Invoke();
    }

    /// <summary>
    /// Connects to the trusted server, sends HELLO and starts the heartbeat loop.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the connect attempt.</param>
    /// <returns><c>true</c> if the connection was established; otherwise <c>false</c>.</returns>
    public async Task<bool> ConnectAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _manager.ConnectAsync(_trustedServer.ToString(), _config.TcpControlPort, cancellationToken).ConfigureAwait(false);
            await _manager.SendAsync(
                ControlMessage.Hello(_machineName, _adDomain, _adUser, _screenWidth, _screenHeight),
                cancellationToken).ConfigureAwait(false);

            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _heartbeatLoop = Task.Run(() => HeartbeatLoopAsync(_cts.Token));
            Logger.Info($"Control channel connected to {_trustedServer}.");
            return true;
        }
        catch (Exception ex)
        {
            Logger.Warn($"Control connect to {_trustedServer} failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>Sends a THUMBNAIL message carrying a base64-encoded JPEG.</summary>
    /// <param name="base64Jpeg">The base64-encoded JPEG image.</param>
    /// <returns><c>true</c> if the message was sent.</returns>
    public Task<bool> SendThumbnailAsync(string base64Jpeg)
        => _manager.SendAsync(ControlMessage.ThumbnailMessage(base64Jpeg));

    /// <summary>Dispatches an inbound control message after trust and replay checks.</summary>
    private void OnMessage(ControlMessage message)
    {
        // Replay / out-of-order protection: server stamps an increasing sequence on
        // every command. Discard anything not strictly newer (0 = unsequenced keepalive).
        if (message.Seq != 0)
        {
            if (message.Seq <= _lastServerSeq)
            {
                Logger.Warn($"Discarding out-of-order message seq={message.Seq} (last={_lastServerSeq}).");
                return;
            }
            _lastServerSeq = message.Seq;
        }

        switch (message.Type)
        {
            case MessageType.LOCK:
                bool locked = message.Locked ?? false;
                Logger.Info($"LOCK command: locked={locked}.");
                LockReceived?.Invoke(locked);
                _ = _manager.SendAsync(ControlMessage.ControlAck(MessageType.LOCK));
                break;

            case MessageType.BROADCAST_START:
                Logger.Info("BROADCAST_START command.");
                BroadcastStartReceived?.Invoke();
                _ = _manager.SendAsync(ControlMessage.ControlAck(MessageType.BROADCAST_START));
                break;

            case MessageType.BROADCAST_STOP:
                Logger.Info("BROADCAST_STOP command.");
                BroadcastStopReceived?.Invoke();
                _ = _manager.SendAsync(ControlMessage.ControlAck(MessageType.BROADCAST_STOP));
                break;

            case MessageType.LOGOFF:
                Logger.Info("LOGOFF command.");
                // ACK first so the teacher sees confirmation before we log off.
                _ = _manager.SendAsync(ControlMessage.ControlAck(MessageType.LOGOFF));
                LogoffReceived?.Invoke();
                break;

            case MessageType.HEARTBEAT:
                // Server keepalive; nothing to do.
                break;

            default:
                Logger.Debug($"Ignoring unexpected '{message.Type}' from server.");
                break;
        }
    }

    /// <summary>Sends a heartbeat to the server every ten seconds.</summary>
    private async Task HeartbeatLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(HeartbeatInterval, token).ConfigureAwait(false);
                await _manager.SendAsync(ControlMessage.Heartbeat(), token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Logger.Debug($"Heartbeat send failed: {ex.Message}");
                break;
            }
        }
    }

    /// <summary>Closes the control connection and stops the heartbeat loop.</summary>
    public void Dispose()
    {
        try { _cts?.Cancel(); } catch { /* ignore */ }
        _manager.Dispose();
        _cts?.Dispose();
    }
}
