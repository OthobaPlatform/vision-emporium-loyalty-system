namespace VELoyalty.Data;

/// <summary>
/// Builds composite keys (PK, SK, GSI keys) for all entity types in the VELoyalty single-table design.
/// </summary>
public static class KeyBuilder
{
    // ─── Customer ───────────────────────────────────────────────────────────────

    /// <summary>Customer PK: CUST#{customerId}</summary>
    public static string CustomerPk(string customerId) => $"CUST#{customerId}";

    /// <summary>Customer SK: PROFILE</summary>
    public static string CustomerSk() => "PROFILE";

    /// <summary>Customer GSI1PK: PHONE#{phone}</summary>
    public static string CustomerGsi1Pk(string phone) => $"PHONE#{phone}";

    /// <summary>Customer GSI1SK: CUST#{customerId}</summary>
    public static string CustomerGsi1Sk(string customerId) => $"CUST#{customerId}";

    // ─── Purchase ───────────────────────────────────────────────────────────────

    /// <summary>Purchase PK: CUST#{customerId}</summary>
    public static string PurchasePk(string customerId) => $"CUST#{customerId}";

    /// <summary>Purchase SK: PURCH#{challanNo}#{itemId} — unique per line item within a challan.</summary>
    public static string PurchaseSk(string challanNo, string? itemId = null) =>
        itemId != null ? $"PURCH#{challanNo}#{itemId}" : $"PURCH#{challanNo}";

    /// <summary>Purchase GSI1PK: OUTLET#{outletId}</summary>
    public static string PurchaseGsi1Pk(string outletId) => $"OUTLET#{outletId}";

    /// <summary>Purchase GSI1SK: PURCH#{date}</summary>
    public static string PurchaseGsi1Sk(DateOnly date) => $"PURCH#{date:yyyy-MM-dd}";

    /// <summary>Purchase GSI2PK: CHALLAN#{challanNo} — for grouping line items into one purchase.</summary>
    public static string PurchaseGsi2Pk(string challanNo) => $"CHALLAN#{challanNo}";

    /// <summary>Purchase GSI2SK: ITEM#{itemId}</summary>
    public static string PurchaseGsi2Sk(string itemId) => $"ITEM#{itemId}";

    // ─── Eligibility ────────────────────────────────────────────────────────────

    /// <summary>Eligibility PK: CUST#{customerId}</summary>
    public static string EligibilityPk(string customerId) => $"CUST#{customerId}";

    /// <summary>Eligibility SK: ELIG#{cycleId}#{tier}</summary>
    public static string EligibilitySk(string cycleId, int tier) => $"ELIG#{cycleId}#{tier}";

    /// <summary>Eligibility GSI1PK: OUTLET#{outletId}</summary>
    public static string EligibilityGsi1Pk(string outletId) => $"OUTLET#{outletId}";

    /// <summary>Eligibility GSI1SK: ELIG#{date}</summary>
    public static string EligibilityGsi1Sk(DateOnly date) => $"ELIG#{date:yyyy-MM-dd}";

    /// <summary>Eligibility GSI2PK: CODE#{code}</summary>
    public static string EligibilityGsi2Pk(string code) => $"CODE#{code}";

    /// <summary>Eligibility GSI2SK: ELIG#{customerId}</summary>
    public static string EligibilityGsi2Sk(string customerId) => $"ELIG#{customerId}";

    // ─── Redemption ─────────────────────────────────────────────────────────────

    /// <summary>Redemption PK: CUST#{customerId}</summary>
    public static string RedemptionPk(string customerId) => $"CUST#{customerId}";

    /// <summary>Redemption SK: REDM#{code}</summary>
    public static string RedemptionSk(string code) => $"REDM#{code}";

    /// <summary>Redemption GSI1PK: OUTLET#{outletId}</summary>
    public static string RedemptionGsi1Pk(string outletId) => $"OUTLET#{outletId}";

    /// <summary>Redemption GSI1SK: REDM#{date}</summary>
    public static string RedemptionGsi1Sk(DateTime redeemedAt) =>
        $"REDM#{redeemedAt:yyyy-MM-dd}";

    /// <summary>Redemption GSI2PK: CODE#{code}</summary>
    public static string RedemptionGsi2Pk(string code) => $"CODE#{code}";

    /// <summary>Redemption GSI2SK: REDM#{date}</summary>
    public static string RedemptionGsi2Sk(DateTime redeemedAt) =>
        $"REDM#{redeemedAt:yyyy-MM-dd}";

    // ─── Outlet ─────────────────────────────────────────────────────────────────

    /// <summary>Outlet PK: OUTLET#{outletId}</summary>
    public static string OutletPk(string outletId) => $"OUTLET#{outletId}";

    /// <summary>Outlet SK: META</summary>
    public static string OutletSk() => "META";

    /// <summary>Outlet GSI1PK: GSI1_OUTLET (fixed partition for listing all outlets)</summary>
    public static string OutletGsi1Pk() => "GSI1_OUTLET";

    /// <summary>Outlet GSI1SK: OUTLET#{outletId}</summary>
    public static string OutletGsi1Sk(string outletId) => $"OUTLET#{outletId}";

