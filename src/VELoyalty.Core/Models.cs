namespace VELoyalty.Core;

/// <summary>
/// Represents a customer in the loyalty system.
/// </summary>
/// <param name="CustomerId">Unique customer identifier.</param>
/// <param name="Name">Customer display name.</param>
/// <param name="PhoneNumber">Phone number in E.164 format.</param>
/// <param name="QualifyingPurchases">Number of qualifying purchases in the current cycle.</param>
/// <param name="CurrentCycleId">Identifier of the current loyalty cycle.</param>
public record Customer(
    string CustomerId,
    string Name,
    string PhoneNumber,
    int QualifyingPurchases,
    string CurrentCycleId
);

/// <summary>
/// Represents a purchase transaction record (a single line item within a challan).
/// Multiple items with the same ChallanNo constitute one purchase toward the threshold.
/// </summary>
/// <param name="CustomerId">Identifier of the purchasing customer (phone number or staff ID).</param>
/// <param name="OutletId">Identifier of the outlet (DIST_ID) where the purchase was made.</param>
/// <param name="PurchaseDate">Date of the purchase.</param>
/// <param name="Amount">Line item net amount in BDT (2 decimal places). Stored in actual BDT, not thousands.</param>
/// <param name="ProductCategory">Item name/category of the purchased product.</param>
/// <param name="ProcessedAt">UTC timestamp when the record was processed by the system.</param>
/// <param name="ChallanNo">Challan number grouping line items into a single purchase transaction.</param>
/// <param name="ItemId">Product/item identifier.</param>
/// <param name="Quantity">Quantity purchased.</param>
public record Purchase(
    string CustomerId,
    string OutletId,
    DateOnly PurchaseDate,
    decimal Amount,
    string ProductCategory,
    DateTime ProcessedAt,
    string ChallanNo,
    string? ItemId = null,
    int Quantity = 1
);

/// <summary>
/// Represents a configurable loyalty cycle time period.
/// </summary>
/// <param name="CycleId">Unique cycle identifier.</param>
/// <param name="StartDate">Cycle start date (interpreted in Asia/Dhaka).</param>
/// <param name="EndDate">Cycle end date (interpreted in Asia/Dhaka).</param>
/// <param name="IsActive">Whether this cycle is currently active.</param>
public record LoyaltyCycle(
    string CycleId,
    DateOnly StartDate,
    DateOnly EndDate,
    bool IsActive
);

/// <summary>
/// Represents a configurable purchase threshold that triggers gift eligibility.
/// </summary>
/// <param name="Tier">Tier number for ordering.</param>
/// <param name="RequiredPurchases">Number of purchases required to reach this threshold.</param>
/// <param name="GiftType">Type of gift: Cash_Return or Gift_Item.</param>
/// <param name="GiftDescription">Description of the gift (max 200 characters).</param>
/// <param name="GiftValue">Monetary value of the gift in BDT (for Cash_Return) or 0 (for Gift_Item).</param>
/// <param name="GiftValueType">How the gift value is calculated: "fixed" (BDT amount) or "percentage" (of purchase). Only applies to Cash_Return.</param>
/// <param name="IsEnabled">Whether this threshold tier is currently enabled.</param>
/// <param name="MinPurchaseAmount">Minimum purchase amount that qualifies toward this threshold.</param>
/// <param name="ExcludedCategories">Product categories excluded from counting toward this threshold.</param>
public record PurchaseThreshold(
    int Tier,
    int RequiredPurchases,
    string GiftType,
    string GiftDescription,
    decimal GiftValue,
    string GiftValueType,
    bool IsEnabled,
    decimal MinPurchaseAmount,
    List<string> ExcludedCategories
);

/// <summary>
/// Represents a verification code issued to an eligible customer for gift redemption.
/// </summary>
/// <param name="Code">6-digit numeric verification code.</param>
/// <param name="CustomerId">Identifier of the eligible customer.</param>
/// <param name="OutletId">Identifier of the designated redemption outlet.</param>
/// <param name="Tier">Threshold tier this code corresponds to.</param>
/// <param name="GiftType">Type of gift: Cash_Return or Gift_Item.</param>
/// <param name="GiftDescription">Description of the gift.</param>
/// <param name="GiftValue">Monetary value of the gift in BDT.</param>
/// <param name="IssuedAt">UTC timestamp when the code was issued.</param>
/// <param name="ExpiresAt">UTC timestamp when the code expires.</param>
/// <param name="Status">Current status: Active, Redeemed, or Expired.</param>
public record VerificationCode(
    string Code,
    string CustomerId,
    string OutletId,
    int Tier,
    string GiftType,
    string GiftDescription,
    decimal GiftValue,
    DateTime IssuedAt,
    DateTime ExpiresAt,
    string Status
);

