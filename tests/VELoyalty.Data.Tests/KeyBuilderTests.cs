using Xunit;

namespace VELoyalty.Data.Tests;

public class KeyBuilderTests
{
    // ─── Customer Keys ──────────────────────────────────────────────────────────

    [Fact]
    public void CustomerPk_ReturnsCorrectFormat()
    {
        Assert.Equal("CUST#C001", KeyBuilder.CustomerPk("C001"));
    }

    [Fact]
    public void CustomerSk_ReturnsProfile()
    {
        Assert.Equal("PROFILE", KeyBuilder.CustomerSk());
    }

    [Fact]
    public void CustomerGsi1Pk_ReturnsPhoneFormat()
    {
        Assert.Equal("PHONE#+8801712345678", KeyBuilder.CustomerGsi1Pk("+8801712345678"));
    }

    [Fact]
    public void CustomerGsi1Sk_ReturnsCustomerIdFormat()
    {
        Assert.Equal("CUST#C001", KeyBuilder.CustomerGsi1Sk("C001"));
    }

    // ─── Purchase Keys ──────────────────────────────────────────────────────────

    [Fact]
    public void PurchasePk_ReturnsCustomerFormat()
    {
        Assert.Equal("CUST#C001", KeyBuilder.PurchasePk("C001"));
    }

    [Fact]
    public void PurchaseSk_ReturnsCorrectCompositeFormat()
    {
        var date = new DateOnly(2024, 6, 15);
        var result = KeyBuilder.PurchaseSk(date, "OUT01", 1500.50m);
        Assert.Equal("PURCH#2024-06-15#OUT01#1500.50", result);
    }

    [Fact]
    public void PurchaseGsi1Pk_ReturnsOutletFormat()
    {
        Assert.Equal("OUTLET#OUT01", KeyBuilder.PurchaseGsi1Pk("OUT01"));
    }

    [Fact]
    public void PurchaseGsi1Sk_ReturnsDateFormat()
    {
        var date = new DateOnly(2024, 6, 15);
        Assert.Equal("PURCH#2024-06-15", KeyBuilder.PurchaseGsi1Sk(date));
    }

    // ─── Eligibility Keys ───────────────────────────────────────────────────────

    [Fact]
    public void EligibilitySk_ReturnsCorrectFormat()
    {
        Assert.Equal("ELIG#2024-2025#3", KeyBuilder.EligibilitySk("2024-2025", 3));
    }

    [Fact]
    public void EligibilityGsi2Pk_ReturnsCodeFormat()
    {
        Assert.Equal("CODE#123456", KeyBuilder.EligibilityGsi2Pk("123456"));
    }

    [Fact]
    public void EligibilityGsi2Sk_ReturnsCustomerFormat()
    {
        Assert.Equal("ELIG#C001", KeyBuilder.EligibilityGsi2Sk("C001"));
    }

    // ─── Redemption Keys ────────────────────────────────────────────────────────

    [Fact]
    public void RedemptionSk_ReturnsCodeFormat()
    {
        Assert.Equal("REDM#123456", KeyBuilder.RedemptionSk("123456"));
    }

    [Fact]
    public void RedemptionGsi2Pk_ReturnsCodeFormat()
    {
        Assert.Equal("CODE#123456", KeyBuilder.RedemptionGsi2Pk("123456"));
    }

    // ─── Outlet Keys ────────────────────────────────────────────────────────────

    [Fact]
    public void OutletPk_ReturnsCorrectFormat()
    {
        Assert.Equal("OUTLET#OUT01", KeyBuilder.OutletPk("OUT01"));
    }

    [Fact]
    public void OutletSk_ReturnsMeta()
    {
        Assert.Equal("META", KeyBuilder.OutletSk());
    }

    [Fact]
    public void OutletGsi1Pk_ReturnsFixedPartition()
    {
        Assert.Equal("GSI1_OUTLET", KeyBuilder.OutletGsi1Pk());
    }

    // ─── Config Keys ────────────────────────────────────────────────────────────

    [Fact]
    public void ConfigPk_ReturnsConfig()
    {
        Assert.Equal("CONFIG", KeyBuilder.ConfigPk());
    }

