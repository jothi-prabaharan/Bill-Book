using Master.Api.Services;
using Master.Repository.Repositories;
using Master.Repository.Clients;
using Master.Entity.Models;
using Master.Entity.TableEntities;
using Master.Repository;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.CustomFields;
using Xunit;

namespace Master.Api.Tests;

/// <summary>
/// Custom field definitions (TK-110): the rules a definition meets, the key that
/// never changes, the fifty-per-kind limit, and RLS on the new cfd schema.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class CustomFieldTests
{
    private readonly PostgresFixture _postgres;

    public CustomFieldTests(PostgresFixture postgres) => _postgres = postgres;

    private static SaveCustomFieldRequest Region() => new()
    {
        EntityKind = "Contact",
        Key = "region",
        Label = "Region",
        DataType = "Dropdown",
        Options =
        [
            new SaveCustomFieldOptionRequest { Value = "north", Label = "North" },
            new SaveCustomFieldOptionRequest { Value = "south", Label = "South" },
        ],
    };

    /// <summary>The card's "done when", end to end: defined in Master, read back as a definition, and the validator refuses a value outside its options.</summary>
    [SkippableFact]
    public async Task A_branch_defines_a_contact_dropdown_and_the_validator_refuses_a_value_outside_it()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        await using ContactsDbContext db = _postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());
        CustomFieldRepository service = new(db);

        CustomFieldResult saved = await service.SaveAsync(null, Region(), default);
        Assert.Equal(CustomFieldOutcome.Ok, saved.Outcome);

        db.ChangeTracker.Clear();
        CustomFieldDefinitionsResponse read = await service.ForKindAsync(CustomFieldEntityKind.Contact, default);
        CustomFieldDefinitionDto region = Assert.Single(read.Fields);
        Assert.Equal(2, region.Options.Count);

        var west = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, System.Text.Json.JsonElement>>("""{ "region": "west" }""")!;
        Assert.Equal(["Region must be one of its options."], CustomFieldValidator.Validate(read.Fields, west));
    }

    [SkippableFact]
    public async Task A_key_cannot_be_changed_nor_reused_on_the_same_kind()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        await using ContactsDbContext db = _postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());
        CustomFieldRepository service = new(db);
        long id = (await service.SaveAsync(null, Region(), default)).Id;
        db.ChangeTracker.Clear();

        SaveCustomFieldRequest renamed = Region();
        renamed.Key = "area";
        CustomFieldResult rename = await service.SaveAsync(id, renamed, default);

        SaveCustomFieldRequest relabelled = Region();
        relabelled.Label = "Sales region";
        CustomFieldResult relabel = await service.SaveAsync(id, relabelled, default);

        CustomFieldResult again = await service.SaveAsync(null, Region(), default);

        Assert.Equal(CustomFieldOutcome.Refused, rename.Outcome);
        Assert.Equal(CustomFieldOutcome.Ok, relabel.Outcome);
        Assert.Equal(CustomFieldOutcome.Duplicate, again.Outcome);
    }

    [SkippableFact]
    public async Task A_kind_holds_at_most_fifty_active_fields()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        await using ContactsDbContext db = _postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());
        CustomFieldRepository service = new(db);

        for (int i = 0; i < CustomFieldRepository.MaxActivePerKind; i++)
        {
            SaveCustomFieldRequest field = new() { EntityKind = "Item", Key = $"f{i}", Label = $"Field {i}", DataType = "Text" };
            Assert.Equal(CustomFieldOutcome.Ok, (await service.SaveAsync(null, field, default)).Outcome);
            db.ChangeTracker.Clear();
        }

        CustomFieldResult fiftyFirst = await service.SaveAsync(null, new SaveCustomFieldRequest { EntityKind = "Item", Key = "extra", Label = "Extra", DataType = "Text" }, default);
        CustomFieldResult inactive = await service.SaveAsync(null, new SaveCustomFieldRequest { EntityKind = "Item", Key = "parked", Label = "Parked", DataType = "Text", IsActive = false }, default);
        CustomFieldResult otherKind = await service.SaveAsync(null, new SaveCustomFieldRequest { EntityKind = "Contact", Key = "extra", Label = "Extra", DataType = "Text" }, default);

        Assert.Equal(CustomFieldOutcome.LimitReached, fiftyFirst.Outcome);
        Assert.Equal(CustomFieldOutcome.Ok, inactive.Outcome);
        Assert.Equal(CustomFieldOutcome.Ok, otherKind.Outcome);
    }

    [SkippableFact]
    public async Task Row_level_security_covers_the_cfd_schema()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        await using ContactsDbContext db = _postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());

        Assert.Equal(
            string.Empty,
            string.Join("; ", await BillBook.Tests.Shared.RlsAudit.UnprotectedAsync(db, "cfd", "__EFMigrationsHistory")));
    }

    // ---- the pure rules -----------------------------------------------------------

    [Theory]
    [InlineData("Region")]
    [InlineData("1region")]
    [InlineData("region-code")]
    [InlineData("")]
    public void A_key_must_be_lowercase_letters_digits_and_underscores(string key)
    {
        SaveCustomFieldRequest request = Region();
        request.Key = key;

        Assert.NotNull(CustomFieldRules.Refusal(request, null));
    }

    [Fact]
    public void A_field_type_or_kind_cannot_change_and_an_option_cannot_be_removed()
    {
        CustomFieldDefinition existing = new()
        {
            EntityKind = CustomFieldEntityKind.Contact,
            Key = "region",
            Label = "Region",
            DataType = CustomFieldDataType.Dropdown,
            Options = [new() { Value = "north", Label = "North" }, new() { Value = "south", Label = "South" }],
        };

        SaveCustomFieldRequest retyped = Region();
        retyped.DataType = "Text";
        retyped.Options = [];
        SaveCustomFieldRequest moved = Region();
        moved.EntityKind = "Item";
        SaveCustomFieldRequest dropped = Region();
        dropped.Options = [dropped.Options[0]];
        SaveCustomFieldRequest deactivated = Region();
        deactivated.Options[1].Active = false;

        Assert.NotNull(CustomFieldRules.Refusal(retyped, existing));
        Assert.NotNull(CustomFieldRules.Refusal(moved, existing));
        Assert.Contains("south", CustomFieldRules.Refusal(dropped, existing));
        Assert.Null(CustomFieldRules.Refusal(deactivated, existing));
    }

    [Fact]
    public void Options_belong_only_to_choice_fields_and_a_default_must_fit()
    {
        SaveCustomFieldRequest noOptions = Region();
        noOptions.Options = [];
        SaveCustomFieldRequest textWithOptions = Region();
        textWithOptions.DataType = "Text";
        SaveCustomFieldRequest duplicate = Region();
        duplicate.Options[1].Value = "north";
        SaveCustomFieldRequest badDefault = Region();
        badDefault.DefaultValue = "west";
        SaveCustomFieldRequest goodDefault = Region();
        goodDefault.DefaultValue = "north";
        SaveCustomFieldRequest numberDefault = new() { EntityKind = "Item", Key = "shelf", Label = "Shelf", DataType = "Integer", DefaultValue = "three" };

        Assert.NotNull(CustomFieldRules.Refusal(noOptions, null));
        Assert.NotNull(CustomFieldRules.Refusal(textWithOptions, null));
        Assert.NotNull(CustomFieldRules.Refusal(duplicate, null));
        Assert.NotNull(CustomFieldRules.Refusal(badDefault, null));
        Assert.Null(CustomFieldRules.Refusal(goodDefault, null));
        Assert.NotNull(CustomFieldRules.Refusal(numberDefault, null));
    }
}
