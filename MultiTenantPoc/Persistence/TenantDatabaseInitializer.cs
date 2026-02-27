using Microsoft.EntityFrameworkCore;

namespace MultiTenantPoc;

public sealed class TenantDatabaseInitializer(ILogger<TenantDatabaseInitializer> logger)
{
    public async Task EnsureCreatedAsync(PocOptions options, EndpointCatalog catalog, CancellationToken cancellationToken = default)
    {
        foreach (var tenantId in catalog.GetTenantIds())
        {
            var tenantDatabase = catalog.GetTenantDatabase(tenantId);
            var tenantConnectionString = SqlConnectionStringBuilderFactory.ForDatabase(options.SqlTransport.ConnectionString, tenantDatabase);

            var dbOptions = new DbContextOptionsBuilder<PocDbContext>()
                .UseSqlServer(tenantConnectionString)
                .Options;

            await using var dbContext = new PocDbContext(dbOptions);
            await dbContext.Database.EnsureCreatedAsync(cancellationToken);

            var partitionSchemas = catalog.GetPartitionSchemas(tenantId);
            foreach (var schema in partitionSchemas)
            {
                await dbContext.Database.ExecuteSqlAsync($"IF SCHEMA_ID({schema}) IS NULL EXEC(N'CREATE SCHEMA ' + QUOTENAME({schema}))", cancellationToken);
            }

            if (!await dbContext.Tenants.AnyAsync(x => x.TenantId == tenantId, cancellationToken))
            {
                await dbContext.Tenants.AddAsync(new TenantRecord
                {
                    TenantId = tenantId,
                    CreatedUtc = DateTime.UtcNow
                }, cancellationToken);

                await dbContext.SaveChangesAsync(cancellationToken);
            }

            logger.LogInformation("Ensured tenant database {TenantDatabase} with schemas {Schemas}", tenantDatabase, string.Join(",", partitionSchemas));
        }
    }
}
