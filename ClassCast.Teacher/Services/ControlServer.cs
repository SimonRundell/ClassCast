using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using ClassCast.Common.Config;
using ClassCast.Common.Logging;
using ClassCast.Common.Network;
using ClassCast.Common.Protocol;

namespace ClassCast.Teacher.Services;

/// <summary>
/// Accepts and manages the persistent per-student TCP control connections
/// (port 45679). Tracks each <see cref="StudentSession"/>, dispatches inbound
/// HELLO / THUMBNAIL / HEARTBEAT / ACK messages, enforces the heartbeat timeout,
/// and exposes commands for sending control messages to one or all students.
/// </summary>
public sealed class ControlServer : IDisposable
{
    private const int HeartbeatTimeoutSeconds = 30;
    private const int HeartbeatIntervalSeconds = 10;

    private readonly AppConfig _config;
    private readonly ConcurrentDictionary<TcpClientManager, StudentSession> _sessions = new();
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _acceptLoop;
    private Task? _maintenanceLoop;

    /// <summary>Raised when a student completes its HELLO handshake and becomes visible.</summary>
    public event Action<StudentSession>? StudentConnected;

    /// <summary>Raised when a known student's state changes (e.g. lock toggled).</summary>
    public event Action<StudentSession>? StudentUpdated;

    /// <summary>Raised when a student disconnects or times out.</summary>
    public event Action<StudentSession>? StudentDisconnected;

    /// <summary>Raised when a THUMBNAIL message arrives, carrying the decoded JPEG bytes.</summary>
    public event Action<StudentSession, byte[]>? ThumbnailReceived;

    /// <summary>Initialises the control server.</summary>
    /// <param name="config">Configuration providing the control port.</param>
    public ControlServer(AppConfig config) => _config = config;

    /// <summary>Gets a snapshot of all sessions that have completed HELLO.</summary>
    public IReadOnlyList<StudentSession> ActiveSessions =>
        _sessions.Values.Where(s => s.HasIdentity).ToList();

    /// <summary>Gets the number of students currently identified and connected.</summary>
    public int ConnectedCount => _sessions.Values.Count(s => s.HasIdentity);

    /// <summary>Starts the TCP accept loop and the heartbeat-maintenance loop.</summary>
    public void Start()
    {
        _listener = new TcpListener(IPAddress.Any, _config.TcpControlPort);
        _listener.Start();
        _cts = new CancellationTokenSource();
        _acceptLoop = Task.Run(() => AcceptLoopAsync(_cts.Token));
        _maintenanceLoop = Task.Run(() => MaintenanceLoopAsync(_cts.Token));
        Logger.Info($"Control server listening on TCP {_config.TcpControlPort}.");
    }

    /// <summary>Accepts inbound control connections until cancelled.</summary>
    private async Task AcceptLoopAsync(CancellationToken token)
    {
        TcpListener listener = _listener!;
        while (!token.IsCancellationRequested)
        {
            try
            {
                TcpClient client = await listener.AcceptTcpClientAsync(token).ConfigureAwait(false);
                RegisterClient(client);
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
                Logger.Warn($"Control accept error: {ex.Message}");
            }
        }
    }

    /// <summary>Wraps an accepted client in a session and wires its events.</summary>
    private void RegisterClient(TcpClient client)
    {
        var manager = new TcpClientManager();
        var session = new StudentSession(manager);
        _sessions[manager] = session;

        manager.MessageReceived += message => OnMessage(session, message);
        manager.Disconnected += () => OnDisconnected(session);

        manager.Attach(client);
        Logger.Debug($"Accepted control connection from {session.RemoteIp}.");
    }

    /// <summary>Handles an inbound control message from a student.</summary>
    private void OnMessage(StudentSession session, ControlMessage message)
    {
        session.LastActivityUtc = DateTime.UtcNow;

        switch (message.Type)
        {
            case MessageType.HELLO:
                session.MachineName = message.MachineName ?? session.RemoteIp;
                session.AdUser = message.AdUser ?? "(unknown)";
                session.AdDomain = message.AdDomain ?? string.Empty;
                session.ScreenWidth = message.ScreenWidth ?? 0;
                session.ScreenHeight = message.ScreenHeight ?? 0;
                bool firstHello = !session.HasIdentity;
                session.HasIdentity = true;
                Logger.Info($"Student HELLO: {session.MachineName} / {session.AdUser} ({session.RemoteIp}).");
                if (firstHello)
                {
                    StudentConnected?.Invoke(session);
                }
                else
                {
                    StudentUpdated?.Invoke(session);
                }
                break;

            case MessageType.THUMBNAIL:
                if (!string.IsNullOrEmpty(message.Thumbnail))
                {
                    try
                    {
                        byte[] jpeg = Convert.FromBase64String(message.Thumbnail);
                        ThumbnailReceived?.Invoke(session, jpeg);
                    }
                    catch (FormatException ex)
                    {
                        Logger.Warn($"Bad thumbnail base64 from {session.MachineName}: {ex.Message}");
                    }
                }
                break;

            case MessageType.HEARTBEAT:
                // Activity timestamp already refreshed above.
                break;

            case MessageType.ACK:
                Logger.Debug($"ACK from {session.MachineName} for {message.AckType}.");
                break;

            default:
                Logger.Debug($"Ignoring unexpected '{message.Type}' from {session.MachineName}.");
                break;
        }
    }

