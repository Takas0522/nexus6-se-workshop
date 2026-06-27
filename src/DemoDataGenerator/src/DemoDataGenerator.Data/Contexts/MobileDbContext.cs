using DemoDataGenerator.Data.Entities.Mobile;
using Microsoft.EntityFrameworkCore;

namespace DemoDataGenerator.Data.Contexts;

public sealed class MobileDbContext : DbContext
{
    private readonly DemoDataConnectionOptions? connectionOptions;

    public MobileDbContext(DbContextOptions<MobileDbContext> options) : base(options) { }

    public MobileDbContext(DemoDataConnectionOptions connectionOptions) => this.connectionOptions = connectionOptions;

    public DbSet<MobileCustomer> Customers => Set<MobileCustomer>();
    public DbSet<MobileContract> Contracts => Set<MobileContract>();
    public DbSet<MobileUsageBilling> UsageBillings => Set<MobileUsageBilling>();
    public DbSet<MobileCostItem> CostItems => Set<MobileCostItem>();
    public DbSet<MobileTicket> Tickets => Set<MobileTicket>();
    public DbSet<MobilePlanRule> PlanRules => Set<MobilePlanRule>();
    public DbSet<MobileDeviceSku> DeviceSkus => Set<MobileDeviceSku>();
    public DbSet<MobileCampaignAction> CampaignActions => Set<MobileCampaignAction>();
    public DbSet<NetworkBaseStation> NetworkBaseStations => Set<NetworkBaseStation>();
    public DbSet<MnpHistory> MnpHistory => Set<MnpHistory>();
    public DbSet<MobileInstallment> Installments => Set<MobileInstallment>();
    public DbSet<MobileOption> Options => Set<MobileOption>();
    public DbSet<MobileCustomerOption> CustomerOptions => Set<MobileCustomerOption>();

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
        modelBuilder.Entity<MobileCustomer>(e => { e.ToTable("mobile_customers"); e.HasKey(x => x.CustomerId); Map(e); });
        modelBuilder.Entity<MobileContract>(e => { e.ToTable("mobile_contracts"); e.HasKey(x => x.ContractId); Map(e); e.HasIndex(x => x.CustomerId); });
        modelBuilder.Entity<MobileUsageBilling>(e => { e.ToTable("mobile_usage_billings"); e.HasKey(x => x.UsageId); Map(e); e.HasIndex(x => x.ContractId); });
        modelBuilder.Entity<MobileCostItem>(e => { e.ToTable("mobile_cost_items"); e.HasKey(x => x.CostItemId); Map(e); });
        modelBuilder.Entity<MobileTicket>(e => { e.ToTable("mobile_tickets"); e.HasKey(x => x.TicketId); Map(e); e.HasIndex(x => x.CustomerId); });
        modelBuilder.Entity<MobilePlanRule>(e => { e.ToTable("mobile_plan_rules"); e.HasKey(x => x.PlanRuleId); Map(e); });
        modelBuilder.Entity<MobileDeviceSku>(e => { e.ToTable("mobile_device_skus"); e.HasKey(x => x.DeviceSkuId); Map(e); });
        modelBuilder.Entity<MobileCampaignAction>(e => { e.ToTable("mobile_campaign_actions"); e.HasKey(x => x.ActionId); Map(e); e.HasIndex(x => x.CustomerId); });
        modelBuilder.Entity<NetworkBaseStation>(e => { e.ToTable("network_base_stations"); e.HasKey(x => x.StationId); Map(e); });
        modelBuilder.Entity<MnpHistory>(e => { e.ToTable("mnp_history"); e.HasKey(x => x.MnpId); Map(e); e.HasIndex(x => x.CustomerId); });
        modelBuilder.Entity<MobileInstallment>(e => { e.ToTable("mobile_installments"); e.HasKey(x => x.InstallmentId); Map(e); e.HasIndex(x => x.ContractId); });
        modelBuilder.Entity<MobileOption>(e => { e.ToTable("mobile_options"); e.HasKey(x => x.OptionId); Map(e); });
        modelBuilder.Entity<MobileCustomerOption>(e => { e.ToTable("mobile_customer_options"); e.HasKey(x => x.CustomerOptionId); Map(e); e.HasIndex(x => x.CustomerId); });
    }

    private static void Map<TEntity>(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<TEntity> e) where TEntity : class
    {
        foreach (var property in e.Metadata.GetProperties())
            property.SetColumnName(ToSnakeCase(property.Name));
    }

    private static string ToSnakeCase(string value) => string.Concat(value.Select((c, i) => i > 0 && char.IsUpper(c) ? "_" + char.ToLowerInvariant(c) : char.ToLowerInvariant(c).ToString()));
}
