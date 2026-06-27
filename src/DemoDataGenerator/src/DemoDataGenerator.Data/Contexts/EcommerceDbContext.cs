using DemoDataGenerator.Data.Entities.Ecommerce;
using Microsoft.EntityFrameworkCore;

namespace DemoDataGenerator.Data.Contexts;

public sealed class EcommerceDbContext : DbContext
{
    private readonly DemoDataConnectionOptions? connectionOptions;

    public EcommerceDbContext(DbContextOptions<EcommerceDbContext> options) : base(options) { }

    public EcommerceDbContext(DemoDataConnectionOptions connectionOptions) => this.connectionOptions = connectionOptions;

    public DbSet<Product> Products => Set<Product>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<Inventory> Inventories => Set<Inventory>();
    public DbSet<PointCampaign> PointCampaigns => Set<PointCampaign>();
    public DbSet<MemberBehavior> MemberBehaviors => Set<MemberBehavior>();
    public DbSet<PriceRule> PriceRules => Set<PriceRule>();
    public DbSet<ShippingRoute> ShippingRoutes => Set<ShippingRoute>();
    public DbSet<CampaignReaction> CampaignReactions => Set<CampaignReaction>();
    public DbSet<Member> Members => Set<Member>();
    public DbSet<Seller> Sellers => Set<Seller>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<PointEvent> PointEvents => Set<PointEvent>();
    public DbSet<ReturnCancellation> ReturnCancellations => Set<ReturnCancellation>();

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
        modelBuilder.Entity<Product>(e => { e.ToTable("products"); e.HasKey(x => x.Sku); Map(e); });
        modelBuilder.Entity<Order>(e => { e.ToTable("orders"); e.HasKey(x => x.OrderId); Map(e); e.HasIndex(x => x.MemberId); e.HasIndex(x => x.Sku); });
        modelBuilder.Entity<Inventory>(e => { e.ToTable("inventory"); e.HasKey(x => x.InventoryId); Map(e); e.HasIndex(x => x.Sku); });
        modelBuilder.Entity<PointCampaign>(e => { e.ToTable("point_campaigns"); e.HasKey(x => x.CampaignId); Map(e); e.HasIndex(x => x.MemberId); });
        modelBuilder.Entity<MemberBehavior>(e => { e.ToTable("member_behaviors"); e.HasKey(x => x.EventId); Map(e); e.HasIndex(x => x.MemberId); });
        modelBuilder.Entity<PriceRule>(e => { e.ToTable("price_rules"); e.HasKey(x => x.PriceRuleId); Map(e); e.HasIndex(x => x.Sku); });
        modelBuilder.Entity<ShippingRoute>(e => { e.ToTable("shipping_routes"); e.HasKey(x => x.ShippingRouteId); Map(e); });
        modelBuilder.Entity<CampaignReaction>(e => { e.ToTable("campaign_reactions"); e.HasKey(x => x.ReactionId); Map(e); e.HasIndex(x => x.MemberId); e.HasIndex(x => x.CampaignId); });
        modelBuilder.Entity<Member>(e => { e.ToTable("members"); e.HasKey(x => x.MemberId); Map(e); });
        modelBuilder.Entity<Seller>(e => { e.ToTable("sellers"); e.HasKey(x => x.SellerId); Map(e); });
        modelBuilder.Entity<Category>(e => { e.ToTable("categories"); e.HasKey(x => x.CategoryId); Map(e); });
        modelBuilder.Entity<PointEvent>(e => { e.ToTable("point_events"); e.HasKey(x => x.PointEventId); Map(e); e.HasIndex(x => x.MemberId); });
        modelBuilder.Entity<ReturnCancellation>(e => { e.ToTable("return_cancellations"); e.HasKey(x => x.ReturnId); Map(e); e.HasIndex(x => x.OrderId); });
    }

    private static void Map<TEntity>(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<TEntity> e) where TEntity : class
    {
        foreach (var property in e.Metadata.GetProperties())
            property.SetColumnName(ToSnakeCase(property.Name));
    }

    private static string ToSnakeCase(string value) => string.Concat(value.Select((c, i) => i > 0 && char.IsUpper(c) ? "_" + char.ToLowerInvariant(c) : char.ToLowerInvariant(c).ToString()));
}
