namespace MultiTenantPoc;

public sealed class EndpointCatalog
{
    readonly Dictionary<string, string> mainEndpoints;
    readonly Dictionary<string, IReadOnlyList<string>> partitionEndpoints;

    public EndpointCatalog(PocOptions options)
    {
        mainEndpoints = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        partitionEndpoints = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var tenant in options.Tenants)
        {
            var normalizedTenant = Normalize(tenant.TenantId);
            var main = $"{options.EndpointPrefix}-{normalizedTenant}";
            var partitions = Enumerable
                .Range(0, options.PartitionsPerTenant)
                .Select(index => $"{main}-p{index}")
                .ToArray();

            mainEndpoints[tenant.TenantId] = main;
            partitionEndpoints[tenant.TenantId] = partitions;
        }
    }

    public IReadOnlyCollection<string> GetTenantIds() => mainEndpoints.Keys.ToArray();

    public string GetMainEndpoint(string tenantId) => mainEndpoints[tenantId];

    public bool TryGetMainEndpoint(string tenantId, out string endpoint)
        => mainEndpoints.TryGetValue(tenantId, out endpoint!);

    public IReadOnlyList<string> GetPartitionEndpoints(string tenantId) => partitionEndpoints[tenantId];

    public bool TryResolvePartitionEndpoint(string tenantId, Guid businessId, out string endpoint, out int partition)
    {
        endpoint = string.Empty;
        partition = 0;

        if (!partitionEndpoints.TryGetValue(tenantId, out var endpoints))
        {
            return false;
        }

        partition = ResolvePartition(businessId, endpoints.Count);
        endpoint = endpoints[partition];
        return true;
    }

    public object Describe() => mainEndpoints.Keys
        .Select(tenant => new
        {
            tenantId = tenant,
            mainEndpoint = mainEndpoints[tenant],
            partitions = partitionEndpoints[tenant]
                .Select((name, index) => new { index, endpoint = name })
        })
        .ToArray();

    static int ResolvePartition(Guid businessId, int partitionCount)
    {
        var firstByte = businessId.ToByteArray()[0];
        return firstByte % partitionCount;
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
