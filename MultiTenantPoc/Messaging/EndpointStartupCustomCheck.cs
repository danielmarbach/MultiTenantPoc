using NServiceBus.CustomChecks;

namespace MultiTenantPoc;

public sealed class EndpointStartupCustomCheck(EndpointStartupCheckContext context)
    : CustomCheck(
        id: $"startup-{context.EndpointName}",
        category: $"tenant:{context.TenantId}/partition:{context.PartitionLabel}")
{
    public override Task<CheckResult> PerformCheck(CancellationToken cancellationToken = default)
        => CheckResult.Pass;
}

public sealed record EndpointStartupCheckContext(string EndpointName, string TenantId, string PartitionLabel);