    [Fact]
    public void CycleSk_ReturnsCorrectFormat()
    {
        Assert.Equal("CYCLE#2024-2025", KeyBuilder.CycleSk("2024-2025"));
    }

    [Fact]
    public void ThresholdSk_ReturnsCorrectFormat()
    {
        Assert.Equal("THRESH#3", KeyBuilder.ThresholdSk(3));
    }

    // ─── SyncJob Keys ───────────────────────────────────────────────────────────

    [Fact]
    public void SyncJobPk_ReturnsSync()
    {
        Assert.Equal("SYNC", KeyBuilder.SyncJobPk());
    }

    [Fact]
    public void SyncJobGsi2Pk_ReturnsJobIdFormat()
    {
        Assert.Equal("JOBID#job-123", KeyBuilder.SyncJobGsi2Pk("job-123"));
    }

    [Fact]
    public void SyncJobGsi2Sk_ReturnsStatusFormat()
    {
        Assert.Equal("SYNC#Success", KeyBuilder.SyncJobGsi2Sk("Success"));
    }

    // ─── ImportJob Keys ─────────────────────────────────────────────────────────

    [Fact]
    public void ImportJobPk_ReturnsImport()
    {
        Assert.Equal("IMPORT", KeyBuilder.ImportJobPk());
    }

    [Fact]
    public void ImportJobGsi2Sk_ReturnsStatusFormat()
    {
        Assert.Equal("IMPORT#Failed", KeyBuilder.ImportJobGsi2Sk("Failed"));
    }

    // ─── User Keys ──────────────────────────────────────────────────────────────

    [Fact]
    public void UserPk_ReturnsCorrectFormat()
    {
        Assert.Equal("USER#U001", KeyBuilder.UserPk("U001"));
    }

    [Fact]
    public void UserSk_ReturnsMeta()
    {
        Assert.Equal("META", KeyBuilder.UserSk());
    }

    [Fact]
    public void UserGsi1Pk_ReturnsFixedPartition()
    {
        Assert.Equal("GSI1_USER", KeyBuilder.UserGsi1Pk());
    }

    [Fact]
    public void UserGsi1Sk_ReturnsEmailFormat()
    {
        Assert.Equal("USER#admin@ve.com", KeyBuilder.UserGsi1Sk("admin@ve.com"));
    }

    // ─── Audit Keys ─────────────────────────────────────────────────────────────

    [Fact]
    public void AuditPk_ReturnsAudit()
    {
        Assert.Equal("AUDIT", KeyBuilder.AuditPk());
    }

    [Fact]
    public void AuditSk_ReturnsTimestampAndEventType()
    {
        var timestamp = new DateTime(2024, 6, 15, 10, 30, 0, DateTimeKind.Utc);
        var result = KeyBuilder.AuditSk(timestamp, "Redemption");
        Assert.Equal("2024-06-15T10:30:00.000Z#Redemption", result);
    }

    // ─── RateLimit Keys ─────────────────────────────────────────────────────────

    [Fact]
    public void RateLimitPk_ReturnsCodeFormat()
    {
        Assert.Equal("RATELIMIT#123456", KeyBuilder.RateLimitPk("123456"));
    }

    [Fact]
    public void RateLimitSk_ReturnsWindowFormat()
    {
        var windowStart = new DateTime(2024, 6, 15, 10, 0, 0, DateTimeKind.Utc);
        var result = KeyBuilder.RateLimitSk(windowStart);
        Assert.Equal("WINDOW#2024-06-15T10:00:00Z", result);
    }

    // ─── Notification Keys ──────────────────────────────────────────────────────

    [Fact]
    public void NotificationPk_ReturnsCustomerFormat()
    {
        Assert.Equal("NOTIF#C001", KeyBuilder.NotificationPk("C001"));
    }

    [Fact]
    public void NotificationSk_ReturnsTimestampAndType()
    {
        var timestamp = new DateTime(2024, 6, 15, 10, 30, 0, DateTimeKind.Utc);
        var result = KeyBuilder.NotificationSk(timestamp, "Eligibility");
        Assert.Equal("2024-06-15T10:30:00.000Z#Eligibility", result);
    }
}
