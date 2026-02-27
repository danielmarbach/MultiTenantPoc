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
        var tenantConnectionString = SqlConnectionStringBuilderFactory.ForDatabase(
            options.SqlTransport.ConnectionString,
            endpointCatalog.GetTenantDatabase(tenant.TenantId));

        var mainEndpoint = EndpointFactory.Create(
            endpointName: tenantMainEndpointName,
            connectionString: tenantConnectionString,
            defaultSchema: options.SqlTransport.MainSchema,
            transactionMode: options.SqlTransport.TransactionMode,
            processingConcurrency: Math.Max(1, tenant.MainEndpointConcurrency),
            addHandlers: cfg => cfg.AddHandler<BulkIngestionCommandHandler>(),
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
        var tenantConnectionString = SqlConnectionStringBuilderFactory.ForDatabase(
            options.SqlTransport.ConnectionString,
            endpointCatalog.GetTenantDatabase(tenant.TenantId));

        var partitionEndpointConfiguration = EndpointFactory.Create(
            endpointName: partitionEndpoint.EndpointName,
            connectionString: tenantConnectionString,
            defaultSchema: partitionEndpoint.Schema,
            transactionMode: options.SqlTransport.TransactionMode,
            processingConcurrency: 1,
            addHandlers: cfg => cfg.AddHandler<PartitionedBusinessCommandHandler>(),
            routeToSelfMessageTypes: [typeof(PartitionedBusinessCommand)]);

        services.AddNServiceBusEndpoint(partitionEndpointConfiguration, partitionEndpoint.EndpointName);

        return services;
    }
}
