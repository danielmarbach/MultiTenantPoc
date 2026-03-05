using Microsoft.Extensions.DependencyInjection;
using NServiceBus;

namespace MultiTenantPoc;

public static class NServiceBusServiceCollectionExtensions
{
    public static IServiceCollection AddMultiTenantNServiceBusEndpoints(
        this IServiceCollection services,
        PocOptions options,
        EndpointCatalog endpointCatalog)
    {
        foreach (var tenant in options.Tenants)
        {
            services.AddMainEndpoint(options, endpointCatalog, tenant);
            services.AddPartitionEndpoints(options, endpointCatalog, tenant);
        }

        return services;
    }

    static IServiceCollection AddMainEndpoint(
        this IServiceCollection services,
        PocOptions options,
        EndpointCatalog endpointCatalog,
        TenantOptions tenant)
    {
        var tenantMainEndpointName = endpointCatalog.GetMainEndpoint(tenant.TenantId);
        var tenantConnectionString = GetTenantConnectionString(options, endpointCatalog, tenant.TenantId);

        var mainEndpoint = EndpointFactory.Create(
            endpointName: tenantMainEndpointName,
            tenantId: tenant.TenantId,
            partitionLabel: "main",
            connectionString: tenantConnectionString,
            defaultSchema: options.SqlTransport.MainSchema,
            errorQueue: options.SqlTransport.ErrorQueue,
            auditQueue: options.SqlTransport.AuditQueue,
            heartbeatQueue: options.SqlTransport.HeartbeatQueue,
            customChecksQueue: options.SqlTransport.CustomChecksQueue,
            metricsQueue: options.SqlTransport.MetricsQueue,
            transactionMode: options.SqlTransport.TransactionMode,
            processingConcurrency: Math.Max(1, tenant.MainEndpointConcurrency),
            addHandlers: cfg => cfg.Handlers.MultiTenantPocAssembly.MultiTenantPoc.AddBulkIngestionCommandHandler(),
            routeToSelfMessageTypes: [typeof(BulkIngestionCommand)]);

        services.AddNServiceBusEndpoint(mainEndpoint, tenant.TenantId);

        return services;
    }

    static IServiceCollection AddPartitionEndpoints(
        this IServiceCollection services,
        PocOptions options,
        EndpointCatalog endpointCatalog,
        TenantOptions tenant)
    {
        foreach (var partitionEndpoint in endpointCatalog.GetPartitionEndpoints(tenant.TenantId))
        {
            services.AddPartitionEndpoint(options, endpointCatalog, tenant, partitionEndpoint);
        }

        return services;
    }

    static IServiceCollection AddPartitionEndpoint(
        this IServiceCollection services,
        PocOptions options,
        EndpointCatalog endpointCatalog,
        TenantOptions tenant,
        PartitionEndpointDescriptor partitionEndpoint)
    {
        var tenantConnectionString = GetTenantConnectionString(options, endpointCatalog, tenant.TenantId);

        var partitionEndpointConfiguration = EndpointFactory.Create(
            endpointName: partitionEndpoint.EndpointName,
            tenantId: tenant.TenantId,
            partitionLabel: $"p{partitionEndpoint.Partition}",
            connectionString: tenantConnectionString,
            defaultSchema: partitionEndpoint.Schema,
            errorQueue: options.SqlTransport.ErrorQueue,
            auditQueue: options.SqlTransport.AuditQueue,
            heartbeatQueue: options.SqlTransport.HeartbeatQueue,
            customChecksQueue: options.SqlTransport.CustomChecksQueue,
            metricsQueue: options.SqlTransport.MetricsQueue,
            transactionMode: options.SqlTransport.TransactionMode,
            processingConcurrency: 1,
            addHandlers: cfg =>
            {
                var multiTenantPocRegistry = cfg.Handlers.MultiTenantPocAssembly.MultiTenantPoc;
                multiTenantPocRegistry.AddPartitionedBusinessCommandHandler();
                multiTenantPocRegistry.AddPartitionSagaProbeCommandHandler();
                multiTenantPocRegistry.AddPartitionedEndpointSaga();
            },
            routeToSelfMessageTypes: [typeof(PartitionedBusinessCommand), typeof(StartPartitionSagaCommand)]);

        services.AddNServiceBusEndpoint(partitionEndpointConfiguration, partitionEndpoint.EndpointName);

        return services;
    }

    static string GetTenantConnectionString(PocOptions options, EndpointCatalog endpointCatalog, string tenantId)
        => SqlConnectionStringBuilderFactory.ForDatabase(
            options.SqlTransport.ConnectionString,
            endpointCatalog.GetTenantDatabase(tenantId));
}
