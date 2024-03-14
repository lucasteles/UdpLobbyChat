namespace LobbyClient;

[Serializable]
public class Settings
{
    public int LocalPort { get; set; }
    public required string LobbyRoomName { get; set; }
    public required Uri LobbyServerUrl { get; set; }
    public int LobbyUdpPort { get; set; }

    public void ParseArgs(string[] args)
    {
        if (args is []) return;

        var argsDict = args
            .Chunk(2)
            .Where(a => a[0].StartsWith('-'))
            .Select(a => a is [{ } key, { } value]
                ? (Key: key.TrimStart('-'), Value: value)
                : throw new InvalidOperationException("Bad arguments")
            ).ToDictionary(x => x.Key, x => x.Value, StringComparer.InvariantCultureIgnoreCase);

        if (argsDict.TryGetValue(nameof(LocalPort), out var portArg) && int.TryParse(portArg, out var port) && port > 0)
            LocalPort = port;

        if (argsDict.TryGetValue(nameof(LobbyUdpPort), out var lobbyPortArg) &&
            int.TryParse(lobbyPortArg, out var lobbyPort) && lobbyPort > 0)
            LobbyUdpPort = lobbyPort;

        if (argsDict.TryGetValue(nameof(LobbyServerUrl), out var serverUrl) &&
            Uri.TryCreate(serverUrl, UriKind.Absolute, out var serverUri))
            LobbyServerUrl = serverUri;
    }
}
