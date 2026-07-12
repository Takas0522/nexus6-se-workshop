using DemoDataGenerator.Data.Entities.Common;
using Microsoft.EntityFrameworkCore;

namespace DemoDataGenerator.Data.Contexts;

public sealed class CommonDbContext : DbContext
{
    private readonly DemoDataConnectionOptions? connectionOptions;

    public CommonDbContext(DbContextOptions<CommonDbContext> options) : base(options) { }

    public CommonDbContext(DemoDataConnectionOptions connectionOptions) => this.connectionOptions = connectionOptions;

    public DbSet<UnifiedCustomer> UnifiedCustomers => Set<UnifiedCustomer>();
    public DbSet<DomainIdMapping> DomainIdMappings => Set<DomainIdMapping>();
    public DbSet<CustomerSegmentMaster> CustomerSegmentMasters => Set<CustomerSegmentMaster>();
    public DbSet<CustomerSegmentAssignment> CustomerSegmentAssignments => Set<CustomerSegmentAssignment>();
    public DbSet<CustomerIntegrationEvent> CustomerIntegrationEvents => Set<CustomerIntegrationEvent>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured && connectionOptions is not null)
        {
            if (connectionOptions.Target == DemoDataTarget.FabricSql)
                optionsBuilder.UseSqlServer(connectionOptions.ConnectionString);
            else
                optionsBuilder.UseSqlite(connectionOptions.ConnectionString);
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UnifiedCustomer>(e => { e.ToTable("unified_customers"); e.HasKey(x => x.UnifiedCustomerId); Map(e); });
        modelBuilder.Entity<DomainIdMapping>(e => { e.ToTable("domain_id_mappings"); e.HasKey(x => x.MapId); Map(e); e.HasIndex(x => new { x.Domain, x.DomainCustomerId }); });
        modelBuilder.Entity<CustomerSegmentMaster>(e => { e.ToTable("customer_segment_masters"); e.HasKey(x => x.SegmentId); Map(e); });
        modelBuilder.Entity<CustomerSegmentAssignment>(e => { e.ToTable("customer_segment_assignments"); e.HasKey(x => x.AssignmentId); Map(e); });
        modelBuilder.Entity<CustomerIntegrationEvent>(e => { e.ToTable("customer_integration_events"); e.HasKey(x => x.EventId); Map(e); });
    }

    private static void Map<TEntity>(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<TEntity> e) where TEntity : class
    {
        foreach (var property in e.Metadata.GetProperties())
            property.SetColumnName(ToSnakeCase(property.Name));
    }

    private static string ToSnakeCase(string value) => string.Concat(value.Select((c, i) => i > 0 && char.IsUpper(c) ? "_" + char.ToLowerInvariant(c) : char.ToLowerInvariant(c).ToString()));
}
