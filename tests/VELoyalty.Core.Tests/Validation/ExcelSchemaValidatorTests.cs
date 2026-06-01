using Xunit;
using VELoyalty.Core.Validation;

namespace VELoyalty.Core.Tests.Validation;

public class ExcelSchemaValidatorTests
{
    // ─── ValidateColumns ────────────────────────────────────────────────────────

    [Fact]
    public void ValidateColumns_AllRequiredPresent_ReturnsSuccess()
    {
        var headers = new List<string>
        {
            "DIST_ID", "DIST_NAME", "ITEM_ID", "ITEM_NAME",
            "OC_QTY", "AMNT", "CHALLAN_DATE", "CHALLAN_NO", "NET_AMNT", "NOTE"
        };

        var result = ExcelSchemaValidator.ValidateColumns(headers);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateColumns_CaseInsensitive_ReturnsSuccess()
    {
        var headers = new List<string>
        {
            "dist_id", "Dist_Name", "item_id", "Item_Name",
            "oc_qty", "amnt", "challan_date", "challan_no", "net_amnt", "note"
        };

        var result = ExcelSchemaValidator.ValidateColumns(headers);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateColumns_MissingColumn_ReturnsFailure()
    {
        var headers = new List<string>
        {
            "DIST_ID", "DIST_NAME", "ITEM_ID", "ITEM_NAME",
            "OC_QTY", "AMNT", "CHALLAN_DATE", "CHALLAN_NO", "NET_AMNT"
            // Missing "NOTE"
        };

        var result = ExcelSchemaValidator.ValidateColumns(headers);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("NOTE"));
    }

    [Fact]
    public void ValidateColumns_MultipleMissing_ReportsAll()
    {
        var headers = new List<string>
        {
            "DIST_ID", "DIST_NAME"
        };

        var result = ExcelSchemaValidator.ValidateColumns(headers);

        Assert.False(result.IsValid);
        Assert.True(result.Errors.Count >= 8); // At least 8 missing columns
    }

    [Fact]
    public void ValidateColumns_EmptyHeaders_ReturnsFailure()
    {
        var result = ExcelSchemaValidator.ValidateColumns(new List<string>());

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("No columns"));
    }

    [Fact]
    public void ValidateColumns_NullHeaders_ReturnsFailure()
    {
        var result = ExcelSchemaValidator.ValidateColumns(null!);

        Assert.False(result.IsValid);
    }

    // ─── ValidateRow ────────────────────────────────────────────────────────────

    [Fact]
    public void ValidateRow_AllValidData_ReturnsSuccess()
    {
        var row = CreateValidRow();

        var result = ExcelSchemaValidator.ValidateRow(row, 1);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateRow_EmptyDistId_ReturnsFailure()
    {
        var row = CreateValidRow();
        row["DIST_ID"] = "";

        var result = ExcelSchemaValidator.ValidateRow(row, 2);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Row 2") && e.Contains("DIST_ID"));
    }

    [Fact]
    public void ValidateRow_EmptyItemId_ReturnsFailure()
    {
        var row = CreateValidRow();
        row["ITEM_ID"] = "";

        var result = ExcelSchemaValidator.ValidateRow(row, 3);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("ITEM_ID"));
    }

