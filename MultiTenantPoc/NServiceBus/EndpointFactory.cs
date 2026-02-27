using NServiceBus;

namespace MultiTenantPoc;

public static class EndpointFactory
{
    public static EndpointConfiguration Create(
        string endpointName,
        string connectionString,
        string defaultSchema,
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

        return endpointConfiguration;
    }
}
