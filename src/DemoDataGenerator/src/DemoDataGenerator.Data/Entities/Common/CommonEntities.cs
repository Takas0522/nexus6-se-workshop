namespace DemoDataGenerator.Data.Entities.Common;

public sealed class UnifiedCustomer
{
    public string UnifiedCustomerId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public DateOnly BirthDate { get; set; }
    public string Gender { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string AgeBand { get; set; } = string.Empty;
    public string PrimaryEmail { get; set; } = string.Empty;
    public string PrimaryPhone { get; set; } = string.Empty;
    public string KycStatus { get; set; } = string.Empty;
    public DateTime RegisteredAt { get; set; }
    public DateTime LastUpdatedAt { get; set; }
}

public sealed class DomainIdMapping
{
    public string MapId { get; set; } = string.Empty;
    public string UnifiedCustomerId { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public string DomainCustomerId { get; set; } = string.Empty;
    public string SourceSystem { get; set; } = string.Empty;
    public DateTime LinkedAt { get; set; }
    public string LinkStatus { get; set; } = string.Empty;
}

public sealed class CustomerSegmentMaster
{
    public string SegmentId { get; set; } = string.Empty;
    public string SegmentName { get; set; } = string.Empty;
    public string DefinitionRule { get; set; } = string.Empty;
    public string TargetDomains { get; set; } = string.Empty;
    public DateTime ValidFrom { get; set; }
    public DateTime ValidTo { get; set; }
}

public sealed class CustomerSegmentAssignment
{
    public string AssignmentId { get; set; } = string.Empty;
    public string UnifiedCustomerId { get; set; } = string.Empty;
    public string SegmentId { get; set; } = string.Empty;
    public DateTime AssignedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
}

public sealed class CustomerIntegrationEvent
{
    public string EventId { get; set; } = string.Empty;
    public string UnifiedCustomerId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public string EventDetail { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
}
