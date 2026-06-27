using DemoDataGenerator.Data.Contexts;
using DemoDataGenerator.Data.Entities.Common;
using DemoDataGenerator.Data.Entities.Ecommerce;
using DemoDataGenerator.Data.Entities.Fintech;
using DemoDataGenerator.Data.Entities.Mobile;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace DemoDataGenerator.Data.Seeding;

public sealed class DemoDataGenerationService(
    CommonDbContext commonDb,
    MobileDbContext mobileDb,
    EcommerceDbContext ecommerceDb,
    FintechDbContext fintechDb)
{
    private static readonly DateTime BaseNow = new(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly string[] Regions = ["関東", "関西", "中部", "九州", "北海道"];
    private static readonly string[] AgeBands = ["20s", "30s", "40s", "50s", "60s+"];

    public async Task EnsureSchemaAsync(bool recreateLocalSqlite, CancellationToken cancellationToken = default)
    {
        if (recreateLocalSqlite)
        {
            await commonDb.Database.EnsureDeletedAsync(cancellationToken);
            await commonDb.Database.EnsureCreatedAsync(cancellationToken);
        }
        else
        {
            await CreateTablesAsync(commonDb, cancellationToken);
        }

        await CreateTablesAsync(mobileDb, cancellationToken);
        await CreateTablesAsync(ecommerceDb, cancellationToken);
        await CreateTablesAsync(fintechDb, cancellationToken);
    }

    public async Task ClearDataAsync(CancellationToken cancellationToken = default)
    {
        await ClearContextAsync(commonDb, cancellationToken);
        await ClearContextAsync(mobileDb, cancellationToken);
        await ClearContextAsync(ecommerceDb, cancellationToken);
        await ClearContextAsync(fintechDb, cancellationToken);
    }

    public async Task<GenerationSummary> GenerateAsync(DemoDataRequest request, CancellationToken cancellationToken = default)
    {
        var rnd = new Random(request.Seed + ((int)request.Scenario * 1000));
        var customers = BuildCommonCustomers(request.CustomerCount, rnd).ToList();
        await commonDb.UnifiedCustomers.AddRangeAsync(customers, cancellationToken);
        await commonDb.CustomerSegmentMasters.AddRangeAsync(BuildSegments(), cancellationToken);
        await commonDb.CustomerSegmentAssignments.AddRangeAsync(customers.Select((c, i) => new CustomerSegmentAssignment
        {
            AssignmentId = $"CSA-{i + 1:D6}",
            UnifiedCustomerId = c.UnifiedCustomerId,
            SegmentId = i % 3 == 0 ? "SEG-FX" : i % 3 == 1 ? "SEG-LOYALTY" : "SEG-RATE",
            AssignedAt = BaseNow.AddDays(-20 + i % 20),
            ExpiresAt = null
        }), cancellationToken);
        await commonDb.DomainIdMappings.AddRangeAsync(BuildMappings(customers), cancellationToken);
        await commonDb.CustomerIntegrationEvents.AddRangeAsync(customers.Select((c, i) => new CustomerIntegrationEvent
        {
            EventId = $"CIE-{i + 1:D6}",
            UnifiedCustomerId = c.UnifiedCustomerId,
            EventType = "登録",
            Domain = "common",
            EventDetail = "デモデータ生成",
            OccurredAt = c.RegisteredAt
        }), cancellationToken);

        SeedMobile(request, customers, rnd);
        SeedEcommerce(request, customers, rnd);
        SeedFintech(request, customers, rnd);

        await commonDb.SaveChangesAsync(cancellationToken);
        await mobileDb.SaveChangesAsync(cancellationToken);
        await ecommerceDb.SaveChangesAsync(cancellationToken);
        await fintechDb.SaveChangesAsync(cancellationToken);

        return new GenerationSummary(
            request.Scenario,
            await commonDb.UnifiedCustomers.CountAsync(cancellationToken),
            await mobileDb.Contracts.CountAsync(cancellationToken),
            await ecommerceDb.Orders.CountAsync(cancellationToken),
            await fintechDb.Accounts.CountAsync(cancellationToken));
    }

    private static async Task CreateTablesAsync(DbContext context, CancellationToken cancellationToken)
    {
        var creator = context.Database.GetService<IRelationalDatabaseCreator>();
        try
        {
            await creator.CreateTablesAsync(cancellationToken);
        }
        catch (Exception ex) when (ex.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase) || ex.Message.Contains("There is already", StringComparison.OrdinalIgnoreCase))
        {
        }
    }

    private static async Task ClearContextAsync(DbContext context, CancellationToken cancellationToken)
    {
        var tables = context.Model.GetEntityTypes()
            .Select(e => e.GetTableName())
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var table in tables)
        {
            var sql = $"DELETE FROM [{EscapeIdentifier(table!)}];";
            await context.Database.ExecuteSqlRawAsync(sql, cancellationToken);
        }
    }

    private static string EscapeIdentifier(string value) => value.Replace("]", "]]", StringComparison.Ordinal);

    private static IEnumerable<UnifiedCustomer> BuildCommonCustomers(int customerCount, Random rnd)
    {
        for (var i = 1; i <= customerCount; i++)
        {
            var ageBand = AgeBands[i % AgeBands.Length];
            yield return new UnifiedCustomer
            {
                UnifiedCustomerId = $"UC-{i:D6}",
                FullName = $"Demo Customer {i:D6}",
                BirthDate = new DateOnly(1965 + i % 40, 1 + i % 12, 1 + i % 27),
                Gender = i % 3 == 0 ? "Other" : i % 2 == 0 ? "F" : "M",
                Region = Regions[i % Regions.Length],
                AgeBand = ageBand,
                PrimaryEmail = $"customer{i:D6}@example.invalid",
                PrimaryPhone = $"090-0000-{i % 10000:D4}",
                KycStatus = i % 10 == 0 ? "pending" : i % 37 == 0 ? "rejected" : "verified",
                RegisteredAt = BaseNow.AddDays(-180 + rnd.Next(0, 150)),
                LastUpdatedAt = BaseNow.AddDays(-rnd.Next(0, 30))
            };
        }
    }

    private static IEnumerable<CustomerSegmentMaster> BuildSegments() =>
    [
        new() { SegmentId = "SEG-FX", SegmentName = "為替感応", DefinitionRule = "overseas_or_import=true", TargetDomains = "mobile,ecommerce,fintech", ValidFrom = BaseNow.AddMonths(-6), ValidTo = BaseNow.AddMonths(6) },
        new() { SegmentId = "SEG-LOYALTY", SegmentName = "競合影響", DefinitionRule = "campaign_response=true", TargetDomains = "mobile,ecommerce", ValidFrom = BaseNow.AddMonths(-6), ValidTo = BaseNow.AddMonths(6) },
        new() { SegmentId = "SEG-RATE", SegmentName = "金利感応", DefinitionRule = "loan_or_installment=true", TargetDomains = "mobile,fintech", ValidFrom = BaseNow.AddMonths(-6), ValidTo = BaseNow.AddMonths(6) }
    ];

    private static IEnumerable<DomainIdMapping> BuildMappings(IReadOnlyList<UnifiedCustomer> customers)
    {
        foreach (var (c, index) in customers.Select((c, i) => (c, i + 1)))
        {
            yield return Mapping(index, c.UnifiedCustomerId, "mobile", $"MOB-{index:D6}", "mobile-customer-mgmt");
            if (index <= customers.Count * 2 / 3)
                yield return Mapping(index, c.UnifiedCustomerId, "ecommerce", $"ECM-{index:D6}", "ec-member-point");
            if (index <= customers.Count / 2)
                yield return Mapping(index, c.UnifiedCustomerId, "fintech", $"FIN-{index:D6}", "fintech-account-mgmt");
        }

        static DomainIdMapping Mapping(int index, string unifiedId, string domain, string domainId, string source) => new()
        {
            MapId = $"MAP-{domain.ToUpperInvariant()}-{index:D6}",
            UnifiedCustomerId = unifiedId,
            Domain = domain,
            DomainCustomerId = domainId,
            SourceSystem = source,
            LinkedAt = BaseNow.AddDays(-90 + index % 60),
            LinkStatus = "有効"
        };
    }

    private void SeedMobile(DemoDataRequest request, IReadOnlyList<UnifiedCustomer> customers, Random rnd)
    {
        var scenario = request.Scenario;
        mobileDb.PlanRules.AddRange(
            new MobilePlanRule { PlanRuleId = "MPR-001", PlanId = "PLAN-LITE", BaseFee = 2980, OverageRule = "1GBごと550円", DeviceDiscountRule = "標準", ValidFrom = BaseNow.AddMonths(-6), ValidTo = BaseNow.AddMonths(6) },
            new MobilePlanRule { PlanRuleId = "MPR-002", PlanId = "PLAN-UNLIMITED", BaseFee = 6980, OverageRule = "なし", DeviceDiscountRule = scenario == ScenarioKind.Competitor ? "競合対抗増額" : "標準", ValidFrom = BaseNow.AddMonths(-6), ValidTo = BaseNow.AddMonths(6) });
        mobileDb.DeviceSkus.AddRange(Enumerable.Range(1, 8).Select(i => new MobileDeviceSku
        {
            DeviceSkuId = $"DSKU-{i:D3}", DeviceName = $"Demo Phone {i}", SupplierRegion = i % 2 == 0 ? "US" : "CN", ImportCurrency = i % 2 == 0 ? "USD" : "CNY", StandardCost = 40000 + i * 5000, SalesPrice = 70000 + i * 7000
        }));
        mobileDb.Options.AddRange(Enumerable.Range(1, 4).Select(i => new MobileOption { OptionId = $"OPT-{i:D3}", OptionName = $"Option {i}", OptionFee = 300 + i * 200, OptionCategory = i % 2 == 0 ? "エンタメ" : "保険", ValidFrom = BaseNow.AddMonths(-6), ValidTo = BaseNow.AddMonths(6) }));
        mobileDb.NetworkBaseStations.AddRange(Enumerable.Range(1, Math.Max(6, request.CustomerCount / 25)).Select(i => new NetworkBaseStation
        {
            StationId = $"BST-{i:D4}", Region = Regions[i % Regions.Length], EquipmentType = i % 2 == 0 ? "5G" : "4G", VendorName = $"Vendor {i % 4}", VendorCountry = i % 2 == 0 ? "US" : "JP", ContractCurrency = i % 2 == 0 ? "USD" : "JPY", InstallDate = DateOnly.FromDateTime(BaseNow.AddDays(-300 + i)), MaintenanceCost = 120000 + i * 1000
        }));

        foreach (var (c, i) in customers.Select((c, i) => (c, i + 1)))
        {
            var customerId = $"MOB-{i:D6}";
            var planId = i % 3 == 0 ? "PLAN-UNLIMITED" : "PLAN-LITE";
            var contractId = $"CON-{i:D6}";
            mobileDb.Customers.Add(new MobileCustomer { CustomerId = customerId, CustomerType = i % 12 == 0 ? "法人" : "個人", Region = c.Region, AgeBand = c.AgeBand, Segment = i % 3 == 0 ? "高利用" : "標準" });
            mobileDb.Contracts.Add(new MobileContract { ContractId = contractId, CustomerId = customerId, PlanId = planId, DeviceType = i % 2 == 0 ? "iOS" : "Android", UpdateMonth = "2026-06", SubsidyAmount = scenario == ScenarioKind.Competitor ? 18000 : 10000 });
            mobileDb.UsageBillings.Add(new MobileUsageBilling { UsageId = $"USE-{i:D6}", ContractId = contractId, UsageDate = DateOnly.FromDateTime(BaseNow.AddDays(-(i % 28))), VoiceUsage = rnd.Next(10, 300), DataUsage = rnd.Next(3, 60), MonthlyCharge = planId == "PLAN-UNLIMITED" ? 6980 : 2980 + rnd.Next(0, 2500) });
            mobileDb.Installments.Add(new MobileInstallment { InstallmentId = $"INS-{i:D6}", ContractId = contractId, DeviceSkuId = $"DSKU-{1 + i % 8:D3}", TotalAmount = 96000, MonthlyPayment = scenario == ScenarioKind.BojRateHike ? 4300 : 4000, RemainingMonths = 1 + i % 24, InterestRate = scenario == ScenarioKind.BojRateHike ? 0.045m : 0.025m, StartDate = DateOnly.FromDateTime(BaseNow.AddMonths(-i % 12)) });
            mobileDb.CustomerOptions.Add(new MobileCustomerOption { CustomerOptionId = $"MCO-{i:D6}", CustomerId = customerId, OptionId = $"OPT-{1 + i % 4:D3}", SubscribedAt = BaseNow.AddDays(-i % 90), CancelledAt = i % 19 == 0 ? BaseNow.AddDays(-i % 10) : null });
            if (i % (scenario == ScenarioKind.Competitor ? 3 : 8) == 0)
                mobileDb.MnpHistory.Add(new MnpHistory { MnpId = $"MNP-{i:D6}", CustomerId = customerId, MnpType = i % 2 == 0 ? "転出" : "転入", FromCarrier = i % 2 == 0 ? "自社" : "競合A", ToCarrier = i % 2 == 0 ? "競合A" : "自社", ExecutedAt = BaseNow.AddDays(-i % 30), TriggerReason = scenario == ScenarioKind.Competitor ? "競合キャンペーン" : "料金" });
            if (i % 5 == 0)
                mobileDb.Tickets.Add(new MobileTicket { TicketId = $"TCK-{i:D6}", CustomerId = customerId, ContactReason = scenario == ScenarioKind.Competitor ? "料金相談" : "契約確認", CreatedAt = BaseNow.AddDays(-i % 20), ResolvedAt = BaseNow.AddDays(-i % 20).AddHours(4), CancelFlag = scenario == ScenarioKind.Competitor && i % 10 == 0 });
            if (i % 4 == 0)
                mobileDb.CampaignActions.Add(new MobileCampaignAction { ActionId = $"ACT-{i:D6}", CustomerId = customerId, ActionType = scenario == ScenarioKind.Competitor ? "割引" : "通知", SentAt = BaseNow.AddDays(-i % 15), ResponseType = i % 8 == 0 ? "申込" : "開封", ResponseAt = BaseNow.AddDays(-i % 15).AddHours(2) });
        }

        mobileDb.CostItems.AddRange(Enumerable.Range(1, Math.Max(12, request.CustomerCount / 10)).Select(i => new MobileCostItem { CostItemId = $"CST-{i:D5}", CostType = i % 3 == 0 ? "基地局" : "端末", Currency = scenario == ScenarioKind.Fx && i % 2 == 0 ? "USD" : "JPY", UnitCost = scenario == ScenarioKind.Fx && i % 2 == 0 ? 650 : 85000 + i * 100, ProcurementDate = DateOnly.FromDateTime(BaseNow.AddDays(-i % 30)), VendorRegion = i % 2 == 0 ? "US" : "JP" }));
    }

    private void SeedEcommerce(DemoDataRequest request, IReadOnlyList<UnifiedCustomer> customers, Random rnd)
    {
        var memberCount = request.CustomerCount * 2 / 3;
        ecommerceDb.Categories.AddRange(Enumerable.Range(1, 6).Select(i => new Category { CategoryId = $"CAT-{i:D3}", CategoryName = $"Category {i}", ParentCategoryId = null, ImportRatio = i % 2 == 0 ? 0.7m : 0.2m, DemandElasticity = 0.8m + i / 10m }));
        ecommerceDb.Sellers.AddRange(Enumerable.Range(1, 6).Select(i => new Seller { SellerId = $"SEL-{i:D3}", SellerName = $"Seller {i}", SellerCountry = i % 2 == 0 ? "US" : "JP", SettlementCurrency = i % 2 == 0 ? "USD" : "JPY", CrossBorderFlag = i % 2 == 0, ContractStartDate = DateOnly.FromDateTime(BaseNow.AddMonths(-12)) }));
        ecommerceDb.Products.AddRange(Enumerable.Range(1, 20).Select(i => new Product { Sku = $"SKU-{i:D4}", ProductName = $"Demo Product {i}", Category = $"Category {1 + i % 6}", ProcurementCurrency = request.Scenario == ScenarioKind.Fx && i % 2 == 0 ? "USD" : "JPY", CostPrice = 1000 + i * 250, SellingPrice = 1800 + i * 350 }));
        ecommerceDb.PriceRules.AddRange(Enumerable.Range(1, 20).Select(i => new PriceRule { PriceRuleId = $"PRC-{i:D4}", Sku = $"SKU-{i:D4}", MinPrice = 1200 + i * 250, MaxPrice = 2500 + i * 450, MarkdownRule = request.Scenario == ScenarioKind.Competitor ? "ポイント対抗" : "通常", ValidFrom = BaseNow.AddMonths(-1), ValidTo = BaseNow.AddMonths(2) }));
        ecommerceDb.ShippingRoutes.AddRange(Enumerable.Range(1, 5).Select(i => new ShippingRoute { ShippingRouteId = $"SHP-{i:D3}", WarehouseId = $"WHS-{i:D2}", DestinationRegion = Regions[i % Regions.Length], LeadTimeDays = 1 + i % 4, ShippingCost = 500 + i * 100 }));
        ecommerceDb.Inventories.AddRange(Enumerable.Range(1, 20).Select(i => new Inventory { InventoryId = $"INV-{i:D4}", Sku = $"SKU-{i:D4}", WarehouseId = $"WHS-{1 + i % 5:D2}", StockQty = 30 + rnd.Next(0, 200), ArrivalDate = DateOnly.FromDateTime(BaseNow.AddDays(-i % 20)), ImportCurrency = i % 2 == 0 ? "USD" : "JPY" }));

        for (var i = 1; i <= memberCount; i++)
        {
            var c = customers[i - 1];
            var memberId = $"ECM-{i:D6}";
            ecommerceDb.Members.Add(new Member { MemberId = memberId, MemberName = c.FullName, Email = c.PrimaryEmail, Rank = i % 10 == 0 ? "Gold" : "Regular", RegisteredAt = DateOnly.FromDateTime(c.RegisteredAt), Region = c.Region, AgeBand = c.AgeBand });
            var sku = $"SKU-{1 + i % 20:D4}";
            var orderId = $"ORD-{i:D6}";
            var quantity = 1 + i % 3;
            ecommerceDb.Orders.Add(new Order { OrderId = orderId, MemberId = memberId, Sku = sku, OrderDate = DateOnly.FromDateTime(BaseNow.AddDays(-i % 30)), Quantity = quantity, OrderAmount = quantity * (1800 + (1 + i % 20) * 350) });
            ecommerceDb.PointEvents.Add(new PointEvent { PointEventId = $"PNT-{i:D6}", MemberId = memberId, EventType = "付与", PointAmount = request.Scenario == ScenarioKind.Competitor ? 200 : 80, BalanceAfter = 1000 + i, RelatedOrderId = orderId, EventAt = BaseNow.AddDays(-i % 30), ExpiryAt = BaseNow.AddMonths(12) });
            ecommerceDb.MemberBehaviors.Add(new MemberBehavior { EventId = $"BEH-{i:D6}", MemberId = memberId, EventType = i % 5 == 0 ? "離脱" : "購入", EventTime = BaseNow.AddDays(-i % 30).AddHours(i % 24), DeviceType = i % 2 == 0 ? "mobile" : "desktop" });
            ecommerceDb.PointCampaigns.Add(new PointCampaign { CampaignId = $"CMP-{i:D6}", MemberId = memberId, PointRate = request.Scenario == ScenarioKind.Competitor ? 0.1m : 0.03m, CampaignBudget = 10000 + i * 10, CampaignStartDate = DateOnly.FromDateTime(BaseNow.AddDays(-5)), CampaignEndDate = DateOnly.FromDateTime(BaseNow.AddDays(25)) });
            ecommerceDb.CampaignReactions.Add(new CampaignReaction { ReactionId = $"REA-{i:D6}", CampaignId = $"CMP-{i:D6}", MemberId = memberId, ReactionType = i % 4 == 0 ? "購入" : "クリック", ReactionTime = BaseNow.AddDays(-i % 10) });
            if (i % 13 == 0)
                ecommerceDb.ReturnCancellations.Add(new ReturnCancellation { ReturnId = $"RET-{i:D6}", OrderId = orderId, MemberId = memberId, Sku = sku, ReturnReason = request.Scenario == ScenarioKind.BojRateHike ? "買い控え" : "サイズ違い", ReturnAmount = 1200, ReturnedAt = BaseNow.AddDays(-i % 9) });
        }
    }

    private void SeedFintech(DemoDataRequest request, IReadOnlyList<UnifiedCustomer> customers, Random rnd)
    {
        var userCount = request.CustomerCount / 2;
        fintechDb.Merchants.AddRange(Enumerable.Range(1, 10).Select(i => new Merchant { MerchantId = $"MER-{i:D3}", MerchantName = $"Merchant {i}", MerchantCategory = $"MCC-{5000 + i}", SettlementCurrency = i % 2 == 0 ? "USD" : "JPY", FeeRate = 0.02m + i / 1000m, OverseasFlag = i % 2 == 0, ContractedAt = DateOnly.FromDateTime(BaseNow.AddMonths(-10)) }));
        fintechDb.ProductRules.AddRange(Enumerable.Range(1, 4).Select(i => new ProductRule { ProductRuleId = $"FPR-{i:D3}", ProductType = i % 2 == 0 ? "FX" : "ローン", InterestRate = request.Scenario == ScenarioKind.BojRateHike ? 0.045m : 0.018m, FeeRate = 0.01m, LeverageLimit = 25, ValidFrom = BaseNow.AddMonths(-6), ValidTo = BaseNow.AddMonths(6) }));
        fintechDb.FxRateSnapshots.AddRange(Enumerable.Range(1, 8).Select(i => new FxRateSnapshot { RateSnapshotId = $"FXR-{i:D3}", BaseCurrency = "USD", QuoteCurrency = "JPY", MidRate = request.Scenario == ScenarioKind.Fx ? 160 + i / 10m : 145 + i / 10m, BidRate = request.Scenario == ScenarioKind.Fx ? 159.8m : 144.8m, AskRate = request.Scenario == ScenarioKind.Fx ? 160.2m : 145.2m, CapturedAt = BaseNow.AddDays(-i), Source = "template" }));
        fintechDb.RevenueRisks.AddRange(Enumerable.Range(1, 6).Select(i => new RevenueRisk { RevenueId = $"REV-{i:D3}", BusinessLine = i % 2 == 0 ? "カード" : "FX", FeeRevenue = 100000 + i * 10000, InterestRevenue = request.Scenario == ScenarioKind.BojRateHike ? 60000 + i * 5000 : 30000 + i * 3000, FxRevenue = request.Scenario == ScenarioKind.Fx ? 90000 + i * 7000 : 40000, RiskLoss = i * 5000 }));

        for (var i = 1; i <= userCount; i++)
        {
            var userId = $"FIN-{i:D6}";
            fintechDb.Accounts.Add(new Account { UserId = userId, AccountId = $"ACC-{i:D6}", AccountType = i % 3 == 0 ? "投資" : "普通", OpenedAt = DateOnly.FromDateTime(BaseNow.AddMonths(-i % 24)), Balance = 50000 + rnd.Next(0, 1_000_000) });
            fintechDb.KycRecords.Add(new KycRecord { KycId = $"KYC-{i:D6}", UserId = userId, KycStatus = i % 10 == 0 ? "審査中" : "完了", IdType = "運転免許証", VerifiedAt = BaseNow.AddDays(-i % 100), ExpiryAt = BaseNow.AddYears(3), ReviewResult = i % 10 == 0 ? "保留" : "承認" });
            fintechDb.AccountLinks.Add(new AccountLink { LinkId = $"LNK-{i:D6}", UserId = userId, ExternalAccountRef = $"EXT-{i:D6}", LinkStatus = "有効", LinkedAt = BaseNow.AddDays(-i % 80) });
            fintechDb.CardTransactions.Add(new CardTransaction { CardId = $"CRD-{i:D6}", UserId = userId, TransactionId = $"TRN-{i:D6}", TransactionDate = BaseNow.AddDays(-i % 30), Amount = 1000 + rnd.Next(0, 50000), OverseasFlag = request.Scenario == ScenarioKind.Fx && i % 3 == 0 });
            fintechDb.FxPositions.Add(new FxPosition { PositionId = $"POS-{i:D6}", UserId = userId, ProductType = i % 3 == 0 ? "暗号資産" : "FX", PositionAmount = 10000 + i * 100, PnlAmount = request.Scenario == ScenarioKind.Fx ? -500 - i : 300 + i, MarketCurrency = i % 2 == 0 ? "USD" : "JPY" });
            fintechDb.CreditReviews.Add(new CreditReview { ReviewId = $"CRV-{i:D6}", UserId = userId, CreditScore = request.Scenario == ScenarioKind.BojRateHike ? 620 - i % 50 : 700 - i % 40, ApprovalStatus = i % 12 == 0 ? "保留" : "承認", ReviewReason = request.Scenario == ScenarioKind.BojRateHike ? "金利上昇影響" : "通常審査", ReviewedAt = BaseNow.AddDays(-i % 30) });
            fintechDb.LoanBalances.Add(new LoanBalance { LoanId = $"LOA-{i:D6}", UserId = userId, LoanType = i % 2 == 0 ? "住宅ローン" : "リボ払い", PrincipalBalance = 100000 + i * 1000, InterestRate = request.Scenario == ScenarioKind.BojRateHike ? 0.052m : 0.024m, MonthlyPayment = 12000 + i % 1000, MaturityDate = DateOnly.FromDateTime(BaseNow.AddYears(5)), OverdueFlag = request.Scenario == ScenarioKind.BojRateHike && i % 17 == 0 });
            if (i % 5 == 0)
                fintechDb.RiskEvents.Add(new RiskEvent { RiskEventId = $"RSK-{i:D6}", UserId = userId, RiskType = request.Scenario == ScenarioKind.Fx ? "市場" : "延滞", RiskScore = 0.3m + (i % 7) / 10m, DetectedAt = BaseNow.AddDays(-i % 20) });
            if (i % 7 == 0)
                fintechDb.TransactionAlerts.Add(new TransactionAlert { AlertId = $"ALT-{i:D6}", UserId = userId, AlertType = request.Scenario == ScenarioKind.BojRateHike ? "延滞" : "不正疑義", AlertLevel = i % 14 == 0 ? "高" : "中", TriggeredAt = BaseNow.AddDays(-i % 15), NotifiedAt = BaseNow.AddDays(-i % 15).AddMinutes(10), ResolvedAt = i % 14 == 0 ? null : BaseNow.AddDays(-i % 15).AddHours(2) });
        }
    }
}
