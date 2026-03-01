namespace MultiTenantPoc;

public sealed class EndpointColorConsoleLoggerProvider : ILoggerProvider, ISupportExternalScope
{
    static readonly string[] EndpointScopeKeys =
    ["EndpointIdentifier", "EndpointName", "NServiceBus.EndpointName", "Endpoint"];

    readonly Lock gate = new();

    private readonly bool disableColors =
        string.Equals(Environment.GetEnvironmentVariable("NO_COLOR"), "1", StringComparison.OrdinalIgnoreCase);

    private IExternalScopeProvider scopeProvider = new LoggerExternalScopeProvider();

    public ILogger CreateLogger(string categoryName) => new EndpointColorConsoleLogger(categoryName, this);

    public void Dispose()
    {
    }

    public void SetScopeProvider(IExternalScopeProvider scopeProvider)
    {
        this.scopeProvider = scopeProvider;
    }

    internal void Write(LogLevel logLevel, string category, string message, Exception? exception)
    {
        var scopes = ReadScopes();
        var endpoint = ResolveEndpoint(scopes);
        var endpointColor = ResolveTenantColor(endpoint);

        lock (gate)
        {
            WriteColored(DateTimeOffset.Now.ToString("HH:mm:ss.fff"), ConsoleColor.DarkGray, ConsoleColor.Black);
            Console.Write(' ');

            WriteColored($"[{logLevel,-11}]", GetLogLevelColor(logLevel), ConsoleColor.Black);
            Console.Write(' ');

            WriteColored($"[{endpoint}]", endpointColor, ConsoleColor.Black);
            Console.Write(' ');

            WriteColored(category, ConsoleColor.Gray, ConsoleColor.Black);
            Console.Write(" - ");
            WriteColored(message, ConsoleColor.Gray, ConsoleColor.Black);

            if (scopes.Count > 0)
            {
                WriteColored(" | scopes: ", ConsoleColor.DarkGray, ConsoleColor.Black);
                WriteColored(string.Join(", ", scopes.Select(kvp => $"{kvp.Key}={kvp.Value}")), ConsoleColor.DarkGray, ConsoleColor.Black);
            }

            Console.WriteLine();

            if (exception is not null)
            {
                WriteColored(exception.ToString(), ConsoleColor.Red, ConsoleColor.Black);
                Console.WriteLine();
            }
        }
    }

    private void WriteColored(string value, ConsoleColor foreground, ConsoleColor? background = null)
    {
        if (disableColors)
        {
            Console.Write(value);
            return;
        }

        if (background.HasValue)
        {
            Console.Write(GetBackgroundColorEscapeCode(background.Value));
        }
        Console.Write(GetForegroundColorEscapeCode(foreground));
        Console.Write(value);
        Console.Write(DefaultForegroundColor);
        if (background.HasValue)
        {
            Console.Write(DefaultBackgroundColor);
        }
    }

    // Uses the same escape codes as Microsoft.Extensions.Logging.Console.AnsiParser
    // Bright colors use bold (\x1B[1m) + base code, NOT the 90-97 range,
    // because Aspire's dashboard ANSI parser doesn't support 90-97.
    const string DefaultForegroundColor = "\x1B[39m\x1B[22m";
    const string DefaultBackgroundColor = "\x1B[49m";

    static string GetForegroundColorEscapeCode(ConsoleColor color) => color switch
    {
        ConsoleColor.Black => "\x1B[30m",
        ConsoleColor.DarkRed => "\x1B[31m",
        ConsoleColor.DarkGreen => "\x1B[32m",
        ConsoleColor.DarkYellow => "\x1B[33m",
        ConsoleColor.DarkBlue => "\x1B[34m",
        ConsoleColor.DarkMagenta => "\x1B[35m",
        ConsoleColor.DarkCyan => "\x1B[36m",
        ConsoleColor.Gray => "\x1B[37m",
        ConsoleColor.DarkGray => "\x1B[1m\x1B[30m",
        ConsoleColor.Red => "\x1B[1m\x1B[31m",
        ConsoleColor.Green => "\x1B[1m\x1B[32m",
        ConsoleColor.Yellow => "\x1B[1m\x1B[33m",
        ConsoleColor.Blue => "\x1B[1m\x1B[34m",
        ConsoleColor.Magenta => "\x1B[1m\x1B[35m",
        ConsoleColor.Cyan => "\x1B[1m\x1B[36m",
        ConsoleColor.White => "\x1B[1m\x1B[37m",
        _ => DefaultForegroundColor
    };

