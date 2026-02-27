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
    public string MainSchema { get; init; } = "dbo";
    public string PartitionSchemaPrefix { get; init; } = "p";
    public string DatabasePrefix { get; init; } = "NsbPoc_";
    public string TransactionMode { get; init; } = "SendsAtomicWithReceive";
}

public sealed class TenantOptions
{
    public string TenantId { get; init; } = string.Empty;
    public string? DatabaseName { get; init; }
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

        if (string.IsNullOrWhiteSpace(options.SqlTransport.MainSchema))
        {
            errors.Add("Poc:SqlTransport:MainSchema is required.");
        }

        if (string.IsNullOrWhiteSpace(options.SqlTransport.PartitionSchemaPrefix))
        {
            errors.Add("Poc:SqlTransport:PartitionSchemaPrefix is required.");
        }

        if (string.IsNullOrWhiteSpace(options.SqlTransport.DatabasePrefix))
        {
            errors.Add("Poc:SqlTransport:DatabasePrefix is required.");
        }

        var allowedModes = new[] { "None", "ReceiveOnly", "SendsAtomicWithReceive", "TransactionScope" };
        if (!allowedModes.Contains(options.SqlTransport.TransactionMode, StringComparer.OrdinalIgnoreCase))
        {
            errors.Add($"Poc:SqlTransport:TransactionMode '{options.SqlTransport.TransactionMode}' is invalid.");
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

            if (!string.IsNullOrWhiteSpace(tenant.DatabaseName) && !IsSqlIdentifier(tenant.DatabaseName))
            {
                errors.Add($"Tenant database name '{tenant.DatabaseName}' is invalid.");
            }
        }

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }

    static bool IsSqlIdentifier(string value)
    {
        if (value.Length == 0 || value.Length > 128)
        {
            return false;
        }

        return value.All(ch => char.IsLetterOrDigit(ch) || ch is '_' or '-');
    }
}
