using System.Text.Json;
using System.Text.Json.Serialization;
using Purchase.Entity.Models;
using Shared.Kernel.Documents;
using Shared.Kernel.Tax;
using Xunit;

namespace Purchase.Api.Tests;

/// <summary>
/// Verifies that services read enums by name and number (TK-124).
/// Prevents screens sending string enum names (e.g. lineType: "Stock", taxTreatment: "Taxable")
/// from failing model binding with a 400.
/// </summary>
public sealed class EnumJsonBindingTests
{
    private readonly JsonSerializerOptions _options;

    public EnumJsonBindingTests()
    {
        _options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        _options.Converters.Add(new JsonStringEnumConverter());
    }

    [Fact]
    public void PurchaseBillLine_BindsEnumByName()
    {
        const string json = """
        {
            "description": "Consulting Services",
            "quantity": 1,
            "unitPrice": 5000,
            "lineType": "Expense",
            "taxTreatment": "Exempt",
            "itcEligibility": "Ineligible"
        }
        """;

        var line = JsonSerializer.Deserialize<SaveBillLineRequest>(json, _options);

        Assert.NotNull(line);
        Assert.Equal(DocumentLineType.Expense, line.LineType);
        Assert.Equal(TaxTreatment.Exempt, line.TaxTreatment);
        Assert.Equal(ItcEligibility.Ineligible, line.ItcEligibility);
    }

    [Fact]
    public void PurchaseBillLine_BindsEnumByNumber()
    {
        // DocumentLineType.Stock = 1, TaxTreatment.Taxable = 1, ItcEligibility.Eligible = 1 (or defined integer)
        string json = $$"""
        {
            "description": "Stock Item",
            "quantity": 2,
            "unitPrice": 100,
            "lineType": {{(int)DocumentLineType.Stock}},
            "taxTreatment": {{(int)TaxTreatment.Taxable}},
            "itcEligibility": {{(int)ItcEligibility.Eligible}}
        }
        """;

        var line = JsonSerializer.Deserialize<SaveBillLineRequest>(json, _options);

        Assert.NotNull(line);
        Assert.Equal(DocumentLineType.Stock, line.LineType);
        Assert.Equal(TaxTreatment.Taxable, line.TaxTreatment);
        Assert.Equal(ItcEligibility.Eligible, line.ItcEligibility);
    }

    [Fact]
    public void PurchaseBillLine_SerializesEnumsAsStrings()
    {
        var line = new SaveBillLineRequest
        {
            Description = "Capital Equipment",
            Quantity = 1,
            UnitPrice = 25000,
            LineType = DocumentLineType.Capital,
            TaxTreatment = TaxTreatment.Taxable,
            ItcEligibility = ItcEligibility.Blocked
        };

        string json = JsonSerializer.Serialize(line, _options);

        Assert.Contains("\"lineType\":\"Capital\"", json);
        Assert.Contains("\"taxTreatment\":\"Taxable\"", json);
        Assert.Contains("\"itcEligibility\":\"Blocked\"", json);
    }
}
