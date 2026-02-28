using NServiceBus;
using Microsoft.EntityFrameworkCore;
using NServiceBus.Persistence.Sql;

namespace MultiTenantPoc;

public static class EndpointFactory
{
    public static EndpointConfiguration Create(
        string endpointName,
        string tenantId,
        string partitionLabel,
        string connectionString,
        string defaultSchema,
        string errorQueue,
        string auditQueue,
        string heartbeatQueue,
        string customChecksQueue,
        string metricsQueue,
        string transactionMode,
        int processingConcurrency,
        Action<EndpointConfiguration> addHandlers,
        params Type[] routeToSelfMessageTypes)
    {
        var endpointConfiguration = new EndpointConfiguration(endpointName);
        endpointConfiguration.EnableInstallers();
        endpointConfiguration.UseSerialization<SystemJsonSerializer>();
        endpointConfiguration.SendFailedMessagesTo(errorQueue);
        endpointConfiguration.AuditProcessedMessagesTo(auditQueue);
        endpointConfiguration.SendHeartbeatTo(heartbeatQueue);
        endpointConfiguration.ReportCustomChecksTo(customChecksQueue, TimeSpan.FromSeconds(10));
        endpointConfiguration.AddCustomCheck<EndpointStartupCustomCheck>();
        var metrics = endpointConfiguration.EnableMetrics();
        metrics.SendMetricDataToServiceControl(metricsQueue, TimeSpan.FromSeconds(20));
        endpointConfiguration.LimitMessageProcessingConcurrencyTo(processingConcurrency);

        var recoverability = endpointConfiguration.Recoverability();
        recoverability.AddUnrecoverableException<SimulatedUnrecoverableException>();

        endpointConfiguration.AssemblyScanner().Disable = true;
        addHandlers(endpointConfiguration);

        var transport = endpointConfiguration.UseTransport<SqlServerTransport>();
        transport.ConnectionString(connectionString);
        transport.DefaultSchema(defaultSchema);
        transport.Transactions(ParseMode(transactionMode));

        var persistence = endpointConfiguration.UsePersistence<SqlPersistence>();
        persistence.SqlDialect<SqlDialect.MsSqlServer>();
        persistence.ConnectionBuilder(() => new Microsoft.Data.SqlClient.SqlConnection(connectionString));

        endpointConfiguration.RegisterComponents(c =>
        {
            c.AddSingleton(new EndpointStartupCheckContext(endpointName, tenantId, partitionLabel));

            c.AddScoped(serviceProvider =>
            {
                var session = serviceProvider.GetRequiredService<ISqlStorageSession>();

                var dbContext = new PocDbContext(new DbContextOptionsBuilder<PocDbContext>()
                    .UseSqlServer(session.Connection)
                    .Options);

                dbContext.Database.UseTransaction(session.Transaction);
                session.OnSaveChanges((_, cancellationToken) => dbContext.SaveChangesAsync(cancellationToken));

                return dbContext;
            });
        });

        var routing = transport.Routing();
        foreach (var messageType in routeToSelfMessageTypes)
        {
            routing.RouteToEndpoint(messageType, endpointName);
        }

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
