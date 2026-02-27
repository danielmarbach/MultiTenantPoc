using System.Collections.Concurrent;
using NServiceBus;

namespace MultiTenantPoc;

public sealed class BulkIngestionCommandHandler(ILogger<BulkIngestionCommandHandler> logger)
    : IHandleMessages<BulkIngestionCommand>
{
    public Task Handle(BulkIngestionCommand message, IMessageHandlerContext context)
    {
        logger.LogInformation(
            "Handled bulk ingestion command for TenantId={TenantId}, BusinessId={BusinessId}, Payload={Payload}",
            message.TenantId,
            message.BusinessId,
            message.Payload);

        return Task.CompletedTask;
    }
}

public sealed class PartitionedBusinessCommandHandler(ILogger<PartitionedBusinessCommandHandler> logger)
    : IHandleMessages<PartitionedBusinessCommand>
{
    static readonly ConcurrentDictionary<string, long> MessageOrder = new();

    public Task Handle(PartitionedBusinessCommand message, IMessageHandlerContext context)
    {
        var key = $"{message.TenantId}:{message.BusinessId}";
        var sequence = MessageOrder.AddOrUpdate(key, 1, (_, current) => current + 1);

        logger.LogInformation(
            "Handled partitioned command for TenantId={TenantId}, BusinessId={BusinessId}, Partition={Partition}, Sequence={Sequence}, Payload={Payload}",
            message.TenantId,
            message.BusinessId,
            message.Partition,
            sequence,
            message.Payload);

        return Task.CompletedTask;
    }
}
