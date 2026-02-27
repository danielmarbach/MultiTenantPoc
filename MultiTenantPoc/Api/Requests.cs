namespace MultiTenantPoc;

public sealed record BulkIngestionRequest(Guid BusinessId, string Payload);

public sealed record PartitionedCommandRequest(Guid BusinessId, string Payload);
