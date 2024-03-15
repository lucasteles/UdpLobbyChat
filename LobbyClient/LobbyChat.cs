using System.Net;
using System.Text.RegularExpressions;
using LobbyClient.Models;
using LobbyClient.Services;

namespace LobbyClient;

public class LobbyChat(
    Settings settings,
    UserIO console,
    CancellationToken cancellationToken
)
{
    readonly LobbyHttpClient httpClient = new(settings);
    readonly TimeSpan refreshInterval = TimeSpan.FromSeconds(2);
    readonly Dictionary<Guid, Peer> knownPeers = [];
    readonly Dictionary<Guid, ConsoleColor> peerColors = [];

    User? user;
    Peer? userPeer;

    static readonly ConsoleColor[] peerNameColorList =
    [
        ConsoleColor.DarkMagenta,
        ConsoleColor.DarkYellow,
        ConsoleColor.DarkBlue,
        ConsoleColor.DarkRed,
    ];

    int nextColorIndex;

    public async Task Start()
    {
        try
        {
            console.WriteLine("\n-- UDP Lobby Chat --\n", ConsoleColor.Yellow);
            await SignIn();
            await Connect();
        }
        catch (OperationCanceledException)
        {
            // skip
        }

        using UdpSocket socket = new(settings.LocalPort);
        using Outbox outbox = new(socket, cancellationToken);
        Inbox inbox = new(socket, cancellationToken)
        {
            OnMessage = OnMessage,
        };

        await SendLoop(outbox);
        await SignOut();

        console.WriteLine("\n-- Summary --", ConsoleColor.Cyan);
        console.WriteLine($"Send: {outbox.TotalSendBytes} bytes", ConsoleColor.Cyan);
        console.WriteLine($"Recv: {inbox.TotalReceivedBytes} bytes", ConsoleColor.Cyan);
        console.WriteLine(new('-', 13), ConsoleColor.Cyan);
        console.WriteLine();
    }

    void OnMessage(Message message)
    {
        if (string.IsNullOrWhiteSpace(message.Body)) return;
        var peer = knownPeers.Values.FirstOrDefault(x =>
            Equals(x.Endpoint, message.EndPoint) ||
            (x.LocalEndpoint is not null && Equals(x.LocalEndpoint, message.EndPoint)));
        PrintPeerMessage(peer, message.Body, message.EndPoint);
    }

    void PrintPeerMessage(Peer? peer, string message, EndPoint? endPoint = null)
    {
        string name;
        ConsoleColor color;

        if (peer is not null)
        {
            color = peerColors.GetValueOrDefault(peer.PeerId, ConsoleColor.DarkGray);
            name = peer.Username;
        }
        else
        {
            color = ConsoleColor.DarkGray;
            name = endPoint?.ToString() ?? "unknown";
        }

        console.Write($"{name}: ", color);
        console.WriteLine(message);
    }

    async Task SendLoop(Outbox outbox)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var message = await console.Ask(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(message)) continue;
            PrintPeerMessage(userPeer, message);

            foreach (var peer in knownPeers.Values)
            {
                if (!peer.Connected || peer.PeerId == user?.PeerId)
                    continue;

                if (Equals(peer.Endpoint.Address, userPeer?.Endpoint.Address) && peer.LocalEndpoint is not null)
                    await outbox.Post(peer.LocalEndpoint, message);
                else
                    await outbox.Post(peer.Endpoint, message);
            }
        }
    }

    public async Task SignIn()
    {
        var lobbyName = await console.Ask("Room Name", settings.LobbyRoomName, cancellationToken);
        var defaultUserName = Regex.Replace(Environment.UserName.Trim().ToLower(), "[^a-zA-Z0-9]", "_");
        var username = await console.Ask("Username", defaultUserName, cancellationToken);

        console.WriteLine($"Signing-in into {settings.LobbyServerUrl.Host}...", ConsoleColor.Cyan);
        user = await httpClient.EnterLobby(lobbyName, username, cancellationToken);

        console.WriteLine("Sig-In success!", ConsoleColor.Green);
        console.WriteLine($"Logged as {user.Username} on {user.LobbyName} at {user.IP}", ConsoleColor.Cyan);

        Console.Title = $"chat={user.Username}@{user.LobbyName}";
        StartLobbyRefreshTimer();
    }


    async Task SignOut()
    {
        if (user is not null)
        {
            try
            {
                console.WriteLine("Leaving room...", ConsoleColor.Yellow);
                await httpClient.LeaveLobby(user, default);
            }
            catch (Exception)
            {
                // skip
            }
        }
    }

    public async Task Connect()
    {
        if (user is null) throw new InvalidOperationException("Not logged in!");

        var buffer = new byte[36]; // UTF8 string size of a Guid
        if (!user.Token.TryFormat(buffer, out var bytesWritten) || bytesWritten is 0)
            throw new InvalidOperationException("Invalid user token");

        var messageBytes = buffer.AsMemory()[..bytesWritten];

        var serverAddress = UdpSocket.GetDnsIpAddress(settings.LobbyServerUrl.DnsSafeHost);
        IPEndPoint serverEndpoint = new(serverAddress, settings.LobbyUdpPort);

        console.WriteLine($"Local Port: {settings.LocalPort}", ConsoleColor.Cyan);
        console.WriteLine($"Connecting to UDP server at {serverEndpoint}", ConsoleColor.Cyan);
        using UdpSocket socket = new(settings.LocalPort);

        var maxRetries = 10;
        while (maxRetries-- > 0)
        {
            await socket
                .SendToAsync(messageBytes, serverEndpoint, cancellationToken)
                .ConfigureAwait(false);

            await Task.Delay(refreshInterval);

            if (userPeer is not null && userPeer.Connected)
            {
                console.WriteLine(
                    $"\nConnected to server {settings.LobbyServerUrl.Host}:{settings.LobbyUdpPort} at endpoint {userPeer.Endpoint}\n",
                    ConsoleColor.Green
                );
                return;
            }

            console.Write(".");
        }

        console.WriteLine($"\nUnable to connect to UDP server at {serverEndpoint}", ConsoleColor.Red);
    }

    async Task RefreshLobby()
    {
        if (user is null) return;
        var lobbyInfo = await httpClient.GetLobby(user, cancellationToken);

        foreach (var peer in lobbyInfo.Players)
        {
            if (peer.PeerId == user.PeerId)
            {
                userPeer = peer;
                SetColor(peer);
                continue;
            }

            if (knownPeers.TryGetValue(peer.PeerId, out var knownPeer))
            {
                if (peer.Connected == knownPeer.Connected)
                    continue;

                PrintPeerConnectionStatus(peer);
                knownPeer.Connected = peer.Connected;
                knownPeer.Endpoint = peer.Endpoint;
            }
            else
            {
                knownPeers.Add(peer.PeerId, peer);
                SetColor(peer);
                PrintPeerConnectionStatus(peer);
            }
        }

        var ids = lobbyInfo.Players.Select(x => x.PeerId).ToArray();
        foreach (var leaved in knownPeers.Values.Where(x => !ids.Contains(x.PeerId)))
        {
            console.WriteLine($"{leaved.Username} disconnected!", ConsoleColor.Red);
            knownPeers.Remove(leaved.PeerId);
        }
    }

    void SetColor(Peer peer)
    {
        if (!peerColors.ContainsKey(peer.PeerId))
            peerColors.Add(peer.PeerId, peerNameColorList[nextColorIndex++ % peerNameColorList.Length]);
    }

    void PrintPeerConnectionStatus(Peer peer)
    {
        if (peer.Connected)
        {
            console.WriteLine($"{peer.Username} connected from {peer.Endpoint}", ConsoleColor.Magenta);

            if (peer.LocalEndpoint is not null && Equals(peer.Endpoint.Address, user?.IP))
                console.WriteLine(
                    $"{peer.Username} is connecting from the same network, using local endpoint {peer.LocalEndpoint}",
                    ConsoleColor.Yellow);
        }
        else
            console.WriteLine($"{peer.Username} is connecting from {peer.RequestAddress}...", ConsoleColor.Yellow);
    }

    void StartLobbyRefreshTimer() => Task.Run(async () =>
    {
        using PeriodicTimer timer = new(refreshInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                if (user is null) continue;
                await RefreshLobby();
            }
        }
        catch (OperationCanceledException)
        {
            // skip
        }
        catch (Exception ex)
        {
            console.Error(ex);
        }
    });
}
