using System.Net;

namespace LobbyClient.Models;

public readonly record struct Message(EndPoint EndPoint, string Body);
