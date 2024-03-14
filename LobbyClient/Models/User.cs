using System.Net;

namespace LobbyClient.Models;

public sealed class User
{
    public required Guid PeerId { get; init; }
    public required Guid Token { get; init; }
    public required string Username { get; init; }
    public required string LobbyName { get; init; }
    public required IPAddress IP { get; init; }
}
