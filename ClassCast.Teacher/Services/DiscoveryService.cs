using System.Net;
using System.Net.Sockets;
using ClassCast.Common.Config;
using ClassCast.Common.Logging;
using ClassCast.Common.Network;
using ClassCast.Common.Protocol;

namespace ClassCast.Teacher.Services;

/// <summary>
/// Listens for Student Client UDP discovery beacons on the configured port and
/// replies to each with a unicast ACK (specification section 2.3.1). Receiving an
/// ACK tells the student it may open its TCP control connection.
/// </summary>
public sealed class DiscoveryService : IDisposable
{
    private readonly AppConfig _config;
    private readonly string _serverVersion;
    private UdpClient? _listener;
    private CancellationTokenSource? _cts;
    private Task? _loop;

    /// <summary>
    /// Initialises the discovery service.
    /// </summary>
    /// <param name="config">Configuration providing the UDP discovery port.</param>
    /// <param name="serverVersion">Version string returned in the ACK reply.</param>
    public DiscoveryService(AppConfig config, string serverVersion)
    {
        _config = config;
        _serverVersion = serverVersion;
    }

    /// <summary>Starts listening for beacons on a background task.</summary>
    public void Start()
    {
        _listener = UdpBeacon.CreateListener(_config.UdpDiscoveryPort);
        _cts = new CancellationTokenSource();
        _loop = Task.Run(() => ListenLoopAsync(_cts.Token));
        Logger.Info($"Discovery service listening on UDP {_config.UdpDiscoveryPort}.");
    }

    /// <summary>Receives beacons and replies with a unicast ACK to each valid one.</summary>
    private async Task ListenLoopAsync(CancellationToken token)
    {
        UdpClient listener = _listener!;
        while (!token.IsCancellationRequested)
        {
            try
            {
                var received = await UdpBeacon.ReceiveAsync(listener, token).ConfigureAwait(false);
                if (received is not { } r || r.Message.Type != MessageType.BEACON)
                {
                    continue;
                }

                Logger.Debug($"Beacon from {r.From.Address} ({r.Message.MachineName}/{r.Message.AdUser}); sending ACK.");
                var ackTarget = new IPEndPoint(r.From.Address, _config.UdpDiscoveryPort);
                await UdpBeacon.SendAsync(listener, ControlMessage.UdpAck(_serverVersion), ackTarget).ConfigureAwait(false);
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
                Logger.Warn($"Discovery socket error: {ex.Message}");
            }
        }
    }

    /// <summary>Stops listening and releases the UDP socket.</summary>
    public void Dispose()
    {
        try { _cts?.Cancel(); } catch { /* ignore */ }
        try { _listener?.Dispose(); } catch { /* ignore */ }
        _cts?.Dispose();
    }
}