/// <summary>
/// Represents a completed gift redemption event.
/// </summary>
/// <param name="Code">The verification code that was redeemed.</param>
/// <param name="CustomerId">Identifier of the customer who redeemed.</param>
/// <param name="OutletId">Identifier of the outlet where redemption occurred.</param>
/// <param name="StaffMemberId">Identifier of the staff member who processed the redemption.</param>
/// <param name="GiftType">Type of gift dispensed.</param>
/// <param name="RedeemedAt">UTC timestamp of the redemption.</param>
public record Redemption(
    string Code,
    string CustomerId,
    string OutletId,
    string StaffMemberId,
    string GiftType,
    DateTime RedeemedAt
);

/// <summary>
/// Represents a physical Vision Emporium retail outlet.
/// </summary>
/// <param name="OutletId">Unique outlet identifier.</param>
/// <param name="Name">Outlet display name.</param>
/// <param name="Address">Physical address of the outlet.</param>
/// <param name="PhoneNumber">Contact phone number for the outlet.</param>
/// <param name="AssignedManagerId">User ID of the assigned outlet manager.</param>
/// <param name="IsActive">Whether the outlet is currently active.</param>
public record Outlet(
    string OutletId,
    string Name,
    string Address,
    string PhoneNumber,
    string AssignedManagerId,
    bool IsActive
);

/// <summary>
/// Represents the result of an API sync job.
/// </summary>
/// <param name="JobId">Unique job identifier.</param>
/// <param name="Status">Job status: Success, Partial, Failed, or InProgress.</param>
/// <param name="RecordsFetched">Total records fetched from the external API.</param>
/// <param name="RecordsStored">Records successfully stored in DynamoDB.</param>
/// <param name="RecordsSkipped">Records skipped as duplicates.</param>
/// <param name="RecordsRejected">Records rejected due to validation failures.</param>
/// <param name="StartedAt">UTC timestamp when the job started.</param>
/// <param name="CompletedAt">UTC timestamp when the job completed.</param>
public record SyncJobResult(
    string JobId,
    string Status,
    int RecordsFetched,
    int RecordsStored,
    int RecordsSkipped,
    int RecordsRejected,
    DateTime StartedAt,
    DateTime CompletedAt
);

/// <summary>
/// Represents an audit trail entry for system actions.
/// </summary>
/// <param name="EventType">Type of auditable event.</param>
/// <param name="ActorId">Identifier of the user or system component that performed the action.</param>
/// <param name="EntityType">Type of entity affected (e.g., Customer, Outlet, Config).</param>
/// <param name="EntityId">Identifier of the affected entity.</param>
/// <param name="Details">Additional key-value details about the event.</param>
/// <param name="Timestamp">UTC timestamp of the event.</param>
public record AuditEntry(
    string EventType,
    string ActorId,
    string EntityType,
    string EntityId,
    Dictionary<string, string> Details,
    DateTime Timestamp
);

/// <summary>
/// Represents the result of an Excel file import job.
/// </summary>
/// <param name="JobId">Unique job identifier.</param>
/// <param name="Status">Job status: Success, Partial, Failed, or InProgress.</param>
/// <param name="FileName">Original name of the uploaded file.</param>
/// <param name="TotalRows">Total rows found in the file.</param>
/// <param name="RecordsImported">Records successfully imported.</param>
/// <param name="RecordsRejected">Records rejected due to validation failures.</param>
/// <param name="RecordsSkipped">Records skipped as duplicates.</param>
/// <param name="RejectedRows">Details of rejected rows with row number and reason.</param>
/// <param name="StartedAt">UTC timestamp when the job started.</param>
/// <param name="CompletedAt">UTC timestamp when the job completed.</param>
public record ImportJobResult(
    string JobId,
    string Status,
    string FileName,
    int TotalRows,
    int RecordsImported,
    int RecordsRejected,
    int RecordsSkipped,
    List<RejectedRow> RejectedRows,
    DateTime StartedAt,
    DateTime CompletedAt
);

