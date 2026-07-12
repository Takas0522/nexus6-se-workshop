namespace DemoDataGenerator.Data.Entities.Fintech;

public sealed class Account
{
    public string UserId { get; set; } = string.Empty;
    public string AccountId { get; set; } = string.Empty;
    public string AccountType { get; set; } = string.Empty;
    public DateOnly OpenedAt { get; set; }
    public decimal Balance { get; set; }
}

public sealed class CardTransaction
{
    public string CardId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string TransactionId { get; set; } = string.Empty;
    public DateTime TransactionDate { get; set; }
    public decimal Amount { get; set; }
    public bool OverseasFlag { get; set; }
}

public sealed class FxPosition
{
    public string PositionId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string ProductType { get; set; } = string.Empty;
    public decimal PositionAmount { get; set; }
    public decimal PnlAmount { get; set; }
    public string MarketCurrency { get; set; } = string.Empty;
}

public sealed class CreditReview
{
    public string ReviewId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public decimal CreditScore { get; set; }
    public string ApprovalStatus { get; set; } = string.Empty;
    public string ReviewReason { get; set; } = string.Empty;
    public DateTime ReviewedAt { get; set; }
}

public sealed class RevenueRisk
{
    public string RevenueId { get; set; } = string.Empty;
    public string BusinessLine { get; set; } = string.Empty;
    public decimal FeeRevenue { get; set; }
    public decimal InterestRevenue { get; set; }
    public decimal FxRevenue { get; set; }
    public decimal RiskLoss { get; set; }
}

public sealed class ProductRule
{
    public string ProductRuleId { get; set; } = string.Empty;
    public string ProductType { get; set; } = string.Empty;
    public decimal InterestRate { get; set; }
    public decimal FeeRate { get; set; }
    public decimal LeverageLimit { get; set; }
    public DateTime ValidFrom { get; set; }
    public DateTime ValidTo { get; set; }
}

public sealed class RiskEvent
{
    public string RiskEventId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string RiskType { get; set; } = string.Empty;
    public decimal RiskScore { get; set; }
    public DateTime DetectedAt { get; set; }
}

public sealed class AccountLink
{
    public string LinkId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string ExternalAccountRef { get; set; } = string.Empty;
    public string LinkStatus { get; set; } = string.Empty;
    public DateTime LinkedAt { get; set; }
}

public sealed class LoanBalance
{
    public string LoanId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string LoanType { get; set; } = string.Empty;
    public decimal PrincipalBalance { get; set; }
    public decimal InterestRate { get; set; }
    public decimal MonthlyPayment { get; set; }
    public DateOnly MaturityDate { get; set; }
    public bool OverdueFlag { get; set; }
}

public sealed class Merchant
{
    public string MerchantId { get; set; } = string.Empty;
    public string MerchantName { get; set; } = string.Empty;
    public string MerchantCategory { get; set; } = string.Empty;
    public string SettlementCurrency { get; set; } = string.Empty;
    public decimal FeeRate { get; set; }
    public bool OverseasFlag { get; set; }
    public DateOnly ContractedAt { get; set; }
}

public sealed class FxRateSnapshot
{
    public string RateSnapshotId { get; set; } = string.Empty;
    public string BaseCurrency { get; set; } = string.Empty;
    public string QuoteCurrency { get; set; } = string.Empty;
    public decimal MidRate { get; set; }
    public decimal BidRate { get; set; }
    public decimal AskRate { get; set; }
    public DateTime CapturedAt { get; set; }
    public string Source { get; set; } = string.Empty;
}

public sealed class KycRecord
{
    public string KycId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string KycStatus { get; set; } = string.Empty;
    public string IdType { get; set; } = string.Empty;
    public DateTime VerifiedAt { get; set; }
    public DateTime ExpiryAt { get; set; }
    public string ReviewResult { get; set; } = string.Empty;
}

public sealed class TransactionAlert
{
    public string AlertId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string AlertType { get; set; } = string.Empty;
    public string AlertLevel { get; set; } = string.Empty;
    public DateTime TriggeredAt { get; set; }
    public DateTime NotifiedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
}
