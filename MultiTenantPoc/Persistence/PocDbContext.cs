using Microsoft.EntityFrameworkCore;

namespace MultiTenantPoc;

public sealed class PocDbContext(DbContextOptions<PocDbContext> options) : DbContext(options)
{
    public DbSet<TenantRecord> Tenants => Set<TenantRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TenantRecord>(entity =>
        {
            entity.ToTable("Tenants");
            entity.HasKey(x => x.TenantId);
            entity.Property(x => x.TenantId).HasMaxLength(128);
            entity.Property(x => x.CreatedUtc).IsRequired();
        });
    }
}

public sealed class TenantRecord
{
    public required string TenantId { get; init; }
    public DateTime CreatedUtc { get; init; }
}
