using Microsoft.Extensions.Options;
using MultiTenantPoc;
using NServiceBus;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddOptions<PocOptions>()
    .BindConfiguration(PocOptions.SectionName)
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<PocOptions>, PocOptionsValidator>();

builder.Logging.ClearProviders();
builder.Logging.AddProvider(new EndpointColorConsoleLoggerProvider());

var pocOptions = builder.Configuration.GetSection(PocOptions.SectionName).Get<PocOptions>()
    ?? throw new InvalidOperationException($"Configuration section '{PocOptions.SectionName}' is missing.");

var endpointCatalog = new EndpointCatalog(pocOptions);
builder.Services.AddSingleton(endpointCatalog);

foreach (var tenant in pocOptions.Tenants)
{
    var tenantMainEndpointName = endpointCatalog.GetMainEndpoint(tenant.TenantId);
    var mainEndpoint = EndpointFactory.Create(
        endpointName: tenantMainEndpointName,
        connectionString: pocOptions.SqlTransport.ConnectionString,
        defaultSchema: pocOptions.SqlTransport.DefaultSchema,
        processingConcurrency: Math.Max(1, tenant.MainEndpointConcurrency),
        addHandlers: cfg => cfg.AddHandler<BulkIngestionCommandHandler>());

    builder.Services.AddNServiceBusEndpoint(mainEndpoint, tenant.TenantId);

    foreach (var partitionEndpointName in endpointCatalog.GetPartitionEndpoints(tenant.TenantId))
    {
        var partitionEndpoint = EndpointFactory.Create(
            endpointName: partitionEndpointName,
            connectionString: pocOptions.SqlTransport.ConnectionString,
            defaultSchema: pocOptions.SqlTransport.DefaultSchema,
            processingConcurrency: 1,
            addHandlers: cfg => cfg.AddHandler<PartitionedBusinessCommandHandler>());

        builder.Services.AddNServiceBusEndpoint(partitionEndpoint, partitionEndpointName);
    }
}

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new
{
    name = "NServiceBus multi-tenant PoC",
    tenants = endpointCatalog.GetTenantIds()
}));

app.MapGet("/tenants", (EndpointCatalog catalog) => Results.Ok(catalog.Describe()));

app.MapPost("/api/{tenantId}/bulk", async (
    string tenantId,
    BulkIngestionRequest request,
    IServiceProvider serviceProvider,
    EndpointCatalog catalog,
    ILoggerFactory loggerFactory,
    CancellationToken cancellationToken) =>
{
    if (!catalog.TryGetMainEndpoint(tenantId, out var destinationEndpoint))
    {
        return Results.NotFound(new { error = $"Unknown tenant '{tenantId}'." });
    }

    var logger = loggerFactory.CreateLogger("Api");
    using var scope = logger.BeginScope(new Dictionary<string, object>
    {
        ["TenantId"] = tenantId,
        ["BusinessId"] = request.BusinessId,
        ["Route"] = "bulk"
    });

    var messageSession = serviceProvider.GetRequiredKeyedService<IMessageSession>(tenantId);
    var command = new BulkIngestionCommand
    {
        TenantId = tenantId,
        BusinessId = request.BusinessId,
        Payload = request.Payload
    };

    await messageSession.Send(destinationEndpoint, command, cancellationToken);
    logger.LogInformation("Sent bulk ingestion command to {DestinationEndpoint}", destinationEndpoint);

    return Results.Accepted($"/api/{tenantId}/bulk", new
    {
        tenantId,
        businessId = request.BusinessId,
        destination = destinationEndpoint
    });
});

app.MapPost("/api/{tenantId}/business", async (
    string tenantId,
    PartitionedCommandRequest request,
    IServiceProvider serviceProvider,
    EndpointCatalog catalog,
    ILoggerFactory loggerFactory,
    CancellationToken cancellationToken) =>
{
    if (!catalog.TryResolvePartitionEndpoint(tenantId, request.BusinessId, out var destinationEndpoint, out var partition))
    {
        return Results.NotFound(new { error = $"Unknown tenant '{tenantId}'." });
    }

    var logger = loggerFactory.CreateLogger("Api");
    using var scope = logger.BeginScope(new Dictionary<string, object>
    {
        ["TenantId"] = tenantId,
        ["BusinessId"] = request.BusinessId,
        ["Partition"] = partition,
        ["Route"] = "partitioned"
    });

    var messageSession = serviceProvider.GetRequiredKeyedService<IMessageSession>(tenantId);
    var command = new PartitionedBusinessCommand
    {
        TenantId = tenantId,
        BusinessId = request.BusinessId,
        Payload = request.Payload,
        Partition = partition
    };

    await messageSession.Send(destinationEndpoint, command, cancellationToken);
    logger.LogInformation("Sent partitioned business command to {DestinationEndpoint}", destinationEndpoint);

    return Results.Accepted($"/api/{tenantId}/business", new
    {
        tenantId,
        businessId = request.BusinessId,
        partition,
        destination = destinationEndpoint
    });
});

app.Run();
