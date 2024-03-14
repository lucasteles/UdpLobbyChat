using System.Net;

namespace LobbyServer;

using PeerToken = Guid;
using PeerId = Guid;

public sealed class Peer(string username, IPAddress requestAddress)
{
    public PeerId PeerId { get; init; } = Guid.NewGuid();
    public string Username { get; } = username;
    public IPAddress RequestAddress { get; } = requestAddress;
    public IPEndPoint? Endpoint { get; set; }
    public bool Connected => Endpoint is not null;
}

public sealed record LobbyEntry(Peer Peer)
{
    public PeerToken Token { get; init; } = PeerToken.NewGuid();
    public required DateTimeOffset LastRead { get; set; }
}

public sealed class Lobby(
    string name,
    PeerId owner,
    TimeSpan expiration,
    TimeSpan purgeTimeout,
    DateTimeOffset createdAt
)
{
    const int MaxPlayers = 16;

    readonly List<LobbyEntry> entries = [];
    public readonly object Locker = new();

    public string Name { get; } = name;
    public PeerId Owner { get; private set; } = owner;
    public DateTimeOffset CreatedAt { get; } = createdAt;

    public DateTimeOffset ExpiresAt => CreatedAt + expiration;

    public IEnumerable<Peer> Players
    {
        get
        {
            lock (Locker)
                return entries.Take(MaxPlayers).Select(x => x.Peer);
        }
    }

    public void AddPeer(LobbyEntry entry)
    {
        lock (Locker) entries.Add(entry);
    }

    public void RemovePeer(LobbyEntry entry)
    {
        lock (Locker) entries.Remove(entry);
    }

    public LobbyEntry? FindEntry(string username)
    {
        lock (Locker)
            return entries.Find(p =>
                p.Peer.Username.Equals(username, StringComparison.OrdinalIgnoreCase));
    }

    public LobbyEntry? FindEntry(PeerToken token)
    {
        lock (Locker)
            return entries.Find(p => p.Token == token);
    }

    public bool IsEmpty()
    {
        lock (Locker)
            return entries.Count is 0;
    }

    public void Purge(DateTimeOffset now)
    {
        lock (Locker)
        {
            entries.RemoveAll(entry => now - entry.LastRead >= purgeTimeout);
            if (entries.Count > 0 && entries.TrueForAll(x => x.Peer.PeerId != Owner))
                Owner = entries.OrderByDescending(x => x.LastRead).First().Peer.PeerId;
        }
    }
}

public sealed record EnterLobbyRequest(string LobbyName, string Username);

public sealed record EnterLobbyResponse(
    string Username,
    string LobbyName,
    PeerId PeerId,
    PeerToken Token,
    IPAddress IP
);
