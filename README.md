![https://dotnet.microsoft.com/en-us/download](https://img.shields.io/badge/.NET-8-blue)

# UDP Lobby Chat

Basic UDP Chat to test NAT traversal techniques.

### UDP Punching Hole

Use a lobby server which connects and presents peers to each other with their exposed IPs and Ports.

The server runs almost `http`. It keeps a slide cache of the lobbies information. And it also exposes an UDP port to
capture the endpoint of an already logged client.

## Getting Started

### Dependencies

* [.NET 8](https://dotnet.microsoft.com/en-us/download)

### Build

At the solution directory on command line

```sh
dotnet build
```

## Usage

Running from the solution directory

### Server

```bash
dotnet run --project .\LobbyServer
```

- Default **HTTP**: `9999`
- Default **UDP** : `8888`

> 💡 Check the swagger `API` docs at http://localhost:9999/swagger

### Clients

The default client configuration is defined on this [JSON file](/LobbyClient/appsettings.json) which looks like:

```json
{
    "LobbyRoomName": "udp_chat",
    "LocalPort": 9000,
    "LobbyServerUrl": "http://localhost:9999",
    "LobbyUdpPort": 8888
}
```

To start a single client just run:

```sh
dotnet run --project .\LobbyClient
```

You can override some of the default configurations via command args which can be helpful to start another client
without a port conflict.

```sh
dotnet run --project .\LobbyClient -LocalPort 9001
```

Overriding server URL and UDP Port:

```bash
dotnet run --project .\LobbyClient -LobbyServerUrl "https://lobby-server.fly.dev" -LobbyUdpPort 8888
```

