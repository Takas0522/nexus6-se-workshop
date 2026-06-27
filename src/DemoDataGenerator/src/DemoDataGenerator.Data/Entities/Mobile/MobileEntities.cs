namespace DemoDataGenerator.Data.Entities.Mobile;

public sealed class MobileCustomer
{
    public string CustomerId { get; set; } = string.Empty;
    public string CustomerType { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string AgeBand { get; set; } = string.Empty;
    public string Segment { get; set; } = string.Empty;
}

public sealed class MobileContract
{
    public string ContractId { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public string PlanId { get; set; } = string.Empty;
    public string DeviceType { get; set; } = string.Empty;
    public string UpdateMonth { get; set; } = string.Empty;
    public decimal SubsidyAmount { get; set; }
}

public sealed class MobileUsageBilling
{
    public string UsageId { get; set; } = string.Empty;
    public string ContractId { get; set; } = string.Empty;
    public DateOnly UsageDate { get; set; }
    public decimal VoiceUsage { get; set; }
    public decimal DataUsage { get; set; }
    public decimal MonthlyCharge { get; set; }
}

public sealed class MobileCostItem
{
    public string CostItemId { get; set; } = string.Empty;
    public string CostType { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public decimal UnitCost { get; set; }
    public DateOnly ProcurementDate { get; set; }
    public string VendorRegion { get; set; } = string.Empty;
}

public sealed class MobileTicket
{
    public string TicketId { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public string ContactReason { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime ResolvedAt { get; set; }
    public bool CancelFlag { get; set; }
}

public sealed class MobilePlanRule
{
    public string PlanRuleId { get; set; } = string.Empty;
    public string PlanId { get; set; } = string.Empty;
    public decimal BaseFee { get; set; }
    public string OverageRule { get; set; } = string.Empty;
    public string DeviceDiscountRule { get; set; } = string.Empty;
    public DateTime ValidFrom { get; set; }
    public DateTime ValidTo { get; set; }
}

public sealed class MobileDeviceSku
{
    public string DeviceSkuId { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public string SupplierRegion { get; set; } = string.Empty;
    public string ImportCurrency { get; set; } = string.Empty;
    public decimal StandardCost { get; set; }
    public decimal SalesPrice { get; set; }
}

public sealed class MobileCampaignAction
{
    public string ActionId { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public string ActionType { get; set; } = string.Empty;
    public DateTime SentAt { get; set; }
    public string ResponseType { get; set; } = string.Empty;
    public DateTime ResponseAt { get; set; }
}

public sealed class NetworkBaseStation
{
    public string StationId { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string EquipmentType { get; set; } = string.Empty;
    public string VendorName { get; set; } = string.Empty;
    public string VendorCountry { get; set; } = string.Empty;
    public string ContractCurrency { get; set; } = string.Empty;
    public DateOnly InstallDate { get; set; }
    public decimal MaintenanceCost { get; set; }
}

public sealed class MnpHistory
{
    public string MnpId { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public string MnpType { get; set; } = string.Empty;
    public string FromCarrier { get; set; } = string.Empty;
    public string ToCarrier { get; set; } = string.Empty;
    public DateTime ExecutedAt { get; set; }
    public string TriggerReason { get; set; } = string.Empty;
}

public sealed class MobileInstallment
{
    public string InstallmentId { get; set; } = string.Empty;
    public string ContractId { get; set; } = string.Empty;
    public string DeviceSkuId { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public decimal MonthlyPayment { get; set; }
    public int RemainingMonths { get; set; }
    public decimal InterestRate { get; set; }
    public DateOnly StartDate { get; set; }
}

public sealed class MobileOption
{
    public string OptionId { get; set; } = string.Empty;
    public string OptionName { get; set; } = string.Empty;
    public decimal OptionFee { get; set; }
    public string OptionCategory { get; set; } = string.Empty;
    public DateTime ValidFrom { get; set; }
    public DateTime ValidTo { get; set; }
}

public sealed class MobileCustomerOption
{
    public string CustomerOptionId { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public string OptionId { get; set; } = string.Empty;
    public DateTime SubscribedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
}
