namespace MultiTenantPoc;

public sealed class BulkIngestionCommand : ICommand
{
    public required string TenantId { get; init; }
    public required string BusinessId { get; init; }
    public string Payload { get; init; } = string.Empty;
}

public sealed class PartitionedBusinessCommand : ICommand
{
    public required string TenantId { get; init; }
    public required string BusinessId { get; init; }
    public int Partition { get; init; }
    public string Payload { get; init; } = string.Empty;
}

public sealed class StartPartitionSagaCommand : ICommand
{
    public required string CorrelationId { get; init; }
    public required string TenantId { get; init; }
    public required string BusinessId { get; init; }
    public int Partition { get; init; }
    public string Payload { get; init; } = string.Empty;
}

public sealed class PartitionSagaProbeCommand : ICommand
{
    public required string CorrelationId { get; init; }
    public required string TenantId { get; init; }
    public required string BusinessId { get; init; }
    public int Partition { get; init; }
    public string Payload { get; init; } = string.Empty;
}

public sealed class PartitionSagaProbeReply : IMessage
{
    public required string CorrelationId { get; init; }
    public required string TenantId { get; init; }
    public required string BusinessId { get; init; }
    public int Partition { get; init; }
}

public sealed class PartitionSagaCompletionTimeout;