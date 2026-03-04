using Microsoft.EntityFrameworkCore;

namespace MultiTenantPoc;

public static class WebApplicationExtensions
{
    public static WebApplication UseOpenApi(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
        {
            return app;
        }

        app.MapOpenApi();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/openapi/v1.json", "MultiTenantPoc v1");
        });

        return app;
    }

    public static async Task EnsureTenantDatabasesCreatedAsync(
        this WebApplication app,
        PocOptions options,
        EndpointCatalog endpointCatalog)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var initializer = scope.ServiceProvider.GetRequiredService<TenantDatabaseInitializer>();
        await initializer.EnsureCreatedAsync(options, endpointCatalog);
    }

    public static WebApplication MapPocEndpoints(this WebApplication app, PocOptions options)
    {
        app.MapGet("/", (EndpointCatalog catalog) => Results.Ok(new
        {
            name = "NServiceBus multi-tenant PoC",
            tenants = catalog.GetTenantIds()
        }));

        app.MapGet("/tenants", (EndpointCatalog catalog) => Results.Ok(catalog.Describe()));

        var tenantApi = app.MapGroup("/api/{tenantId}")
            .AddEndpointFilter<TenantMessageSessionFilter>();

        tenantApi.MapGet("/partition/{businessId}", (string tenantId, string businessId, EndpointCatalog catalog) =>
        {
            catalog.TryResolvePartitionEndpoint(tenantId, businessId, out var endpoint, out var partition);

            return Results.Ok(new
            {
                tenantId,
                businessId,
                partition,
                endpoint
            });
        })
        .WithSummary("Resolve business ID partition")
        .WithDescription("Returns partition and endpoint for tenantId + businessId.");

        tenantApi.MapPost("/bulk", async (
            string tenantId,
            BulkIngestionRequest request,
            HttpContext httpContext,
            ILoggerFactory loggerFactory,
            CancellationToken cancellationToken) =>
        {
            var tenantContext = httpContext.GetTenantContext();

            var logger = loggerFactory.CreateLogger("Api");
            using var scope = BeginApiScope(logger, tenantId, request.BusinessId, "bulk");

            var command = new BulkIngestionCommand
            {
                TenantId = tenantId,
                BusinessId = request.BusinessId,
                Payload = request.Payload
            };

            await tenantContext.MessageSession.Send(command, cancellationToken);
            logger.LogInformation("Sent bulk ingestion command using configured routing for tenant {TenantId}", tenantContext.TenantId);

            return Results.Accepted($"/api/{tenantId}/bulk", new
            {
                tenantId,
                businessId = request.BusinessId,
                mode = "local"
            });
        })
        .WithSummary("Send bulk ingestion command");

        tenantApi.MapPost("/business", async (
            string tenantId,
            PartitionedCommandRequest request,
            HttpContext httpContext,
            ILoggerFactory loggerFactory,
            CancellationToken cancellationToken) =>
        {
            var tenantContext = httpContext.GetTenantContext();
            var partitionContext = httpContext.GetPartitionContext();
            var partitionEndpoint = partitionContext.PartitionEndpoint;
            var partition = partitionContext.Partition;

            var logger = loggerFactory.CreateLogger("Api");
            using var scope = BeginApiScope(logger, tenantId, request.BusinessId, "partitioned", partition);

            var command = new PartitionedBusinessCommand
            {
                TenantId = tenantId,
                BusinessId = request.BusinessId,
                Payload = request.Payload,
                Partition = partition
            };

            await partitionContext.MessageSession.Send(command, cancellationToken);
            logger.LogInformation("Sent partitioned business command using configured routing for endpoint {PartitionEndpoint} and tenant {TenantId}", partitionEndpoint, tenantContext.TenantId);

            return Results.Accepted($"/api/{tenantId}/business", new
            {
                tenantId,
                businessId = request.BusinessId,
                partition,
                endpoint = partitionEndpoint,
                mode = "local"
            });
        })
        .WithSummary("Send partitioned business command")
        .AddEndpointFilter<PartitionMessageSessionFilter>();

        tenantApi.MapPost("/saga", async (
            string tenantId,
            StartPartitionSagaRequest request,
            HttpContext httpContext,
            ILoggerFactory loggerFactory,
            CancellationToken cancellationToken) =>
        {
            var partitionContext = httpContext.GetPartitionContext();
            var partitionEndpoint = partitionContext.PartitionEndpoint;
            var partition = partitionContext.Partition;

            var logger = loggerFactory.CreateLogger("Api");
            using var scope = BeginApiScope(logger, tenantId, request.BusinessId, "saga", partition);

            var correlationId = Guid.NewGuid().ToString("N");
            await partitionContext.MessageSession.Send(new StartPartitionSagaCommand
            {
                CorrelationId = correlationId,
                TenantId = tenantId,
                BusinessId = request.BusinessId,
                Partition = partition,
                Payload = request.Payload
            }, cancellationToken);

            logger.LogInformation(
                "Sent partition saga start command using endpoint {PartitionEndpoint} for tenant {TenantId} with CorrelationId={CorrelationId}",
                partitionEndpoint,
                tenantId,
                correlationId);

            return Results.Accepted($"/api/{tenantId}/saga", new
            {
                tenantId,
                businessId = request.BusinessId,
                partition,
                endpoint = partitionEndpoint,
                correlationId,
                mode = "local"
            });
        })
        .WithSummary("Start partition saga")
        .AddEndpointFilter<PartitionMessageSessionFilter>();

        tenantApi.MapGet("/persisted", async (
            string tenantId,
            EndpointCatalog catalog,
            int? take,
            CancellationToken cancellationToken) =>
        {
            var rowLimit = Math.Clamp(take ?? 20, 1, 200);
            var tenantConnectionString = SqlConnectionStringBuilderFactory.ForDatabase(
                options.SqlTransport.ConnectionString,
                catalog.GetTenantDatabase(tenantId));

            var dbOptions = new DbContextOptionsBuilder<PocDbContext>()
                .UseSqlServer(tenantConnectionString)
                .Options;

            await using var dbContext = new PocDbContext(dbOptions);

            var bulk = await dbContext.BulkIngestionMessages
                .AsNoTracking()
                .OrderByDescending(x => x.Id)
                .Take(rowLimit)
                .Select(x => new
                {
                    x.Id,
                    x.TenantId,
                    x.BusinessId,
                    x.Payload,
                    x.ReceivedUtc
                })
                .ToListAsync(cancellationToken);

            var partitioned = await dbContext.PartitionedBusinessMessages
                .AsNoTracking()
                .OrderByDescending(x => x.Id)
                .Take(rowLimit)
                .Select(x => new
                {
                    x.Id,
                    x.TenantId,
                    x.BusinessId,
                    x.Partition,
                    x.Sequence,
                    x.Payload,
                    x.ReceivedUtc
                })
                .ToListAsync(cancellationToken);

            return Results.Ok(new
            {
                tenantId,
                take = rowLimit,
                bulk,
                partitioned
            });
        })
        .WithSummary("Inspect persisted handler rows")
        .WithDescription("Returns recent rows persisted by bulk and partition handlers for the tenant.");

        return app;
    }

    static IDisposable? BeginApiScope(ILogger logger, string tenantId, string businessId, string route, int? partition = null)
    {
        var scopeValues = new Dictionary<string, object>
        {
            ["TenantId"] = tenantId,
            ["BusinessId"] = businessId,
            ["Route"] = route
        };

        if (partition.HasValue)
        {
            scopeValues["Partition"] = partition.Value;
        }

        return logger.BeginScope(scopeValues);
    }
}
