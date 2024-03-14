namespace LobbyClient.Models;

public sealed class Lobby
{
    public required string Name { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
    public bool Ready { get; init; }
    public required Peer[] Players { get; init; }
}
