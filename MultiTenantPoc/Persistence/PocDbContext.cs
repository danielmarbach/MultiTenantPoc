using Microsoft.EntityFrameworkCore;

namespace MultiTenantPoc;

public sealed class PocDbContext(DbContextOptions<PocDbContext> options) : DbContext(options)
{
    public DbSet<TenantRecord> Tenants => Set<TenantRecord>();
    public DbSet<BulkIngestionMessage> BulkIngestionMessages => Set<BulkIngestionMessage>();
    public DbSet<PartitionedBusinessMessage> PartitionedBusinessMessages => Set<PartitionedBusinessMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TenantRecord>(entity =>
        {
            entity.ToTable("Tenants");
            entity.HasKey(x => x.TenantId);
            entity.Property(x => x.TenantId).HasMaxLength(128);
            entity.Property(x => x.CreatedUtc).IsRequired();
        });

        modelBuilder.Entity<BulkIngestionMessage>(entity =>
        {
            entity.ToTable("BulkIngestionMessages");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TenantId).HasMaxLength(128).IsRequired();
            entity.Property(x => x.BusinessId).HasMaxLength(256).IsRequired();
            entity.Property(x => x.Payload).HasMaxLength(4000);
            entity.Property(x => x.ReceivedUtc).IsRequired();
            entity.HasIndex(x => new { x.TenantId, x.BusinessId });
        });

        modelBuilder.Entity<PartitionedBusinessMessage>(entity =>
        {
            entity.ToTable("PartitionedBusinessMessages");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TenantId).HasMaxLength(128).IsRequired();
            entity.Property(x => x.BusinessId).HasMaxLength(256).IsRequired();
            entity.Property(x => x.Partition).IsRequired();
            entity.Property(x => x.Sequence).IsRequired();
            entity.Property(x => x.Payload).HasMaxLength(4000);
            entity.Property(x => x.ReceivedUtc).IsRequired();
            entity.HasIndex(x => new { x.TenantId, x.BusinessId, x.Partition });
        });
    }
}

public sealed class TenantRecord
{
    public required string TenantId { get; init; }
    public DateTime CreatedUtc { get; init; }
}

public sealed class BulkIngestionMessage
{
    public long Id { get; init; }
    public required string TenantId { get; init; }
    public required string BusinessId { get; init; }
    public string Payload { get; init; } = string.Empty;
    public DateTime ReceivedUtc { get; init; }
}

public sealed class PartitionedBusinessMessage
{
    public long Id { get; init; }
    public required string TenantId { get; init; }
    public required string BusinessId { get; init; }
    public int Partition { get; init; }
    public long Sequence { get; init; }
    public string Payload { get; init; } = string.Empty;
    public DateTime ReceivedUtc { get; init; }
}
