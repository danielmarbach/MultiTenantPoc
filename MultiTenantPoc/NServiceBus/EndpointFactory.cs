using NServiceBus;

namespace MultiTenantPoc;

public static class EndpointFactory
{
    public static EndpointConfiguration Create(
        string endpointName,
        string connectionString,
        string defaultSchema,
        string transactionMode,
        int processingConcurrency,
        Action<EndpointConfiguration> addHandlers)
    {
        var endpointConfiguration = new EndpointConfiguration(endpointName);
        endpointConfiguration.EnableInstallers();
        endpointConfiguration.UseSerialization<SystemJsonSerializer>();
        endpointConfiguration.SendFailedMessagesTo("error");
        endpointConfiguration.AuditProcessedMessagesTo("audit");
        endpointConfiguration.LimitMessageProcessingConcurrencyTo(processingConcurrency);

        endpointConfiguration.AssemblyScanner().Disable = true;
        addHandlers(endpointConfiguration);

        var transport = endpointConfiguration.UseTransport<SqlServerTransport>();
        transport.ConnectionString(connectionString);
        transport.DefaultSchema(defaultSchema);
        transport.Transactions(ParseMode(transactionMode));

        return endpointConfiguration;
    }

    static TransportTransactionMode ParseMode(string mode)
    {
        if (Enum.TryParse<TransportTransactionMode>(mode, true, out var parsed))
        {
            return parsed;
        }

        throw new InvalidOperationException($"Unsupported transport transaction mode '{mode}'.");
    }
}
