using System.Collections.Concurrent;
using System.IO.Hashing;
using System.Text;

namespace MultiTenantPoc;

public sealed class EndpointColorConsoleLoggerProvider : ILoggerProvider, ISupportExternalScope
{
    static readonly ConsoleColor[] TenantPalette =
    [
        ConsoleColor.Cyan,
        ConsoleColor.Green,
        ConsoleColor.Blue,
        ConsoleColor.Red,
        ConsoleColor.DarkCyan,
        ConsoleColor.DarkGreen,
        ConsoleColor.DarkBlue,
        ConsoleColor.DarkRed,
        ConsoleColor.DarkMagenta,
        ConsoleColor.Magenta
    ];

    readonly object gate = new();
    readonly object colorGate = new();
    readonly ConcurrentDictionary<string, ConsoleColor> endpointColors = new(StringComparer.OrdinalIgnoreCase);
    readonly ConcurrentDictionary<string, ConsoleColor> tenantColors = new(StringComparer.OrdinalIgnoreCase);
    readonly HashSet<ConsoleColor> usedTenantColors = [];
    IExternalScopeProvider scopeProvider = new LoggerExternalScopeProvider();

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
        var endpoint = TryGetScopeValue(scopes, "EndpointIdentifier")
            ?? TryGetScopeValue(scopes, "EndpointName")
            ?? TryGetScopeValue(scopes, "NServiceBus.EndpointName")
            ?? TryGetScopeValue(scopes, "Endpoint")
            ?? "app";

        var tenantSeed = GetTenantColorSeed(TryGetScopeValue(scopes, "Endpoint") ?? endpoint);

        var endpointColor = endpointColors.GetOrAdd(endpoint, value =>
        {
            return ResolveTenantColor(tenantSeed);
        });

        lock (gate)
        {
            var timestamp = DateTimeOffset.Now.ToString("HH:mm:ss.fff");

            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write(timestamp);
            Console.Write(' ');

            Console.ForegroundColor = GetLogLevelColor(logLevel);
            Console.Write($"[{logLevel,-11}]");
            Console.Write(' ');

            Console.ForegroundColor = endpointColor;
            Console.Write($"[{endpoint}]");
            Console.Write(' ');

            Console.ForegroundColor = ConsoleColor.Gray;
            Console.Write(category);
            Console.Write(" - ");
            Console.Write(message);

            if (scopes.Count > 0)
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write(" | scopes: ");
                Console.Write(string.Join(", ", scopes.Select(kvp => $"{kvp.Key}={kvp.Value}")));
            }

            Console.WriteLine();

            if (exception is not null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(exception);
            }

            Console.ResetColor();
        }
    }

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

    static string? TryGetScopeValue(IEnumerable<KeyValuePair<string, object?>> scopes, string key)
        => scopes.FirstOrDefault(kvp => string.Equals(kvp.Key, key, StringComparison.OrdinalIgnoreCase)).Value?.ToString();

    static int StableHash(string value)
    {
        var normalized = value.ToUpperInvariant();
        var bytes = Encoding.UTF8.GetBytes(normalized);
        var hash = XxHash32.HashToUInt32(bytes);
        return (int)(hash & 0x7fffffff);
    }

    static string GetTenantColorSeed(string endpoint)
    {
        var marker = endpoint.LastIndexOf("-p", StringComparison.OrdinalIgnoreCase);
        if (marker > 0)
        {
            return endpoint[..marker];
        }

        return endpoint;
    }

    ConsoleColor ResolveTenantColor(string tenantSeed)
    {
        return tenantColors.GetOrAdd(tenantSeed, seed =>
        {
            lock (colorGate)
            {
                if (usedTenantColors.Count >= TenantPalette.Length)
                {
                    var wrapHash = StableHash(seed);
                    return TenantPalette[wrapHash % TenantPalette.Length];
                }

                var start = StableHash(seed) % TenantPalette.Length;
                for (var offset = 0; offset < TenantPalette.Length; offset++)
                {
                    var candidate = TenantPalette[(start + offset) % TenantPalette.Length];
                    if (usedTenantColors.Add(candidate))
                    {
                        return candidate;
                    }
                }

                return TenantPalette[start];
            }
        });
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
