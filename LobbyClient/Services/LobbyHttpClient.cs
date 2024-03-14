using System.Net.Http.Json;
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
        var response = await client.PostAsJsonAsync("/lobby", new
        {
            lobbyName,
            username,
        }, jsonOptions, cancellationToken: ct);

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<User>(jsonOptions, cancellationToken: ct)
                     ?? throw new InvalidOperationException();

        client.DefaultRequestHeaders.Clear();
        client.DefaultRequestHeaders.Add("token", result.Token.ToString());

        return result;
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
