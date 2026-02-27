using Microsoft.Extensions.Options;

namespace MultiTenantPoc;

public sealed class PocOptions
{
    public const string SectionName = "Poc";

    public string EndpointPrefix { get; init; } = "tenant";
    public int PartitionsPerTenant { get; init; } = 3;
    public SqlTransportOptions SqlTransport { get; init; } = new();
    public List<TenantOptions> Tenants { get; init; } = [];
}

public sealed class SqlTransportOptions
{
    public string ConnectionString { get; init; } = string.Empty;
    public string DefaultSchema { get; init; } = "dbo";
}

public sealed class TenantOptions
{
    public string TenantId { get; init; } = string.Empty;
    public int MainEndpointConcurrency { get; init; } = 2;
}

public sealed class PocOptionsValidator : IValidateOptions<PocOptions>
{
    public ValidateOptionsResult Validate(string? name, PocOptions options)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(options.EndpointPrefix))
        {
            errors.Add("Poc:EndpointPrefix is required.");
        }

        if (options.PartitionsPerTenant is < 1 or > 32)
        {
            errors.Add("Poc:PartitionsPerTenant must be between 1 and 32.");
        }

        if (string.IsNullOrWhiteSpace(options.SqlTransport.ConnectionString))
        {
            errors.Add("Poc:SqlTransport:ConnectionString is required.");
        }

        if (options.Tenants.Count == 0)
        {
            errors.Add("Poc:Tenants must contain at least one tenant.");
        }

        var tenantIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var tenant in options.Tenants)
        {
            if (string.IsNullOrWhiteSpace(tenant.TenantId))
            {
                errors.Add("Each tenant requires a non-empty TenantId.");
                continue;
            }

            if (!tenantIds.Add(tenant.TenantId))
            {
                errors.Add($"Duplicate tenant id '{tenant.TenantId}'.");
            }
        }

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}
