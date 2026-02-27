using NServiceBus;

namespace MultiTenantPoc;

public sealed class PartitionMessageSessionFilter : IEndpointFilter
{
    public const string PartitionContextItemKey = "PartitionContext";

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var tenantContext = context.HttpContext.GetTenantContext();
        var request = context.Arguments.OfType<PartitionedCommandRequest>().FirstOrDefault();
        if (request is null)
        {
            return Results.BadRequest(new { error = "Partitioned command request is required." });
        }

        var catalog = context.HttpContext.RequestServices.GetRequiredService<EndpointCatalog>();
        catalog.TryResolvePartitionEndpoint(tenantContext.TenantId, request.BusinessId, out var partitionEndpoint, out var partition);

        var partitionSession = context.HttpContext.RequestServices.GetRequiredKeyedService<IMessageSession>(partitionEndpoint);

        context.HttpContext.Items[PartitionContextItemKey] = new PartitionRequestContext(partition, partitionEndpoint, partitionSession);

        return await next(context);
    }
}

public sealed record PartitionRequestContext(int Partition, string PartitionEndpoint, IMessageSession MessageSession);

public static class PartitionRequestContextHttpExtensions
{
    public static PartitionRequestContext GetPartitionContext(this HttpContext httpContext)
    {
        if (httpContext.Items.TryGetValue(PartitionMessageSessionFilter.PartitionContextItemKey, out var value) && value is PartitionRequestContext partitionContext)
        {
            return partitionContext;
        }

        throw new InvalidOperationException("Partition context has not been initialized for this request.");
    }
}
