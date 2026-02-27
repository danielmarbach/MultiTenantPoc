namespace MultiTenantPoc;

public sealed class TenantMessageSessionFilter : IEndpointFilter
{
    public const string TenantContextItemKey = "TenantContext";

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        if (!context.HttpContext.Request.RouteValues.TryGetValue("tenantId", out var routeValue) || routeValue is null)
        {
            return Results.BadRequest(new { error = "Route value 'tenantId' is required." });
        }

        var tenantId = routeValue.ToString();
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return Results.BadRequest(new { error = "Route value 'tenantId' is required." });
        }

        var catalog = context.HttpContext.RequestServices.GetRequiredService<EndpointCatalog>();
        if (!catalog.ContainsTenant(tenantId))
        {
            return Results.NotFound(new { error = $"Unknown tenant '{tenantId}'." });
        }

        var session = context.HttpContext.RequestServices.GetRequiredKeyedService<IMessageSession>(tenantId);

        context.HttpContext.Items[TenantContextItemKey] = new TenantRequestContext(tenantId, session);

        return await next(context);
    }
}

public sealed record TenantRequestContext(string TenantId, IMessageSession MessageSession);

public static class TenantRequestContextHttpExtensions
{
    public static TenantRequestContext GetTenantContext(this HttpContext httpContext)
    {
        if (httpContext.Items.TryGetValue(TenantMessageSessionFilter.TenantContextItemKey, out var value) && value is TenantRequestContext tenantContext)
        {
            return tenantContext;
        }

        throw new InvalidOperationException("Tenant context has not been initialized for this request.");
    }
}
