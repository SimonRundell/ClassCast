using System.Net.Sockets;
using ClassCast.Common.Logging;
using ClassCast.Common.Protocol;

namespace ClassCast.Common.Network;

/// <summary>
/// Wraps a single TCP connection that carries length-prefixed
/// <see cref="ControlMessage"/> frames. It owns a background receive loop that
/// raises <see cref="MessageReceived"/> for each inbound message and
/// <see cref="Disconnected"/> when the link drops, and provides thread-safe
/// <see cref="SendAsync"/>.
/// </summary>
/// <remarks>
/// Used by the Student control client and, on the Teacher side, by each accepted
/// control connection. The class can either dial out (<see cref="ConnectAsync"/>)
/// or adopt an already-accepted <see cref="TcpClient"/>.
/// </remarks>
public sealed class TcpClientManager : IDisposable
{
    private readonly SemaphoreSlim _sendGate = new(1, 1);
    private TcpClient? _client;
    private NetworkStream? _stream;
    private CancellationTokenSource? _cts;
    private Task? _receiveLoop;
    private volatile bool _disposed;

    /// <summary>Raised on a background thread for each inbound control message.</summary>
    public event Action<ControlMessage>? MessageReceived;

    /// <summary>Raised once when the connection is detected as closed or faulted.</summary>
    public event Action? Disconnected;

    /// <summary>Gets the remote endpoint address as text, or "?" if unavailable.</summary>
    public string RemoteAddress { get; private set; } = "?";

    /// <summary>Gets a value indicating whether the underlying socket is connected.</summary>
    public bool IsConnected => _client is { Connected: true };

    /// <summary>
    /// Adopts an already-connected <see cref="TcpClient"/> (e.g. one returned by
    /// <c>TcpListener.AcceptTcpClient</c>) and begins receiving.
    /// </summary>
    /// <param name="client">The connected client to manage.</param>
    public void Attach(TcpClient client)
    {
        _client = client;
        _client.NoDelay = true;
        _stream = client.GetStream();
        RemoteAddress = client.Client.RemoteEndPoint?.ToString() ?? "?";
        StartReceiveLoop();
    }

    /// <summary>
    /// Connects to a remote host/port and begins receiving on success.
    /// </summary>
    /// <param name="host">The server host or IP address.</param>
    /// <param name="port">The server TCP port.</param>
    /// <param name="cancellationToken">Token used to cancel the connect attempt.</param>
    /// <returns>A task that completes when connected and the receive loop is running.</returns>
    public async Task ConnectAsync(string host, int port, CancellationToken cancellationToken = default)
    {
        var client = new TcpClient { NoDelay = true };
        await client.ConnectAsync(host, port, cancellationToken).ConfigureAwait(false);
        Attach(client);
    }

    /// <summary>Starts the background receive loop.</summary>
    private void StartReceiveLoop()
    {
        _cts = new CancellationTokenSource();
        _receiveLoop = Task.Run(() => ReceiveLoopAsync(_cts.Token));
    }

    /// <summary>
    /// Sends a control message as a single length-prefixed frame. Writes are
    /// serialised so multiple threads may send concurrently.
    /// </summary>
    /// <param name="message">The message to send.</param>
    /// <param name="cancellationToken">Token used to cancel the send.</param>
    /// <returns><c>true</c> if the message was written; <c>false</c> if the link was down.</returns>
    public async Task<bool> SendAsync(ControlMessage message, CancellationToken cancellationToken = default)
    {
        NetworkStream? stream = _stream;
        if (stream is null || !IsConnected)
        {
            return false;
        }

        await _sendGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await FrameWriter.WriteMessageAsync(stream, message, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or SocketException)
        {
            Logger.Debug($"Send to {RemoteAddress} failed: {ex.Message}");
            HandleDisconnect();
            return false;
        }
        finally
        {
            _sendGate.Release();
        }
    }

    /// <summary>Background loop reading framed messages until the connection closes.</summary>
    private async Task ReceiveLoopAsync(CancellationToken token)
    {
        NetworkStream? stream = _stream;
        if (stream is null)
        {
            return;
        }

        try
        {
            while (!token.IsCancellationRequested)
            {
                ControlMessage? message;
                try
                {
                    message = await FrameReader.ReadMessageAsync(stream, token).ConfigureAwait(false);
                }
                catch (InvalidDataException ex)
                {
                    Logger.Warn($"Malformed frame from {RemoteAddress}: {ex.Message}");
                    break;
                }

                if (message is null)
                {
                    break; // Clean disconnect.
                }

                try
                {
                    MessageReceived?.Invoke(message);
                }
                catch (Exception ex)
                {
                    Logger.Error($"Message handler for {RemoteAddress} threw.", ex);
                }
            }
        }
        catch (OperationCanceledException) { /* shutting down */ }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or SocketException)
        {
            Logger.Debug($"Receive from {RemoteAddress} ended: {ex.Message}");
        }
        finally
        {
            HandleDisconnect();
        }
    }

    private int _disconnectRaised;

    /// <summary>Raises <see cref="Disconnected"/> exactly once.</summary>
    private void HandleDisconnect()
    {
        if (Interlocked.Exchange(ref _disconnectRaised, 1) == 0)
        {
            try { Disconnected?.Invoke(); }
            catch (Exception ex) { Logger.Error("Disconnected handler threw.", ex); }
        }
    }

    /// <summary>Closes the connection and stops the receive loop.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;

        try { _cts?.Cancel(); } catch { /* ignore */ }
        try { _stream?.Dispose(); } catch { /* ignore */ }
        try { _client?.Close(); } catch { /* ignore */ }

        _cts?.Dispose();
        _sendGate.Dispose();
    }
}
