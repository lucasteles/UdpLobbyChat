using System.Reflection;
using System.Text.Json;
using LobbyClient;
using LobbyClient.Services;

using CancellationTokenSource cts = new();

Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    // ReSharper disable AccessToDisposedClosure
    if (cts.IsCancellationRequested) return;
    Console.WriteLine("\nExiting...");
    cts.Cancel();
};

var settingsFile = Path.Combine(
    Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? Directory.GetCurrentDirectory(),
    "appsettings.json"
);

var settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(settingsFile))
               ?? throw new InvalidOperationException();
settings.ParseArgs(args);

UserIO console = new();
LobbyChat chat = new(settings, console, cts.Token);
await chat.Start();

console.WriteLine("Exited", ConsoleColor.DarkGray);