    /// <summary>Removes a session and notifies listeners when a connection drops.</summary>
    private void OnDisconnected(StudentSession session)
    {
        if (_sessions.TryRemove(session.Connection, out _))
        {
            session.Connection.Dispose();
            Logger.Info($"Student disconnected: {session.MachineName} ({session.RemoteIp}).");
            if (session.HasIdentity)
            {
                StudentDisconnected?.Invoke(session);
            }
        }
    }

    /// <summary>Periodically sends heartbeats and prunes timed-out sessions.</summary>
    private async Task MaintenanceLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(HeartbeatIntervalSeconds), token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            DateTime now = DateTime.UtcNow;
            foreach (StudentSession session in _sessions.Values)
            {
                // Drop students that have gone silent.
                if ((now - session.LastActivityUtc).TotalSeconds > HeartbeatTimeoutSeconds)
                {
                    Logger.Warn($"Heartbeat timeout: {session.MachineName} ({session.RemoteIp}).");
                    session.Connection.Dispose(); // triggers Disconnected -> OnDisconnected
                    continue;
                }

                // Send our own heartbeat so the student can detect a dead teacher too.
                _ = SendAsync(session, ControlMessage.Heartbeat());
            }
        }
    }

    /// <summary>Sends a message to a single student, stamping it with a sequence number.</summary>
    /// <param name="session">The target student session.</param>
    /// <param name="message">The message to send.</param>
    /// <returns><c>true</c> if the message was written to the socket.</returns>
    /// <remarks>
    /// The sequence number must be assigned and the frame written to the socket as one
    /// atomic step, otherwise a command stamped on the UI thread could be overtaken on
    /// the wire by a heartbeat stamped on the maintenance thread; the student's replay
    /// guard would then discard the (now lower-sequence) command. The per-session
    /// <see cref="StudentSession.SendGate"/> guarantees wire order matches sequence order.
    /// </remarks>
    public async Task<bool> SendAsync(StudentSession session, ControlMessage message)
    {
        await session.SendGate.WaitAsync().ConfigureAwait(false);
        try
        {
            message.Seq = session.NextSequence();
            return await session.Connection.SendAsync(message).ConfigureAwait(false);
        }
        finally
        {
            session.SendGate.Release();
        }
    }

    /// <summary>Sends a message produced per-student to every identified student.</summary>
    /// <param name="factory">Factory creating a fresh message for each session.</param>
    public async Task SendToAllAsync(Func<StudentSession, ControlMessage> factory)
    {
        foreach (StudentSession session in ActiveSessions)
        {
            await SendAsync(session, factory(session)).ConfigureAwait(false);
        }
    }

    /// <summary>Locks or unlocks a single student's input.</summary>
    /// <param name="session">The target student.</param>
    /// <param name="locked"><c>true</c> to lock; <c>false</c> to unlock.</param>
    public async Task SetLockAsync(StudentSession session, bool locked)
    {
        if (await SendAsync(session, ControlMessage.Lock(locked)).ConfigureAwait(false))
        {
            session.IsLocked = locked;
            StudentUpdated?.Invoke(session);
        }
    }

    /// <summary>Locks or unlocks every connected student.</summary>
    /// <param name="locked"><c>true</c> to lock all; <c>false</c> to unlock all.</param>
    public async Task SetLockAllAsync(bool locked)
    {
        foreach (StudentSession session in ActiveSessions)
        {
            await SetLockAsync(session, locked).ConfigureAwait(false);
        }
    }

    /// <summary>Logs off a single student.</summary>
    /// <param name="session">The target student.</param>
    public Task LogoffAsync(StudentSession session) => SendAsync(session, ControlMessage.Logoff());

    /// <summary>Logs off every connected student.</summary>
    public Task LogoffAllAsync() => SendToAllAsync(_ => ControlMessage.Logoff());

    /// <summary>Tells all students to begin or end displaying the broadcast.</summary>
    /// <param name="start"><c>true</c> to send BROADCAST_START; <c>false</c> for BROADCAST_STOP.</param>
    public Task BroadcastCommandAllAsync(bool start) =>
        SendToAllAsync(_ => start ? ControlMessage.BroadcastStart() : ControlMessage.BroadcastStop());

    /// <summary>Stops the server and disposes all sessions.</summary>
    public void Dispose()
    {
        try { _cts?.Cancel(); } catch { /* ignore */ }
        try { _listener?.Stop(); } catch { /* ignore */ }

        foreach (StudentSession session in _sessions.Values)
        {
            session.Connection.Dispose();
        }
        _sessions.Clear();
        _cts?.Dispose();
    }
}
