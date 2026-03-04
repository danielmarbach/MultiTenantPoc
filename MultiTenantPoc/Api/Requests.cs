namespace MultiTenantPoc;

/// <summary>
/// Request payload for bulk ingestion.
/// </summary>
/// <example>{"businessId":"import-batch-042","payload":"bulk-import"}</example>
/// <param name="BusinessId" example="import-batch-042">Business identifier used for correlation.</param>
/// <param name="Payload" example="bulk-import">Payload text for the PoC command.</param>
public sealed record BulkIngestionRequest(string BusinessId, string Payload);

/// <summary>
/// Request payload for partitioned processing.
/// </summary>
/// <example>{"businessId":"invoice-9917","payload":"process-order"}</example>
/// <param name="BusinessId" example="invoice-9917">Business identifier used to derive partition.</param>
/// <param name="Payload" example="process-order">Payload text for the PoC command.</param>
public sealed record PartitionedCommandRequest(string BusinessId, string Payload);

/// <summary>
/// Request payload for starting the partition saga.
/// </summary>
/// <example>{"businessId":"invoice-9917","payload":"start-saga"}</example>
/// <param name="BusinessId" example="invoice-9917">Business identifier used to derive partition.</param>
/// <param name="Payload" example="start-saga">Payload text for the PoC command.</param>
public sealed record StartPartitionSagaRequest(string BusinessId, string Payload);