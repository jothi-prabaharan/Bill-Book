using System.Text.Json;
using Shared.Kernel.CustomFields;
using Xunit;

namespace Shared.Kernel.Tests;

/// <summary>The shared custom field validator (TK-110): each data type, unknown keys, and required on new saves only.</summary>
public sealed class CustomFieldValidatorTests
{
    private static CustomFieldDefinitionDto Field(string key, CustomFieldDataType type, bool required = false, bool active = true, int? maxLength = null) =>
        new()
        {
            Key = key,
            Label = key switch { "region" => "Region", _ => key },
            DataType = type,
            IsRequired = required,
            IsActive = active,
            MaxLength = maxLength,
            Options = type is CustomFieldDataType.Dropdown or CustomFieldDataType.MultiSelect
                ?
                [
                    new() { Value = "north", Label = "North" },
                    new() { Value = "south", Label = "South" },
                    new() { Value = "east", Label = "East", Active = false },
                ]
                : [],
        };

    private static Dictionary<string, JsonElement> Values(string json) =>
        JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;

    /// <summary>The card's "done when".</summary>
    [Fact]
    public void A_contact_dropdown_refuses_a_value_that_is_not_one_of_its_options()
    {
        CustomFieldDefinitionDto region = Field("region", CustomFieldDataType.Dropdown);

        Assert.Empty(CustomFieldValidator.Validate([region], Values("""{ "region": "north" }""")));
        Assert.Equal(["Region must be one of its options."], CustomFieldValidator.Validate([region], Values("""{ "region": "west" }""")));
    }

    [Fact]
    public void A_key_with_no_definition_is_refused()
    {
        Assert.Equal(
            ["\"site_ref\" is not a custom field here."],
            CustomFieldValidator.Validate([Field("region", CustomFieldDataType.Dropdown)], Values("""{ "site_ref": "A-1" }""")));
    }

    [Fact]
    public void A_required_field_is_enforced_on_new_saves_only()
    {
        CustomFieldDefinitionDto field = Field("po_ref", CustomFieldDataType.Text, required: true);

        Assert.Equal(["po_ref is required."], CustomFieldValidator.Validate([field], Values("{}")));
        Assert.Equal(["po_ref is required."], CustomFieldValidator.Validate([field], Values("""{ "po_ref": "  " }""")));

        // An existing record saved again is not invalidated by a field made required after it was written.
        Assert.Empty(CustomFieldValidator.Validate([field], Values("{}"), previous: Values("{}")));
    }

    [Theory]
    [InlineData(CustomFieldDataType.Text, "\"hello\"", true)]
    [InlineData(CustomFieldDataType.Text, "12", false)]
    [InlineData(CustomFieldDataType.LongText, "\"a long note\"", true)]
    [InlineData(CustomFieldDataType.Integer, "42", true)]
    [InlineData(CustomFieldDataType.Integer, "4.2", false)]
    [InlineData(CustomFieldDataType.Integer, "\"42\"", false)]
    [InlineData(CustomFieldDataType.Decimal, "4.25", true)]
    [InlineData(CustomFieldDataType.Decimal, "\"4.25\"", false)]
    [InlineData(CustomFieldDataType.Date, "\"2026-09-27\"", true)]
    [InlineData(CustomFieldDataType.Date, "\"27/09/2026\"", false)]
    [InlineData(CustomFieldDataType.Date, "\"2026-02-30\"", false)]
    [InlineData(CustomFieldDataType.Boolean, "true", true)]
    [InlineData(CustomFieldDataType.Boolean, "\"yes\"", false)]
    [InlineData(CustomFieldDataType.Dropdown, "\"south\"", true)]
    [InlineData(CustomFieldDataType.Dropdown, "\"east\"", false)]
    [InlineData(CustomFieldDataType.MultiSelect, "[\"north\",\"south\"]", true)]
    [InlineData(CustomFieldDataType.MultiSelect, "[\"north\",\"north\"]", false)]
    [InlineData(CustomFieldDataType.MultiSelect, "[\"north\",\"west\"]", false)]
    [InlineData(CustomFieldDataType.MultiSelect, "\"north\"", false)]
    [InlineData(CustomFieldDataType.Email, "\"accounts@example.com\"", true)]
    [InlineData(CustomFieldDataType.Email, "\"accounts at example\"", false)]
    [InlineData(CustomFieldDataType.Url, "\"https://example.com/site\"", true)]
    [InlineData(CustomFieldDataType.Url, "\"ftp://example.com\"", false)]
    [InlineData(CustomFieldDataType.Url, "\"example.com\"", false)]
    [InlineData(CustomFieldDataType.Phone, "\"9876543210\"", true)]
    [InlineData(CustomFieldDataType.Phone, "\"+44 20 7946 0958\"", true)]
    [InlineData(CustomFieldDataType.Phone, "\"call me\"", false)]
    public void Each_type_accepts_its_own_values_and_refuses_others(CustomFieldDataType type, string json, bool valid)
    {
        CustomFieldDefinitionDto field = Field("f", type);
        IReadOnlyList<string> errors = CustomFieldValidator.Validate([field], Values($$"""{ "f": {{json}} }"""));

        Assert.Equal(valid, errors.Count == 0);
    }

    [Fact]
    public void A_text_field_holds_to_its_maximum_length()
    {
        CustomFieldDefinitionDto field = Field("code", CustomFieldDataType.Text, maxLength: 5);

        Assert.Empty(CustomFieldValidator.Validate([field], Values("""{ "code": "ABCDE" }""")));
        Assert.Equal(["code cannot exceed 5 characters."], CustomFieldValidator.Validate([field], Values("""{ "code": "ABCDEF" }""")));
    }

    [Fact]
    public void A_deactivated_field_keeps_its_stored_value_but_takes_no_new_one()
    {
        CustomFieldDefinitionDto field = Field("old", CustomFieldDataType.Text, active: false);
        Dictionary<string, JsonElement> stored = Values("""{ "old": "kept" }""");

        Assert.Empty(CustomFieldValidator.Validate([field], stored, previous: stored));
        Assert.Equal(["old is no longer in use, so it cannot be set."], CustomFieldValidator.Validate([field], Values("""{ "old": "changed" }"""), previous: stored));
        Assert.NotEmpty(CustomFieldValidator.Validate([field], stored));
    }

    [Fact]
    public void A_deactivated_option_already_chosen_can_stay_but_cannot_be_newly_chosen()
    {
        CustomFieldDefinitionDto region = Field("region", CustomFieldDataType.Dropdown);
        Dictionary<string, JsonElement> east = Values("""{ "region": "east" }""");

        Assert.Empty(CustomFieldValidator.Validate([region], east, previous: east));
        Assert.NotEmpty(CustomFieldValidator.Validate([region], east, previous: Values("""{ "region": "north" }""")));
    }

    [Fact]
    public void Null_and_empty_values_count_as_not_given()
    {
        CustomFieldDefinitionDto field = Field("f", CustomFieldDataType.Integer);

        Assert.Empty(CustomFieldValidator.Validate([field], Values("""{ "f": null }""")));
        Assert.Empty(CustomFieldValidator.Validate([Field("m", CustomFieldDataType.MultiSelect)], Values("""{ "m": [] }""")));
    }
}
