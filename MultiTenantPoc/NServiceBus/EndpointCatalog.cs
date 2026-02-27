namespace MultiTenantPoc;

public sealed class EndpointCatalog
{
    readonly Dictionary<string, string> mainEndpoints;
    readonly Dictionary<string, string> tenantDatabases;
    readonly Dictionary<string, IReadOnlyList<PartitionEndpointDescriptor>> partitionEndpoints;

    public EndpointCatalog(PocOptions options)
    {
        mainEndpoints = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        tenantDatabases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        partitionEndpoints = new Dictionary<string, IReadOnlyList<PartitionEndpointDescriptor>>(StringComparer.OrdinalIgnoreCase);

        foreach (var tenant in options.Tenants)
        {
            var normalizedTenant = Normalize(tenant.TenantId);
            var main = $"{options.EndpointPrefix}-{normalizedTenant}";
            var database = tenant.DatabaseName ?? $"{options.SqlTransport.DatabasePrefix}{normalizedTenant}";
            var partitions = Enumerable
                .Range(0, options.PartitionsPerTenant)
                .Select(index => new PartitionEndpointDescriptor(
                    index,
                    $"{main}-p{index}",
                    $"{options.SqlTransport.PartitionSchemaPrefix}{index}"))
                .ToArray();

            mainEndpoints[tenant.TenantId] = main;
            tenantDatabases[tenant.TenantId] = database;
            partitionEndpoints[tenant.TenantId] = partitions;
        }
    }

    public IReadOnlyCollection<string> GetTenantIds() => mainEndpoints.Keys.ToArray();

    public bool ContainsTenant(string tenantId) => mainEndpoints.ContainsKey(tenantId);

    public string GetMainEndpoint(string tenantId) => mainEndpoints[tenantId];

    public string GetTenantDatabase(string tenantId) => tenantDatabases[tenantId];

    public bool TryGetMainEndpoint(string tenantId, out string endpoint)
        => mainEndpoints.TryGetValue(tenantId, out endpoint!);

    public IReadOnlyList<PartitionEndpointDescriptor> GetPartitionEndpoints(string tenantId) => partitionEndpoints[tenantId];

    public IReadOnlyList<string> GetPartitionSchemas(string tenantId)
        => partitionEndpoints[tenantId].Select(p => p.Schema).ToArray();

    public bool TryResolvePartitionEndpoint(string tenantId, string businessId, out string endpoint, out int partition)
    {
        endpoint = string.Empty;
        partition = 0;

        if (!partitionEndpoints.TryGetValue(tenantId, out var endpoints))
        {
            return false;
        }

        partition = ResolvePartition(businessId, endpoints.Count);
        endpoint = endpoints[partition].EndpointName;
        return true;
    }

    public object Describe() => mainEndpoints.Keys
        .Select(tenant => new
        {
            tenantId = tenant,
            mainEndpoint = mainEndpoints[tenant],
            database = tenantDatabases[tenant],
            partitions = partitionEndpoints[tenant]
                .Select(p => new { index = p.Partition, endpoint = p.EndpointName, schema = p.Schema })
        })
        .ToArray();

    static int ResolvePartition(string businessId, int partitionCount)
    {
        var hash = (uint)StringComparer.OrdinalIgnoreCase.GetHashCode(businessId);
        return (int)(hash % (uint)partitionCount);
    }

    static string Normalize(string tenantId)
    {
        var chars = tenantId
            .Trim()
            .ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
            .ToArray();

        return new string(chars);
    }
}

public sealed record PartitionEndpointDescriptor(int Partition, string EndpointName, string Schema);
