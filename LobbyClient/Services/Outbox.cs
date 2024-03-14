using System.Diagnostics;
using System.Net;
using System.Text;
using System.Threading.Channels;
using LobbyClient.Models;

namespace LobbyClient.Services;

public sealed class Outbox : IDisposable
{
    readonly UdpSocket socket;
    readonly CancellationToken cancellationToken;

    public int TotalSendBytes { get; private set; }

    readonly Channel<Message> messageQueue = Channel.CreateUnbounded<Message>(new()
    {
        SingleReader = true,
        SingleWriter = true,
        AllowSynchronousContinuations = true,
    });

    public Outbox(UdpSocket socket, CancellationToken cancellationToken)
    {
        this.socket = socket;
        this.cancellationToken = cancellationToken;

        _ = BeginSendLoop();
    }

    Task BeginSendLoop() =>
        Task.Run(async () =>
        {
            try
            {
                await SendLoop();
            }
            catch (OperationCanceledException)
            {
                // skip
            }
            catch (ChannelClosedException)
            {
                // skip
            }
            catch (Exception ex)
            {
                await Console.Error.WriteAsync($"Outbox Error: {ex}");
            }
        }, cancellationToken);

    async Task SendLoop()
    {
        var buffer = UdpSocket.GetMemoryBuffer();

        await foreach (var message in messageQueue.Reader.ReadAllAsync(cancellationToken))
        {
            var bytesWritten = Encoding.UTF8.GetBytes(message.Body, buffer.Span);
            var messageBytes = buffer[..bytesWritten];

            var bytesSent = await socket
                .SendToAsync(messageBytes, message.EndPoint, cancellationToken)
                .ConfigureAwait(false);

            Debug.Assert(bytesWritten == bytesSent);

            TotalSendBytes += bytesSent;
        }
    }

    public ValueTask Post(in Message message) => messageQueue.Writer.WriteAsync(message, cancellationToken);
    public ValueTask Post(EndPoint recipient, string message) => Post(new(recipient, message));
    public void Dispose() => messageQueue.Writer.TryComplete();
}
