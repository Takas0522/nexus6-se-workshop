namespace DemoDataGenerator.Data.Entities.Ecommerce;

public sealed class Product
{
    public string Sku { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string ProcurementCurrency { get; set; } = string.Empty;
    public decimal CostPrice { get; set; }
    public decimal SellingPrice { get; set; }
}

public sealed class Order
{
    public string OrderId { get; set; } = string.Empty;
    public string MemberId { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public DateOnly OrderDate { get; set; }
    public int Quantity { get; set; }
    public decimal OrderAmount { get; set; }
}

public sealed class Inventory
{
    public string InventoryId { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string WarehouseId { get; set; } = string.Empty;
    public int StockQty { get; set; }
    public DateOnly ArrivalDate { get; set; }
    public string ImportCurrency { get; set; } = string.Empty;
}

public sealed class PointCampaign
{
    public string CampaignId { get; set; } = string.Empty;
    public string MemberId { get; set; } = string.Empty;
    public decimal PointRate { get; set; }
    public decimal CampaignBudget { get; set; }
    public DateOnly CampaignStartDate { get; set; }
    public DateOnly CampaignEndDate { get; set; }
}

public sealed class MemberBehavior
{
    public string EventId { get; set; } = string.Empty;
    public string MemberId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public DateTime EventTime { get; set; }
    public string DeviceType { get; set; } = string.Empty;
}

public sealed class PriceRule
{
    public string PriceRuleId { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public decimal MinPrice { get; set; }
    public decimal MaxPrice { get; set; }
    public string MarkdownRule { get; set; } = string.Empty;
    public DateTime ValidFrom { get; set; }
    public DateTime ValidTo { get; set; }
}

public sealed class ShippingRoute
{
    public string ShippingRouteId { get; set; } = string.Empty;
    public string WarehouseId { get; set; } = string.Empty;
    public string DestinationRegion { get; set; } = string.Empty;
    public int LeadTimeDays { get; set; }
    public decimal ShippingCost { get; set; }
}

public sealed class CampaignReaction
{
    public string ReactionId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string MemberId { get; set; } = string.Empty;
    public string ReactionType { get; set; } = string.Empty;
    public DateTime ReactionTime { get; set; }
}

public sealed class Member
{
    public string MemberId { get; set; } = string.Empty;
    public string MemberName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Rank { get; set; } = string.Empty;
    public DateOnly RegisteredAt { get; set; }
    public string Region { get; set; } = string.Empty;
    public string AgeBand { get; set; } = string.Empty;
}

public sealed class Seller
{
    public string SellerId { get; set; } = string.Empty;
    public string SellerName { get; set; } = string.Empty;
    public string SellerCountry { get; set; } = string.Empty;
    public string SettlementCurrency { get; set; } = string.Empty;
    public bool CrossBorderFlag { get; set; }
    public DateOnly ContractStartDate { get; set; }
}

public sealed class Category
{
    public string CategoryId { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string? ParentCategoryId { get; set; }
    public decimal ImportRatio { get; set; }
    public decimal DemandElasticity { get; set; }
}

public sealed class PointEvent
{
    public string PointEventId { get; set; } = string.Empty;
    public string MemberId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public int PointAmount { get; set; }
    public int BalanceAfter { get; set; }
    public string RelatedOrderId { get; set; } = string.Empty;
    public DateTime EventAt { get; set; }
    public DateTime ExpiryAt { get; set; }
}

public sealed class ReturnCancellation
{
    public string ReturnId { get; set; } = string.Empty;
    public string OrderId { get; set; } = string.Empty;
    public string MemberId { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string ReturnReason { get; set; } = string.Empty;
    public decimal ReturnAmount { get; set; }
    public DateTime ReturnedAt { get; set; }
}
