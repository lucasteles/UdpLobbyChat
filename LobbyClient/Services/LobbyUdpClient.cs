using System.Net;
using LobbyClient.Models;

namespace LobbyClient.Services;

public sealed class LobbyUdpClient : IDisposable
{
    readonly IPEndPoint remoteEndPoint;
    readonly UdpSocket socket;
    readonly byte[] buffer = GC.AllocateArray<byte>(36, pinned: true);

    public LobbyUdpClient(Settings settings)
    {
        socket = new UdpSocket(settings.LocalPort);

        var serverAddress = UdpSocket.GetDnsIpAddress(settings.LobbyServerUrl.DnsSafeHost);
        remoteEndPoint = new(serverAddress, settings.LobbyUdpPort);
    }

    public async Task HandShake(User user, CancellationToken ct = default)
    {
        if (!user.Token.TryFormat(buffer, out var bytesWritten) || bytesWritten is 0) return;

        await socket
            .SendToAsync(buffer.AsMemory()[..bytesWritten], remoteEndPoint, ct)
            .ConfigureAwait(false);
    }

    public void Dispose() => socket.Dispose();
}