    static string GetBackgroundColorEscapeCode(ConsoleColor color) => color switch
    {
        ConsoleColor.Black => "\x1B[40m",
        ConsoleColor.DarkRed => "\x1B[41m",
        ConsoleColor.DarkGreen => "\x1B[42m",
        ConsoleColor.DarkYellow => "\x1B[43m",
        ConsoleColor.DarkBlue => "\x1B[44m",
        ConsoleColor.DarkMagenta => "\x1B[45m",
        ConsoleColor.DarkCyan => "\x1B[46m",
        ConsoleColor.Gray => "\x1B[47m",
        ConsoleColor.DarkGray => "\x1B[1m\x1B[40m",
        ConsoleColor.Red => "\x1B[1m\x1B[41m",
        ConsoleColor.Green => "\x1B[1m\x1B[42m",
        ConsoleColor.Yellow => "\x1B[1m\x1B[43m",
        ConsoleColor.Blue => "\x1B[1m\x1B[44m",
        ConsoleColor.Magenta => "\x1B[1m\x1B[45m",
        ConsoleColor.Cyan => "\x1B[1m\x1B[46m",
        ConsoleColor.White => "\x1B[1m\x1B[47m",
        _ => DefaultBackgroundColor
    };

    List<KeyValuePair<string, object?>> ReadScopes()
    {
        var values = new List<KeyValuePair<string, object?>>();

        scopeProvider.ForEachScope((scope, state) =>
        {
            if (scope is IEnumerable<KeyValuePair<string, object?>> keyValuePairs)
            {
                state.AddRange(keyValuePairs.Where(static kvp => kvp.Key != "{OriginalFormat}"));
                return;
            }

            state.Add(new KeyValuePair<string, object?>("Scope", scope));
        }, values);

        return values;
    }

    static string ResolveEndpoint(IEnumerable<KeyValuePair<string, object?>> scopes)
    {
        foreach (var key in EndpointScopeKeys)
        {
            var endpoint = TryGetScopeValue(scopes, key);
            if (!string.IsNullOrWhiteSpace(endpoint))
            {
                return endpoint;
            }
        }

        return "app";
    }

    static string? TryGetScopeValue(IEnumerable<KeyValuePair<string, object?>> scopes, string key)
    {
        foreach (var (scopeKey, scopeValue) in scopes)
        {
            if (string.Equals(scopeKey, key, StringComparison.OrdinalIgnoreCase))
            {
                return scopeValue?.ToString();
            }
        }

        return null;
    }

    static ConsoleColor ResolveTenantColor(string endpoint)
    {
        if (endpoint.Contains("tenant-a", StringComparison.OrdinalIgnoreCase))
        {
            return ConsoleColor.Cyan;
        }

        if (endpoint.Contains("tenant-b", StringComparison.OrdinalIgnoreCase))
        {
            return ConsoleColor.Green;
        }

        if (endpoint.Contains("tenant-c", StringComparison.OrdinalIgnoreCase))
        {
            return ConsoleColor.Magenta;
        }

        if (endpoint.Contains("tenant-d", StringComparison.OrdinalIgnoreCase))
        {
            return ConsoleColor.Red;
        }

        if (endpoint.Contains("tenant-e", StringComparison.OrdinalIgnoreCase))
        {
            return ConsoleColor.Blue;
        }

        return ConsoleColor.Gray;
    }

    static ConsoleColor GetLogLevelColor(LogLevel level) => level switch
    {
        LogLevel.Trace => ConsoleColor.DarkGray,
        LogLevel.Debug => ConsoleColor.Gray,
        LogLevel.Information => ConsoleColor.White,
        LogLevel.Warning => ConsoleColor.Yellow,
        LogLevel.Error => ConsoleColor.Red,
        LogLevel.Critical => ConsoleColor.DarkRed,
        _ => ConsoleColor.Gray
    };

    sealed class EndpointColorConsoleLogger(string categoryName, EndpointColorConsoleLoggerProvider provider) : ILogger
    {
        public IDisposable BeginScope<TState>(TState state) where TState : notnull
            => provider.scopeProvider.Push(state);

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var message = formatter(state, exception);
            provider.Write(logLevel, categoryName, message, exception);
        }
    }
}