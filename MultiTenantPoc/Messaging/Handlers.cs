using System.Collections.Concurrent;

namespace MultiTenantPoc;

public sealed class BulkIngestionCommandHandler(ILogger<BulkIngestionCommandHandler> logger, PocDbContext dbContext)
    : IHandleMessages<BulkIngestionCommand>
{
    static long processedCount;

    public async Task Handle(BulkIngestionCommand message, IMessageHandlerContext context)
    {
        await dbContext.BulkIngestionMessages.AddAsync(new BulkIngestionMessage
        {
            TenantId = message.TenantId,
            BusinessId = message.BusinessId,
            Payload = message.Payload,
            ReceivedUtc = DateTime.UtcNow
        }, context.CancellationToken);

        logger.LogInformation(
            "Handled bulk ingestion command for TenantId={TenantId}, BusinessId={BusinessId}, Payload={Payload}",
            message.TenantId,
            message.BusinessId,
            message.Payload);

        var current = Interlocked.Increment(ref processedCount);
        if (current % 10 == 0)
        {
            throw new SimulatedUnrecoverableException($"Simulated unrecoverable failure in bulk handler at message {current}.");
        }

        if (current % 5 == 0)
        {
            throw new InvalidOperationException($"Simulated recoverable failure in bulk handler at message {current}.");
        }
    }
}

public sealed class PartitionedBusinessCommandHandler(ILogger<PartitionedBusinessCommandHandler> logger, PocDbContext dbContext)
    : IHandleMessages<PartitionedBusinessCommand>
{
    static readonly ConcurrentDictionary<string, long> MessageOrder = new();
    static long processedCount;

    public async Task Handle(PartitionedBusinessCommand message, IMessageHandlerContext context)
    {
        var key = $"{message.TenantId}:{message.BusinessId}";
        var sequence = MessageOrder.AddOrUpdate(key, 1, (_, current) => current + 1);

        await dbContext.PartitionedBusinessMessages.AddAsync(new PartitionedBusinessMessage
        {
            TenantId = message.TenantId,
            BusinessId = message.BusinessId,
            Partition = message.Partition,
            Sequence = sequence,
            Payload = message.Payload,
            ReceivedUtc = DateTime.UtcNow
        }, context.CancellationToken);

        logger.LogInformation(
            "Handled partitioned command for TenantId={TenantId}, BusinessId={BusinessId}, Partition={Partition}, Sequence={Sequence}, Payload={Payload}",
            message.TenantId,
            message.BusinessId,
            message.Partition,
            sequence,
            message.Payload);

        var current = Interlocked.Increment(ref processedCount);
        if (current % 10 == 0)
        {
            throw new SimulatedUnrecoverableException($"Simulated unrecoverable failure in partition handler at message {current}.");
        }

        if (current % 5 == 0)
        {
            throw new InvalidOperationException($"Simulated recoverable failure in partition handler at message {current}.");
        }
    }
}