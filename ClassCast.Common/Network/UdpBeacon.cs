using System.Net;
using System.Net.Sockets;
using ClassCast.Common.Protocol;

namespace ClassCast.Common.Network;

/// <summary>
/// Reusable low-level helpers for the UDP discovery channel (specification 2.3.1):
/// the Student Client broadcasts <c>BEACON</c> packets to the subnet and the Teacher
/// Server replies with a unicast <c>ACK</c>. Both the Student
/// <c>BeaconService</c> and the Teacher <c>DiscoveryService</c> build on these
/// primitives.
/// </summary>
public static class UdpBeacon
{
    /// <summary>The IPv4 limited-broadcast address used for beacons.</summary>
    public const string BroadcastAddress = "255.255.255.255";

    /// <summary>
    /// Creates a UDP socket bound to <paramref name="port"/> for receiving beacons
    /// (server) or acknowledgements (client). Address reuse is enabled so multiple
    /// processes on a test machine can coexist.
    /// </summary>
    /// <param name="port">The local UDP port to bind.</param>
    /// <returns>A bound <see cref="UdpClient"/> ready to receive.</returns>
    public static UdpClient CreateListener(int port)
    {
        var client = new UdpClient();
        client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        client.Client.Bind(new IPEndPoint(IPAddress.Any, port));
        return client;
    }

    /// <summary>
    /// Creates a UDP socket suitable for sending broadcast beacons.
    /// </summary>
    /// <returns>A broadcast-enabled <see cref="UdpClient"/>.</returns>
    public static UdpClient CreateSender()
    {
        var client = new UdpClient { EnableBroadcast = true };
        return client;
    }

    /// <summary>
    /// Serialises a control message to JSON and sends it to the given endpoint.
    /// </summary>
    /// <param name="client">The UDP socket to send from.</param>
    /// <param name="message">The message to serialise and send.</param>
    /// <param name="endpoint">The destination endpoint.</param>
    /// <returns>A task that completes when the datagram has been sent.</returns>
    public static async Task SendAsync(UdpClient client, ControlMessage message, IPEndPoint endpoint)
    {
        byte[] payload = message.ToUtf8();
        await client.SendAsync(payload, payload.Length, endpoint).ConfigureAwait(false);
    }

    /// <summary>
    /// Broadcasts a control message to the subnet on the given port.
    /// </summary>
    /// <param name="client">A broadcast-enabled UDP socket.</param>
    /// <param name="message">The message to broadcast.</param>
    /// <param name="port">The destination UDP port.</param>
    /// <returns>A task that completes when the datagram has been broadcast.</returns>
    public static Task BroadcastAsync(UdpClient client, ControlMessage message, int port)
        => SendAsync(client, message, new IPEndPoint(IPAddress.Broadcast, port));

    /// <summary>
    /// Awaits the next datagram and parses it as a <see cref="ControlMessage"/>.
    /// </summary>
    /// <param name="client">The bound listening socket.</param>
    /// <param name="cancellationToken">Token used to cancel the receive.</param>
    /// <returns>
    /// The parsed message and the sender's endpoint, or <c>null</c> if the datagram
    /// could not be parsed as a valid control message.
    /// </returns>
    public static async Task<(ControlMessage Message, IPEndPoint From)?> ReceiveAsync(UdpClient client, CancellationToken cancellationToken = default)
    {
        UdpReceiveResult result = await client.ReceiveAsync(cancellationToken).ConfigureAwait(false);
        ControlMessage? message = ControlMessage.FromUtf8(result.Buffer);
        return message is null ? null : (message, result.RemoteEndPoint);
    }
}
