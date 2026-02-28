using Microsoft.Extensions.Options;
using NServiceBus;

namespace MultiTenantPoc;

public sealed class TenantTrafficGeneratorHostedService(
    IServiceProvider serviceProvider,
    EndpointCatalog endpointCatalog,
    IOptionsMonitor<TrafficGeneratorOptions> optionsMonitor,
    ILogger<TenantTrafficGeneratorHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var startupDelay = TimeSpan.FromSeconds(Math.Max(0, optionsMonitor.CurrentValue.StartupDelaySeconds));

        if (startupDelay > TimeSpan.Zero)
        {
            await Task.Delay(startupDelay, stoppingToken);
        }

        var tenantIds = endpointCatalog.GetTenantIds().ToArray();
        logger.LogInformation("Starting tenant traffic generator for {TenantCount} tenants.", tenantIds.Length);

        var loops = tenantIds
            .Select(tenantId => RunTenantLoop(tenantId, stoppingToken))
            .ToArray();

        try
        {
            await Task.WhenAll(loops);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("Tenant traffic generator is stopping.");
        }
    }

    async Task RunTenantLoop(string tenantId, CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting tenant generator loop for {TenantId}", tenantId);

        var mainSession = serviceProvider.GetRequiredKeyedService<IMessageSession>(tenantId);
        bool? wasEnabled = null;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var generatorOptions = optionsMonitor.CurrentValue;
                var minDelayMs = Math.Max(100, generatorOptions.MinDelayMs);
                var maxDelayMs = Math.Max(minDelayMs + 1, generatorOptions.MaxDelayMs);

                if (!generatorOptions.Enabled)
                {
                    if (wasEnabled != false)
                    {
                        logger.LogInformation("Tenant generator loop paused for {TenantId}", tenantId);
                        wasEnabled = false;
                    }

                    await Task.Delay(1000, cancellationToken);
                    continue;
                }

                if (wasEnabled != true)
                {
                    logger.LogInformation(
                        "Tenant generator loop resumed for {TenantId} with delay range {MinDelayMs}-{MaxDelayMs} ms.",
                        tenantId,
                        minDelayMs,
                        maxDelayMs);
                    wasEnabled = true;
                }

                var businessId = $"bg-{tenantId}-{Guid.NewGuid():N}";
                var payload = $"payload-{Random.Shared.Next(1, 10_000)}";
                var sendBulk = Random.Shared.NextDouble() < 0.5;

                if (sendBulk)
                {
                    await mainSession.Send(new BulkIngestionCommand
                    {
                        TenantId = tenantId,
                        BusinessId = businessId,
                        Payload = payload
                    }, cancellationToken);
                }
                else
                {
                    endpointCatalog.TryResolvePartitionEndpoint(tenantId, businessId, out var partitionEndpoint, out var partition);
                    var partitionSession = serviceProvider.GetRequiredKeyedService<IMessageSession>(partitionEndpoint);

                    await partitionSession.Send(new PartitionedBusinessCommand
                    {
                        TenantId = tenantId,
                        BusinessId = businessId,
                        Partition = partition,
                        Payload = payload
                    }, cancellationToken);
                }

                await Task.Delay(Random.Shared.Next(minDelayMs, maxDelayMs), cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Tenant generator loop failed for {TenantId}. Retrying.", tenantId);
                await Task.Delay(1000, cancellationToken);
            }
        }

        logger.LogInformation("Stopped tenant generator loop for {TenantId}", tenantId);
    }
}