/// <summary>
/// Represents a rejected row from an Excel import with the reason for rejection.
/// </summary>
/// <param name="RowNumber">1-based row number in the Excel file.</param>
/// <param name="Reason">Specific reason the row was rejected.</param>
public record RejectedRow(
    int RowNumber,
    string Reason
);

/// <summary>
/// Represents a notification log entry for SMS delivery tracking.
/// </summary>
/// <param name="NotificationId">Unique notification identifier.</param>
/// <param name="CustomerId">Identifier of the recipient customer.</param>
/// <param name="PhoneNumber">Recipient phone number in E.164 format.</param>
/// <param name="MessageType">Type of notification (e.g., Eligibility, Reminder).</param>
/// <param name="Content">SMS message content.</param>
/// <param name="DeliveryStatus">Delivery status: Sent, Delivered, or Failed.</param>
/// <param name="FailureReason">Reason for delivery failure, if applicable.</param>
/// <param name="AttemptCount">Number of delivery attempts made.</param>
/// <param name="SentAt">UTC timestamp when the notification was sent.</param>
public record NotificationLog(
    string NotificationId,
    string CustomerId,
    string PhoneNumber,
    string MessageType,
    string Content,
    string DeliveryStatus,
    string? FailureReason,
    int AttemptCount,
    DateTime SentAt
);

/// <summary>
/// Represents a user account in the system.
/// </summary>
/// <param name="UserId">Unique user identifier.</param>
/// <param name="Email">User email address (used for login).</param>
/// <param name="Name">User display name.</param>
/// <param name="PasswordHash">bcrypt hash of the user's password (cost factor 12).</param>
/// <param name="Role">User role: Admin or Outlet_Manager.</param>
/// <param name="OutletId">Assigned outlet identifier (Outlet_Manager only).</param>
/// <param name="IsActive">Whether the user account is active.</param>
/// <param name="CreatedAt">UTC timestamp of account creation.</param>
/// <param name="UpdatedAt">UTC timestamp of last modification.</param>
public record User(
    string UserId,
    string Email,
    string Name,
    string PasswordHash,
    string Role,
    string? OutletId,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

/// <summary>
/// Represents general system configuration settings.
/// </summary>
/// <param name="SyncIntervalMinutes">Interval in minutes between API sync jobs (minimum 15).</param>
/// <param name="CodeExpiryDays">Number of days before a verification code expires (7–90, default 30).</param>
/// <param name="MinPurchaseAmount">Minimum purchase amount in BDT that qualifies toward thresholds (0.01–999,999.99).</param>
/// <param name="ExcludedCategories">Product categories excluded from counting toward purchase thresholds.</param>
public record GeneralConfig(
    int SyncIntervalMinutes,
    int CodeExpiryDays,
    decimal MinPurchaseAmount,
    List<string> ExcludedCategories
);

/// <summary>
/// Represents SMS gateway configuration stored in DynamoDB.
/// </summary>
/// <param name="Enabled">Whether SMS sending is enabled.</param>
/// <param name="BaseUrl">Base URL of the SMS gateway API.</param>
/// <param name="ApiKey">API key for authenticating with the SMS gateway.</param>
/// <param name="SenderId">Sender ID displayed on the SMS.</param>
public record SmsConfig(
    bool Enabled,
    string BaseUrl,
    string ApiKey,
    string SenderId
);

/// <summary>
/// Represents a signed JWT authentication token.
/// </summary>
/// <param name="Token">Signed JWT string (HMAC-SHA256).</param>
/// <param name="ExpiresAt">UTC timestamp when the token expires.</param>
public record AuthToken(
    string Token,
    DateTime ExpiresAt
);

/// <summary>
/// Represents brand/theming configuration stored in DynamoDB.
/// </summary>
/// <param name="CompanyName">Company display name.</param>
/// <param name="PrimaryColor">Primary brand color (hex, e.g., "#E31E24").</param>
/// <param name="SecondaryColor">Secondary brand color (hex, e.g., "#1a1a1a").</param>
/// <param name="AccentColor">Accent/background color (hex, e.g., "#D6E4F0").</param>
/// <param name="LogoUrl">URL to the logo image (can be a data: URI or external URL).</param>
/// <param name="FaviconUrl">URL to the favicon.</param>
public record BrandConfig(
    string CompanyName,
    string PrimaryColor,
    string SecondaryColor,
    string AccentColor,
    string LogoUrl,
    string FaviconUrl
);
