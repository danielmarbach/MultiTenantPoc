using NServiceBus;

namespace MultiTenantPoc;

public sealed class BulkIngestionCommand : ICommand
{
    public required string TenantId { get; init; }
    public required Guid BusinessId { get; init; }
    public string Payload { get; init; } = string.Empty;
}

public sealed class PartitionedBusinessCommand : ICommand
{
    public required string TenantId { get; init; }
    public required Guid BusinessId { get; init; }
    public int Partition { get; init; }
    public string Payload { get; init; } = string.Empty;
}