    [Fact]
    public void ValidateRow_InvalidChallanDate_ReturnsFailure()
    {
        var row = CreateValidRow();
        row["CHALLAN_DATE"] = "not-a-date";

        var result = ExcelSchemaValidator.ValidateRow(row, 5);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("CHALLAN_DATE") && e.Contains("not a valid date"));
    }

    [Fact]
    public void ValidateRow_NonNumericNetAmnt_ReturnsFailure()
    {
        var row = CreateValidRow();
        row["NET_AMNT"] = "abc";

        var result = ExcelSchemaValidator.ValidateRow(row, 6);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("NET_AMNT") && e.Contains("not a valid numeric"));
    }

    [Fact]
    public void ValidateRow_NegativeNetAmnt_ReturnsFailure()
    {
        var row = CreateValidRow();
        row["NET_AMNT"] = "-100";

        var result = ExcelSchemaValidator.ValidateRow(row, 7);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("NET_AMNT") && e.Contains("negative"));
    }

    [Fact]
    public void ValidateRow_ZeroNetAmnt_ReturnsSuccess()
    {
        var row = CreateValidRow();
        row["NET_AMNT"] = "0";

        var result = ExcelSchemaValidator.ValidateRow(row, 7);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateRow_NoteWithoutPhoneOrStaffId_ReturnsFailure()
    {
        var row = CreateValidRow();
        row["NOTE"] = "Some random note without identifiers";

        var result = ExcelSchemaValidator.ValidateRow(row, 8);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("NOTE") && e.Contains("phone number"));
    }

    [Fact]
    public void ValidateRow_EmptyNote_ReturnsFailure()
    {
        var row = CreateValidRow();
        row["NOTE"] = "";

        var result = ExcelSchemaValidator.ValidateRow(row, 9);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("NOTE") && e.Contains("required"));
    }

    [Fact]
    public void ValidateRow_MultipleErrors_ReportsAll()
    {
        var row = new Dictionary<string, string?>
        {
            ["DIST_ID"] = "",
            ["DIST_NAME"] = "",
            ["ITEM_ID"] = "",
            ["ITEM_NAME"] = "",
            ["OC_QTY"] = "",
            ["AMNT"] = "",
            ["CHALLAN_DATE"] = "",
            ["CHALLAN_NO"] = "",
            ["NET_AMNT"] = "",
            ["NOTE"] = ""
        };

        var result = ExcelSchemaValidator.ValidateRow(row, 10);

        Assert.False(result.IsValid);
        Assert.True(result.Errors.Count >= 5); // Multiple fields should have errors
    }

    // ─── ExtractPhoneNumber ─────────────────────────────────────────────────────

    [Fact]
    public void ExtractPhoneNumber_ValidNote_ReturnsNormalizedPhone()
    {
        var note = "Name: John Doe Mb No: 01712345678";

        var phone = ExcelSchemaValidator.ExtractPhoneNumber(note);

        Assert.Equal("+8801712345678", phone);
    }

    [Fact]
    public void ExtractPhoneNumber_NullNote_ReturnsNull()
    {
        var phone = ExcelSchemaValidator.ExtractPhoneNumber(null);

        Assert.Null(phone);
    }

    [Fact]
    public void ExtractPhoneNumber_NoPhoneInNote_ReturnsNull()
    {
        var phone = ExcelSchemaValidator.ExtractPhoneNumber("Credit Staff Id: 12345");

        Assert.Null(phone);
    }

    // ─── ExtractCustomerName ────────────────────────────────────────────────────

    [Fact]
    public void ExtractCustomerName_ValidNote_ReturnsName()
    {
        var note = "Name: John Doe Mb No: 01712345678";

        var name = ExcelSchemaValidator.ExtractCustomerName(note);

        Assert.Equal("John Doe", name);
    }

    [Fact]
    public void ExtractCustomerName_NullNote_ReturnsNull()
    {
        var name = ExcelSchemaValidator.ExtractCustomerName(null);

        Assert.Null(name);
    }

    [Fact]
    public void ExtractCustomerName_NoNameInNote_ReturnsNull()
    {
        var name = ExcelSchemaValidator.ExtractCustomerName("Mb No: 01712345678");

        Assert.Null(name);
    }

    // ─── ExtractStaffId ─────────────────────────────────────────────────────────

    [Fact]
    public void ExtractStaffId_ValidNote_ReturnsStaffId()
    {
        var note = "Credit Staff Id: 12345 Name: Staff Person Mb No: 01712345678";

        var staffId = ExcelSchemaValidator.ExtractStaffId(note);

        Assert.Equal("12345", staffId);
    }

    [Fact]
    public void ExtractStaffId_NullNote_ReturnsNull()
    {
        var staffId = ExcelSchemaValidator.ExtractStaffId(null);

        Assert.Null(staffId);
    }

    [Fact]
    public void ExtractStaffId_NoStaffIdInNote_ReturnsNull()
    {
        var staffId = ExcelSchemaValidator.ExtractStaffId("Name: John Doe Mb No: 01712345678");

        Assert.Null(staffId);
    }

    // ─── ConvertFromThousands ───────────────────────────────────────────────────

    [Fact]
    public void ConvertFromThousands_MultipliesBy1000()
    {
        Assert.Equal(265.0m, ExcelSchemaValidator.ConvertFromThousands(0.2650m));
        Assert.Equal(56200.0m, ExcelSchemaValidator.ConvertFromThousands(56.20m));
        Assert.Equal(1000m, ExcelSchemaValidator.ConvertFromThousands(1m));
    }

    // ─── TryParseChallanDate ────────────────────────────────────────────────────

    [Fact]
    public void TryParseChallanDate_ValidDateTimeFormat_ReturnsTrue()
    {
        var success = ExcelSchemaValidator.TryParseChallanDate("15/07/2024 10:30:00 AM", out var result);

        Assert.True(success);
        Assert.Equal(new DateOnly(2024, 7, 15), result);
    }

    [Fact]
    public void TryParseChallanDate_DateOnlyFormat_ReturnsTrue()
    {
        var success = ExcelSchemaValidator.TryParseChallanDate("15/07/2024", out var result);

        Assert.True(success);
        Assert.Equal(new DateOnly(2024, 7, 15), result);
    }

    [Fact]
    public void TryParseChallanDate_InvalidFormat_ReturnsFalse()
    {
        var success = ExcelSchemaValidator.TryParseChallanDate("not-a-date", out _);

        Assert.False(success);
    }

    // ─── Helper ─────────────────────────────────────────────────────────────────

    private static Dictionary<string, string?> CreateValidRow()
    {
        return new Dictionary<string, string?>
        {
            ["DIST_ID"] = "1001",
            ["DIST_NAME"] = "Vision Emporium Gulshan",
            ["ITEM_ID"] = "ITEM-001",
            ["ITEM_NAME"] = "Samsung TV 55\"",
            ["OC_QTY"] = "1",
            ["AMNT"] = "56.20",
            ["CHALLAN_DATE"] = "15/07/2024 10:30:00 AM",
            ["CHALLAN_NO"] = "CHN-2024-001",
            ["NET_AMNT"] = "55.00",
            ["NOTE"] = "Name: John Doe Mb No: 01712345678"
        };
    }
}
