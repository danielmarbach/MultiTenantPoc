using Microsoft.EntityFrameworkCore;
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
builder.Services.AddDbContext<PocDbContext>(options => options.UseSqlServer(pocOptions.SqlTransport.ConnectionString));

foreach (var tenant in pocOptions.Tenants)
{
    var tenantMainEndpointName = endpointCatalog.GetMainEndpoint(tenant.TenantId);
    var mainEndpoint = EndpointFactory.Create(
        endpointName: tenantMainEndpointName,
        connectionString: pocOptions.SqlTransport.ConnectionString,
        defaultSchema: pocOptions.SqlTransport.DefaultSchema,
        transactionMode: pocOptions.SqlTransport.TransactionMode,
        processingConcurrency: Math.Max(1, tenant.MainEndpointConcurrency),
        addHandlers: cfg => cfg.AddHandler<BulkIngestionCommandHandler>(),
        routeToSelfMessageTypes: [typeof(BulkIngestionCommand)]);

    builder.Services.AddNServiceBusEndpoint(mainEndpoint, tenant.TenantId);

    foreach (var partitionEndpointName in endpointCatalog.GetPartitionEndpoints(tenant.TenantId))
    {
        var partitionEndpoint = EndpointFactory.Create(
            endpointName: partitionEndpointName,
            connectionString: pocOptions.SqlTransport.ConnectionString,
            defaultSchema: pocOptions.SqlTransport.DefaultSchema,
            transactionMode: pocOptions.SqlTransport.TransactionMode,
            processingConcurrency: 1,
            addHandlers: cfg => cfg.AddHandler<PartitionedBusinessCommandHandler>(),
            routeToSelfMessageTypes: [typeof(PartitionedBusinessCommand)]);

        builder.Services.AddNServiceBusEndpoint(partitionEndpoint, partitionEndpointName);
    }
}

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<PocDbContext>();
    await dbContext.Database.EnsureCreatedAsync();

    if (!await dbContext.Tenants.AnyAsync())
    {
        var tenants = pocOptions.Tenants
            .Select(t => new TenantRecord
            {
                TenantId = t.TenantId,
                CreatedUtc = DateTime.UtcNow
            });

        await dbContext.Tenants.AddRangeAsync(tenants);
        await dbContext.SaveChangesAsync();
    }
}

app.MapGet("/", () => Results.Ok(new
{
    name = "NServiceBus multi-tenant PoC",
    tenants = endpointCatalog.GetTenantIds()
}));

app.MapGet("/tenants", (EndpointCatalog catalog) => Results.Ok(catalog.Describe()));

var tenantApi = app.MapGroup("/api/{tenantId}")
    .AddEndpointFilter<TenantMessageSessionFilter>();

tenantApi.MapGet("/partition/{businessId:guid}", (string tenantId, Guid businessId, EndpointCatalog catalog) =>
{
    catalog.TryResolvePartitionEndpoint(tenantId, businessId, out var endpoint, out var partition);

    return Results.Ok(new
    {
        tenantId,
        businessId,
        partition,
        endpoint
    });
});

tenantApi.MapPost("/bulk", async (
    BulkIngestionRequest request,
    HttpContext httpContext,
    ILoggerFactory loggerFactory,
    CancellationToken cancellationToken) =>
{
    var tenantContext = httpContext.GetTenantContext();
    var tenantId = tenantContext.TenantId;

    var logger = loggerFactory.CreateLogger("Api");
    using var scope = logger.BeginScope(new Dictionary<string, object>
    {
        ["TenantId"] = tenantId,
        ["BusinessId"] = request.BusinessId,
        ["Route"] = "bulk"
    });

    var command = new BulkIngestionCommand
    {
        TenantId = tenantId,
        BusinessId = request.BusinessId,
        Payload = request.Payload
    };

    await tenantContext.MessageSession.Send(command, cancellationToken);
    logger.LogInformation("Sent bulk ingestion command using configured routing for tenant {TenantId}", tenantId);

    return Results.Accepted($"/api/{tenantId}/bulk", new
    {
        tenantId,
        businessId = request.BusinessId,
        mode = "local"
    });
});

tenantApi.MapPost("/business", async (
    PartitionedCommandRequest request,
    HttpContext httpContext,
    ILoggerFactory loggerFactory,
    CancellationToken cancellationToken) =>
{
    var tenantContext = httpContext.GetTenantContext();
    var partitionContext = httpContext.GetPartitionContext();
    var tenantId = tenantContext.TenantId;
    var partitionEndpoint = partitionContext.PartitionEndpoint;
    var partition = partitionContext.Partition;

    var logger = loggerFactory.CreateLogger("Api");
    using var scope = logger.BeginScope(new Dictionary<string, object>
    {
        ["TenantId"] = tenantId,
        ["BusinessId"] = request.BusinessId,
        ["Partition"] = partition,
        ["Route"] = "partitioned"
    });

    var command = new PartitionedBusinessCommand
    {
        TenantId = tenantId,
        BusinessId = request.BusinessId,
        Payload = request.Payload,
        Partition = partition
    };

    await partitionContext.MessageSession.Send(command, cancellationToken);
    logger.LogInformation("Sent partitioned business command using configured routing for endpoint {PartitionEndpoint}", partitionEndpoint);

    return Results.Accepted($"/api/{tenantId}/business", new
    {
        tenantId,
        businessId = request.BusinessId,
        partition,
        endpoint = partitionEndpoint,
        mode = "local"
    });
})
.AddEndpointFilter<PartitionMessageSessionFilter>();

app.Run();
