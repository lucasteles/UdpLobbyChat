namespace LobbyClient.Services;

public sealed class UserIO
{
    readonly object locker = new();

    public void WriteLine(string text, ConsoleColor foreColor)
    {
        lock (locker)
        {
            var originalColor = Console.ForegroundColor;
            Console.ForegroundColor = foreColor;
            Console.WriteLine(text);
            Console.ForegroundColor = originalColor;
        }
    }

    public void WriteLine(string text) => WriteLine(text, ConsoleColor.White);
    public void WriteLine() => WriteLine(string.Empty);

    public void Write(string text)
    {
        lock (locker)
            Console.Write(text);
    }

    public void Write(string text, ConsoleColor foreColor)
    {
        lock (locker)
        {
            var originalColor = Console.ForegroundColor;
            Console.ForegroundColor = foreColor;
            Console.Write(text);
            Console.ForegroundColor = originalColor;
        }
    }

    public async Task<string> Ask(CancellationToken cancellationToken)
    {
        var result = await Ask(string.Empty, null, cancellationToken);

        lock (locker)
            Console.SetCursorPosition(0, Console.CursorTop - 1);

        return result;
    }

    public async Task<string> Ask(
        string label,
        string? defaultValue = null,
        CancellationToken cancellationToken = default
    )
    {
        if (!string.IsNullOrWhiteSpace(label))
            lock (locker)
            {
                Write($"{label}", ConsoleColor.Cyan);

                if (defaultValue is not null)
                    Write($" ({defaultValue})", ConsoleColor.Gray);

                Write(":", ConsoleColor.Cyan);
            }

        string? result = null;
        while (result is null)
        {
            if (cancellationToken.IsCancellationRequested)
                return string.Empty;

            try
            {
                result = await Task.Run(Console.ReadLine, cancellationToken)
                    .WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                /* skip */
            }

            if (string.IsNullOrWhiteSpace(result) && defaultValue is not null)
                result = defaultValue;
        }

        return result;
    }

    public void Error(Exception exception) => WriteLine(exception.ToString(), ConsoleColor.Red);
}
