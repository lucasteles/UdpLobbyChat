using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Serialization;
using Backdash.JsonConverters;
using LobbyClient.Models;

namespace LobbyClient.Services;

public sealed class LobbyHttpClient(Settings appSettings)
{
    static readonly JsonSerializerOptions jsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters =
        {
            new JsonStringEnumConverter(),
            new JsonIPAddressConverter(),
            new JsonIPEndPointConverter(),
        }
    };

    readonly HttpClient client = new()
    {
        BaseAddress = appSettings.LobbyServerUrl,
    };

    public async Task<User> EnterLobby(string lobbyName, string username, CancellationToken ct)
    {
        var localEndpoint = await GetLocalEndpoint();

        var response = await client.PostAsJsonAsync("/lobby", new
        {
            lobbyName,
            username,
            localEndpoint,
        }, jsonOptions, ct);

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<User>(jsonOptions, ct)
                     ?? throw new InvalidOperationException();

        client.DefaultRequestHeaders.Clear();
        client.DefaultRequestHeaders.Add("token", result.Token.ToString());

        return result;
    }

    async Task<IPEndPoint?> GetLocalEndpoint()
    {
        try
        {
            using Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);
            await socket.ConnectAsync("8.8.8.8", 65530);
            if (socket.LocalEndPoint is not IPEndPoint { Address: { } ipAddress })
                return null;
            return new(ipAddress, appSettings.LocalPort);
        }
        catch (Exception)
        {
            // skip
        }

        return null;
    }


    public async Task<Lobby> GetLobby(User user, CancellationToken ct) =>
        await client.GetFromJsonAsync<Lobby>($"/lobby/{user.LobbyName}", jsonOptions, cancellationToken: ct)
        ?? throw new InvalidOperationException();

    public async Task LeaveLobby(User user, CancellationToken ct = default)
    {
        var response = await client.DeleteAsync($"/lobby/{user.LobbyName}", ct);
        response.EnsureSuccessStatusCode();
    }
}
