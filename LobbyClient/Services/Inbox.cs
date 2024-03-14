using System.Text;
using LobbyClient.Models;

namespace LobbyClient.Services;

public sealed class Inbox
{
    readonly UdpSocket socket;
    readonly CancellationToken cancellationToken;

    public Action<Message> OnMessage = delegate { };

    public int TotalReceivedBytes { get; private set; }

    public Inbox(UdpSocket socket, CancellationToken cancellationToken)
    {
        this.socket = socket;
        this.cancellationToken = cancellationToken;
        _ = BeginReceiveLoop();
    }

    Task BeginReceiveLoop() =>
        Task.Run(async () =>
        {
            try
            {
                await ReceiveLoop();
            }
            catch (OperationCanceledException)
            {
                // skip
            }
            catch (Exception ex)
            {
                await Console.Error.WriteAsync($"Inbox Error: {ex}");
            }
        }, cancellationToken);

    async Task ReceiveLoop()
    {
        var buffer = UdpSocket.GetMemoryBuffer();

        while (!cancellationToken.IsCancellationRequested)
        {
            var receiveInfo = await socket.ReceiveAsync(buffer, cancellationToken);
            if (receiveInfo.ReceivedBytes is 0) continue;
            var messageBytes = buffer[..receiveInfo.ReceivedBytes];

            TotalReceivedBytes += receiveInfo.ReceivedBytes;

            Message message = new(
                receiveInfo.RemoteEndPoint,
                Encoding.UTF8.GetString(messageBytes.Span)
            );

            OnMessage(message);
        }
    }
}
