using System.Net;

namespace LobbyClient.Models;

public sealed class Peer
{
    public required Guid PeerId { get; init; }
    public required string Username { get; init; }
    public required IPAddress RequestAddress { get; init; }
    public required IPEndPoint Endpoint { get; set; }
    public bool Connected { get; set; }
    public IPEndPoint? LocalEndpoint { get; init; }
}
