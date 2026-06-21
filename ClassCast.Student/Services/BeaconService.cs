using System.Net;
using System.Net.Sockets;
using ClassCast.Common.Config;
using ClassCast.Common.Logging;
using ClassCast.Common.Network;
using ClassCast.Common.Protocol;

namespace ClassCast.Student.Services;

/// <summary>
/// Sends UDP discovery beacons to the subnet every five seconds until the Teacher
/// Server replies with an ACK, then yields the server's IP address so the control
/// connection can be opened (specification section 2.3.1).
/// </summary>
public sealed class BeaconService
{
    private static readonly TimeSpan BeaconInterval = TimeSpan.FromSeconds(5);

    private readonly AppConfig _config;
    private readonly string _machineName;
    private readonly string _adUser;
    private readonly string _version;

    /// <summary>
    /// Initialises the beacon service.
    /// </summary>
    /// <param name="config">Configuration providing the discovery port.</param>
    /// <param name="machineName">This PC's machine name (sent in the beacon).</param>
    /// <param name="adUser">The logged-in AD username (sent in the beacon).</param>
    /// <param name="version">The client version string.</param>
    public BeaconService(AppConfig config, string machineName, string adUser, string version)
    {
        _config = config;
        _machineName = machineName;
        _adUser = adUser;
        _version = version;
    }

    /// <summary>
    /// Broadcasts beacons until an ACK is received or the operation is cancelled.
    /// </summary>
    /// <param name="cancellationToken">Token used to abort beaconing.</param>
    /// <returns>
    /// The IP address of the Teacher Server that acknowledged, or <c>null</c> if the
    /// operation was cancelled before any ACK arrived.
    /// </returns>
    public async Task<IPAddress?> DiscoverServerAsync(CancellationToken cancellationToken)
    {
        // Bind to the discovery port so the unicast ACK (sent to this port) is received.
        using UdpClient socket = UdpBeacon.CreateListener(_config.UdpDiscoveryPort);
        socket.EnableBroadcast = true;

        ControlMessage beacon = ControlMessage.Beacon(_machineName, _adUser, _version);
        Logger.Info("Beaconing for Teacher Server...");

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await UdpBeacon.BroadcastAsync(socket, beacon, _config.UdpDiscoveryPort).ConfigureAwait(false);

                // Wait up to one interval for an ACK before re-broadcasting.
                using var window = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                window.CancelAfter(BeaconInterval);

                try
                {
                    while (true)
                    {
                        var received = await UdpBeacon.ReceiveAsync(socket, window.Token).ConfigureAwait(false);
                        if (received is { } r && r.Message.Type == MessageType.ACK)
                        {
                            Logger.Info($"ACK received from Teacher Server {r.From.Address} (v{r.Message.ServerVersion}).");
                            return r.From.Address;
                        }
                        // Ignore our own broadcast echoes / unrelated datagrams and keep waiting.
                    }
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    // Interval elapsed with no ACK; loop and beacon again.
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex) when (ex is SocketException or ObjectDisposedException)
            {
                Logger.Warn($"Beacon socket error: {ex.Message}");
                try { await Task.Delay(BeaconInterval, cancellationToken).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
            }
        }

        return null;
    }
}