    // ─── Config ─────────────────────────────────────────────────────────────────

    /// <summary>Config PK: CONFIG</summary>
    public static string ConfigPk() => "CONFIG";

    /// <summary>Config SK: {configType}#{id}</summary>
    public static string ConfigSk(string configType, string id) => $"{configType}#{id}";

    // ─── Cycle ──────────────────────────────────────────────────────────────────

    /// <summary>Cycle PK: CONFIG</summary>
    public static string CyclePk() => "CONFIG";

    /// <summary>Cycle SK: CYCLE#{cycleId}</summary>
    public static string CycleSk(string cycleId) => $"CYCLE#{cycleId}";

    // ─── Threshold ──────────────────────────────────────────────────────────────

    /// <summary>Threshold PK: CONFIG</summary>
    public static string ThresholdPk() => "CONFIG";

    /// <summary>Threshold SK: THRESH#{tier}</summary>
    public static string ThresholdSk(int tier) => $"THRESH#{tier}";

    // ─── SyncJob ────────────────────────────────────────────────────────────────

    /// <summary>SyncJob PK: SYNC</summary>
    public static string SyncJobPk() => "SYNC";

    /// <summary>SyncJob SK: JOB#{timestamp}</summary>
    public static string SyncJobSk(DateTime timestamp) =>
        $"JOB#{timestamp:yyyy-MM-ddTHH:mm:ss.fffZ}";

    /// <summary>SyncJob GSI2PK: JOBID#{jobId}</summary>
    public static string SyncJobGsi2Pk(string jobId) => $"JOBID#{jobId}";

    /// <summary>SyncJob GSI2SK: SYNC#{status}</summary>
    public static string SyncJobGsi2Sk(string status) => $"SYNC#{status}";

    // ─── ImportJob ──────────────────────────────────────────────────────────────

    /// <summary>ImportJob PK: IMPORT</summary>
    public static string ImportJobPk() => "IMPORT";

    /// <summary>ImportJob SK: JOB#{timestamp}</summary>
    public static string ImportJobSk(DateTime timestamp) =>
        $"JOB#{timestamp:yyyy-MM-ddTHH:mm:ss.fffZ}";

    /// <summary>ImportJob GSI2PK: JOBID#{jobId}</summary>
    public static string ImportJobGsi2Pk(string jobId) => $"JOBID#{jobId}";

    /// <summary>ImportJob GSI2SK: IMPORT#{status}</summary>
    public static string ImportJobGsi2Sk(string status) => $"IMPORT#{status}";

    // ─── SMS Config ────────────────────────────────────────────────────────────

    /// <summary>SMS Config PK: CONFIG</summary>
    public static string SmsConfigPk() => "CONFIG";

    /// <summary>SMS Config SK: SETTINGS#SMS</summary>
    public static string SmsConfigSk() => "SETTINGS#SMS";

    // ─── Brand Config ─────────────────────────────────────────────────────────

    /// <summary>Brand Config PK: CONFIG</summary>
    public static string BrandConfigPk() => "CONFIG";

    /// <summary>Brand Config SK: SETTINGS#BRAND</summary>
    public static string BrandConfigSk() => "SETTINGS#BRAND";

    // ─── Notification ───────────────────────────────────────────────────────────

    /// <summary>Notification PK: NOTIF#{customerId}</summary>
    public static string NotificationPk(string customerId) => $"NOTIF#{customerId}";

    /// <summary>Notification SK: {timestamp}#{type}</summary>
    public static string NotificationSk(DateTime timestamp, string type) =>
        $"{timestamp:yyyy-MM-ddTHH:mm:ss.fffZ}#{type}";

    // ─── Audit ──────────────────────────────────────────────────────────────────

    /// <summary>Audit PK: AUDIT</summary>
    public static string AuditPk() => "AUDIT";

    /// <summary>Audit SK: {timestamp}#{eventType}</summary>
    public static string AuditSk(DateTime timestamp, string eventType) =>
        $"{timestamp:yyyy-MM-ddTHH:mm:ss.fffZ}#{eventType}";

    // ─── User ───────────────────────────────────────────────────────────────────

    /// <summary>User PK: USER#{userId}</summary>
    public static string UserPk(string userId) => $"USER#{userId}";

    /// <summary>User SK: META</summary>
    public static string UserSk() => "META";

    /// <summary>User GSI1PK: GSI1_USER (fixed partition for listing all users)</summary>
    public static string UserGsi1Pk() => "GSI1_USER";

    /// <summary>User GSI1SK: USER#{email}</summary>
    public static string UserGsi1Sk(string email) => $"USER#{email}";

    // ─── RateLimit ──────────────────────────────────────────────────────────────

    /// <summary>RateLimit PK: RATELIMIT#{code}</summary>
    public static string RateLimitPk(string code) => $"RATELIMIT#{code}";

    /// <summary>RateLimit SK: WINDOW#{windowStart}</summary>
    public static string RateLimitSk(DateTime windowStart) =>
        $"WINDOW#{windowStart:yyyy-MM-ddTHH:mm:ssZ}";
}
