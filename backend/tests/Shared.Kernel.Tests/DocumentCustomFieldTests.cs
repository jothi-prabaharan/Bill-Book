using System.Text.Json;
using Shared.Kernel.CustomFields;
using Shared.Kernel.Printing;
using Xunit;

namespace Shared.Kernel.Tests;

/// <summary>Carry-forward between documents and custom fields in a print payload (TK-112).</summary>
public sealed class DocumentCustomFieldTests
{
    private static CustomFieldDefinitionDto Field(CustomFieldEntityKind kind, string key, bool carry = false, bool active = true, bool print = true, CustomFieldDataType type = CustomFieldDataType.Text) => new()
    {
        EntityKind = kind,
        Key = key,
        Label = key,
        DataType = type,
        CarryForward = carry,
        IsActive = active,
        ShowOnPrint = print,
        Options = type == CustomFieldDataType.Dropdown ? [new() { Value = "n", Label = "North" }] : [],
    };

    private static Dictionary<string, JsonElement> Values(string json) =>
        JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;

    [Fact]
    public void Only_carry_forward_keys_the_target_also_has_and_accepts_are_carried()
    {
        Dictionary<string, JsonElement> carried = DocumentCustomFieldGate.Carry(
            Values("""{ "site_ref": "TOWER-B", "internal": "x", "gone": "y", "region": "zz" }"""),
            [
                Field(CustomFieldEntityKind.Quote, "site_ref", carry: true),
                Field(CustomFieldEntityKind.Quote, "internal"),
                Field(CustomFieldEntityKind.Quote, "gone", carry: true),
                Field(CustomFieldEntityKind.Quote, "region", carry: true),
            ],
            [
                Field(CustomFieldEntityKind.Invoice, "site_ref"),
                Field(CustomFieldEntityKind.Invoice, "internal"),
                Field(CustomFieldEntityKind.Invoice, "gone", active: false),
                Field(CustomFieldEntityKind.Invoice, "region", type: CustomFieldDataType.Dropdown),
            ]);

        // Not carry-forward, inactive on the target, and not one of the target's options: all left behind.
        Assert.Equal(["site_ref"], carried.Keys);
        Assert.Equal("TOWER-B", carried["site_ref"].GetString());
    }

    [Fact]
    public void Print_adds_fields_marked_to_print_as_text()
    {
        var payload = new PrintPayload();
        using JsonDocument values = JsonDocument.Parse("""{ "site_ref": "TOWER-B", "secret": "x", "region": "n", "vip": true }""");

        CustomFieldPrint.Add(payload, values,
        [
            Field(CustomFieldEntityKind.Invoice, "site_ref"),
            Field(CustomFieldEntityKind.Invoice, "secret", print: false),
            Field(CustomFieldEntityKind.Invoice, "region", type: CustomFieldDataType.Dropdown),
            Field(CustomFieldEntityKind.Invoice, "vip", type: CustomFieldDataType.Boolean),
        ]);

        Assert.Equal("TOWER-B", payload.Singles["custom.site_ref"]);
        Assert.Equal("North", payload.Singles["custom.region"]);
        Assert.Equal("Yes", payload.Singles["custom.vip"]);
        Assert.False(payload.Singles.ContainsKey("custom.secret"));
    }

    [Fact]
    public void Every_document_type_with_custom_fields_maps_to_its_kind()
    {
        Assert.Equal(CustomFieldEntityKind.Invoice, CustomFieldKinds.ForDocumentType("INV"));
        Assert.Equal(CustomFieldEntityKind.Bill, CustomFieldKinds.ForDocumentType("bil"));
        Assert.Null(CustomFieldKinds.ForDocumentType("JNL"));
    }
}
