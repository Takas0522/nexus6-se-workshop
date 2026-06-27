using DemoDataGenerator.Data.Entities.Fintech;
using Microsoft.EntityFrameworkCore;

namespace DemoDataGenerator.Data.Contexts;

public sealed class FintechDbContext : DbContext
{
    private readonly DemoDataConnectionOptions? connectionOptions;

    public FintechDbContext(DbContextOptions<FintechDbContext> options) : base(options) { }

    public FintechDbContext(DemoDataConnectionOptions connectionOptions) => this.connectionOptions = connectionOptions;

    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<CardTransaction> CardTransactions => Set<CardTransaction>();
    public DbSet<FxPosition> FxPositions => Set<FxPosition>();
    public DbSet<CreditReview> CreditReviews => Set<CreditReview>();
    public DbSet<RevenueRisk> RevenueRisks => Set<RevenueRisk>();
    public DbSet<ProductRule> ProductRules => Set<ProductRule>();
    public DbSet<RiskEvent> RiskEvents => Set<RiskEvent>();
    public DbSet<AccountLink> AccountLinks => Set<AccountLink>();
    public DbSet<LoanBalance> LoanBalances => Set<LoanBalance>();
    public DbSet<Merchant> Merchants => Set<Merchant>();
    public DbSet<FxRateSnapshot> FxRateSnapshots => Set<FxRateSnapshot>();
    public DbSet<KycRecord> KycRecords => Set<KycRecord>();
    public DbSet<TransactionAlert> TransactionAlerts => Set<TransactionAlert>();

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
        modelBuilder.Entity<Account>(e => { e.ToTable("accounts"); e.HasKey(x => x.AccountId); Map(e); e.HasIndex(x => x.UserId); });
        modelBuilder.Entity<CardTransaction>(e => { e.ToTable("card_transactions"); e.HasKey(x => x.TransactionId); Map(e); e.HasIndex(x => x.UserId); });
        modelBuilder.Entity<FxPosition>(e => { e.ToTable("fx_positions"); e.HasKey(x => x.PositionId); Map(e); e.HasIndex(x => x.UserId); });
        modelBuilder.Entity<CreditReview>(e => { e.ToTable("credit_reviews"); e.HasKey(x => x.ReviewId); Map(e); e.HasIndex(x => x.UserId); });
        modelBuilder.Entity<RevenueRisk>(e => { e.ToTable("revenue_risks"); e.HasKey(x => x.RevenueId); Map(e); });
        modelBuilder.Entity<ProductRule>(e => { e.ToTable("product_rules"); e.HasKey(x => x.ProductRuleId); Map(e); });
        modelBuilder.Entity<RiskEvent>(e => { e.ToTable("risk_events"); e.HasKey(x => x.RiskEventId); Map(e); e.HasIndex(x => x.UserId); });
        modelBuilder.Entity<AccountLink>(e => { e.ToTable("account_links"); e.HasKey(x => x.LinkId); Map(e); e.HasIndex(x => x.UserId); });
        modelBuilder.Entity<LoanBalance>(e => { e.ToTable("loan_balances"); e.HasKey(x => x.LoanId); Map(e); e.HasIndex(x => x.UserId); });
        modelBuilder.Entity<Merchant>(e => { e.ToTable("merchants"); e.HasKey(x => x.MerchantId); Map(e); });
        modelBuilder.Entity<FxRateSnapshot>(e => { e.ToTable("fx_rate_snapshots"); e.HasKey(x => x.RateSnapshotId); Map(e); });
        modelBuilder.Entity<KycRecord>(e => { e.ToTable("kyc_records"); e.HasKey(x => x.KycId); Map(e); e.HasIndex(x => x.UserId); });
        modelBuilder.Entity<TransactionAlert>(e => { e.ToTable("transaction_alerts"); e.HasKey(x => x.AlertId); Map(e); e.HasIndex(x => x.UserId); });
    }

    private static void Map<TEntity>(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<TEntity> e) where TEntity : class
    {
        foreach (var property in e.Metadata.GetProperties())
            property.SetColumnName(ToSnakeCase(property.Name));
    }

    private static string ToSnakeCase(string value) => string.Concat(value.Select((c, i) => i > 0 && char.IsUpper(c) ? "_" + char.ToLowerInvariant(c) : char.ToLowerInvariant(c).ToString()));
}
