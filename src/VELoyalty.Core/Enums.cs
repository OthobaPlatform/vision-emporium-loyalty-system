namespace VELoyalty.Core;

/// <summary>
/// Classification of a gift associated with a purchase threshold tier.
/// </summary>
public enum GiftType
{
    Cash_Return,
    Gift_Item
}

/// <summary>
/// Status of a verification code throughout its lifecycle.
/// </summary>
public enum CodeStatus
{
    Active,
    Redeemed,
    Expired
}

/// <summary>
/// Status of a data ingestion job (API sync or Excel import).
/// </summary>
public enum JobStatus
{
    InProgress,
    Success,
    Partial,
    Failed
}

/// <summary>
/// Roles supported by the RBAC service.
/// </summary>
public enum UserRole
{
    Admin,
    Outlet_Manager
}

/// <summary>
/// Types of events recorded in the audit trail.
/// </summary>
public enum AuditEventType
{
    Redemption,
    ConfigChange,
    IngestionJob,
    UserCreated,
    UserUpdated,
    OutletCreated,
    OutletUpdated
}
