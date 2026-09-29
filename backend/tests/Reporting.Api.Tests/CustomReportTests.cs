using System.Text.Json;
using Reporting.Api.Services;
using Reporting.Repository.Repositories;
using Reporting.Entity.Enums;
using Reporting.Entity.Models;
using Reporting.Repository;
using Reporting.Repository.SeedData;
using Shared.Kernel.CustomFields;
using Shared.Kernel.Interfaces;
using Xunit;

namespace Reporting.Api.Tests;

/// <summary>
/// Custom reports and their datasets (TK-113). The card's "done when" is the
/// first test: a user builds "sales by salesperson and site reference" from
/// the sales-lines dataset, saves it, and reopens it from the catalog.
///
/// The saved-report tests need the <c>rpt</c> schema and skip without a
/// PostgreSQL. Running a dataset's query needs the other services' schemas as
/// well, so what is asserted here is the layout's round trip, the permission
/// and the dropped column; the column rules are asserted without a database.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class CustomReportTests
{
    private static readonly HashSet<string> SalesUser = ["reports.view", "sales.view"];
    private static readonly HashSet<string> Buyer = ["reports.view", "reports.edit", "purchase.view"];
    private static readonly HashSet<string> NoPurchases = ["reports.view", "sales.view", "inventory.view"];

    private readonly PostgresFixture _postgres;

    public CustomReportTests(PostgresFixture postgres) => _postgres = postgres;

    /// <summary>The card's "done when".</summary>
    [SkippableFact]
    public async Task A_user_builds_sales_by_salesperson_and_site_reference_saves_it_and_reopens_it_from_the_catalog()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Branch branch = await BranchAsync();
        Guid me = Guid.NewGuid();
        await using ReportingDbContext db = _postgres.CreateContext(branch.CustomerId, branch.OrgId);
        CustomReportRepository service = Service(db, me);

        CustomReportModel saved = (await service.CreateAsync(new CustomReportModel
        {
            DatasetKey = "dataset-sales-lines",
            Title = "Sales by salesperson and site reference",
            Layout = new ReportQueryRequest
            {
                Columns = ["custom.salesperson", "custom.site_reference", "customer", "lineTotalBase"],
                GroupBy = ["custom.salesperson", "custom.site_reference"],
                Sorts = [new ReportSortModel { Column = "custom.salesperson" }],
                Page = new ReportPageModel { Number = 4, Size = 100 },
            },
        }, mayShare: false, SalesUser, CancellationToken.None))!;

        ReportCatalogItemView listed = Assert.Single(
            await service.CatalogAsync(SalesUser, CancellationToken.None),
            item => item.CustomReportId == saved.CustomReportId);
        Assert.Equal("dataset-sales-lines", listed.ReportKey);
        Assert.Equal(ReportModule.Custom, listed.Module);
        Assert.Equal("Sales by salesperson and site reference", listed.Title);

        CustomReportModel reopened = (await Service(db, me).GetAsync(listed.CustomReportId!.Value, false, SalesUser, CancellationToken.None))!;
        Assert.Equal(["custom.salesperson", "custom.site_reference", "customer", "lineTotalBase"], reopened.Layout.Columns);
        Assert.Equal(["custom.salesperson", "custom.site_reference"], reopened.Layout.GroupBy);
        Assert.Equal("custom.salesperson", Assert.Single(reopened.Layout.Sorts).Column);
        Assert.Empty(reopened.DroppedColumns);
        Assert.False(reopened.IsShared);
        Assert.True(reopened.MayEdit);

        // The page is not part of a layout.
        Assert.Equal(1, reopened.Layout.Page.Number);
        Assert.Equal(100, reopened.Layout.Page.Size);

        // And it is this user's: another user of the branch does not see it.
        Assert.DoesNotContain(
            await Service(db, Guid.NewGuid()).CatalogAsync(SalesUser, CancellationToken.None),
            item => item.CustomReportId == saved.CustomReportId);
    }

    [SkippableFact]
    public async Task A_user_without_purchase_view_does_not_see_a_shared_purchase_lines_report()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Branch branch = await BranchAsync();
        await using ReportingDbContext db = _postgres.CreateContext(branch.CustomerId, branch.OrgId);

        CustomReportModel saved = (await Service(db, Guid.NewGuid()).CreateAsync(new CustomReportModel
        {
            DatasetKey = "dataset-purchase-lines",
            Title = "Purchases by vendor",
            IsShared = true,
            Layout = new ReportQueryRequest { Columns = ["vendor", "lineTotalBase"], GroupBy = ["vendor"] },
        }, mayShare: true, Buyer, CancellationToken.None))!;

        CustomReportRepository other = Service(db, Guid.NewGuid());

        Assert.Contains(await other.CatalogAsync(Buyer, CancellationToken.None), i => i.CustomReportId == saved.CustomReportId);
        Assert.DoesNotContain(await other.CatalogAsync(NoPurchases, CancellationToken.None), i => i.CustomReportId == saved.CustomReportId);
        Assert.Null(await other.GetAsync(saved.CustomReportId, false, NoPurchases, CancellationToken.None));

        // Nor can they save one: the dataset is absent for them, as a report key they may not run is.
        Assert.Null(await other.CreateAsync(new CustomReportModel
        {
            DatasetKey = "dataset-purchase-lines",
            Title = "Mine",
            Layout = new ReportQueryRequest { Columns = ["vendor"] },
        }, false, NoPurchases, CancellationToken.None));

        // And the dataset is not in their catalog either.
        List<ReportCatalogItemView> catalog = await Catalog(db).ListAsync(NoPurchases, CancellationToken.None);
        Assert.DoesNotContain(catalog, i => i.ReportKey == "dataset-purchase-lines");
        Assert.Contains(catalog, i => i.ReportKey == "dataset-sales-lines" && i.IsDataset);
    }

    [SkippableFact]
    public async Task A_deactivated_fields_column_is_dropped_with_a_notice_when_the_report_is_opened()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Branch branch = await BranchAsync();
        Guid me = Guid.NewGuid();
        await using ReportingDbContext db = _postgres.CreateContext(branch.CustomerId, branch.OrgId);

        CustomReportModel saved = (await Service(db, me).CreateAsync(new CustomReportModel
        {
            DatasetKey = "dataset-sales-lines",
            Title = "Sales by site",
            Layout = new ReportQueryRequest
            {
                Columns = ["custom.site_reference", "lineTotalBase"],
                GroupBy = ["custom.site_reference"],
                Filters = [new ReportFilterModel { Column = "custom.site_reference", Operator = FilterOperator.Contains, Value = "Chennai" }],
            },
        }, false, SalesUser, CancellationToken.None))!;

        // An administrator deactivates Site reference.
        CustomReportModel reopened = (await Service(db, me, Invoice(siteReferenceActive: false))
            .GetAsync(saved.CustomReportId, false, SalesUser, CancellationToken.None))!;

        Assert.Equal(["Site reference"], reopened.DroppedColumns);
        Assert.Equal(["lineTotalBase"], reopened.Layout.Columns);
        Assert.Empty(reopened.Layout.GroupBy);
        Assert.Empty(reopened.Layout.Filters);
    }

    [SkippableFact]
    public async Task Sharing_needs_reports_edit_and_a_shared_report_is_only_changed_by_someone_who_could_share_it()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Branch branch = await BranchAsync();
        await using ReportingDbContext db = _postgres.CreateContext(branch.CustomerId, branch.OrgId);
        CustomReportModel model = new()
        {
            DatasetKey = "dataset-sales-lines",
            Title = "Branch sales",
            IsShared = true,
            Layout = new ReportQueryRequest { Columns = ["customer", "lineTotalBase"] },
        };

        await Assert.ThrowsAsync<ReportQueryException>(() =>
            Service(db, Guid.NewGuid()).CreateAsync(model, mayShare: false, SalesUser, CancellationToken.None));

        CustomReportModel saved = (await Service(db, Guid.NewGuid()).CreateAsync(model, mayShare: true, SalesUser, CancellationToken.None))!;
        CustomReportRepository reader = Service(db, Guid.NewGuid());

        Assert.False((await reader.GetAsync(saved.CustomReportId, false, SalesUser, CancellationToken.None))!.MayEdit);
        Assert.Null(await reader.UpdateAsync(saved.CustomReportId, model, false, SalesUser, CancellationToken.None));
        Assert.False(await reader.DeleteAsync(saved.CustomReportId, false, CancellationToken.None));
        Assert.True(await reader.DeleteAsync(saved.CustomReportId, true, CancellationToken.None));
        Assert.DoesNotContain(await reader.CatalogAsync(SalesUser, CancellationToken.None), i => i.CustomReportId == saved.CustomReportId);
    }

    [SkippableFact]
    public async Task Saving_a_layout_that_names_a_column_the_dataset_lacks_is_refused()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Branch branch = await BranchAsync();
        await using ReportingDbContext db = _postgres.CreateContext(branch.CustomerId, branch.OrgId);

        ReportQueryException refused = await Assert.ThrowsAsync<ReportQueryException>(() =>
            Service(db, Guid.NewGuid()).CreateAsync(new CustomReportModel
            {
                DatasetKey = "dataset-sales-lines",
                Title = "Broken",
                Layout = new ReportQueryRequest { Columns = ["customer", "custom.nothing"] },
            }, false, SalesUser, CancellationToken.None));

        Assert.Contains("custom.nothing", refused.Message);
    }

    [Fact]
    public void Only_active_fields_become_columns_but_every_field_keeps_its_label()
    {
        (List<ReportColumn> columns, Dictionary<string, string> labels) =
            DatasetSourceRepository<SalesLineRow>.Build(Invoice(siteReferenceActive: false), r => r.CustomFields);

        ReportColumn salesperson = Assert.Single(columns);
        Assert.Equal("custom.salesperson", salesperson.Key);
        Assert.True(salesperson.IsGroupable);
        Assert.True(salesperson.IsCustomField);
        Assert.Equal(typeof(string), salesperson.ValueType);
        Assert.Equal("Site reference", labels["custom.site_reference"]);
    }

    [Fact]
    public async Task A_dataset_adds_its_kinds_custom_fields_after_its_seeded_columns()
    {
        SalesLinesDataset dataset = new(new StubDefinitions(Invoice()));
        int seeded = dataset.Columns.Count;

        await dataset.PrepareColumnsAsync(CancellationToken.None);

        Assert.Equal(
            ["custom.salesperson", "custom.site_reference"],
            dataset.Columns.Skip(seeded).Select(c => c.Key));
        Assert.True(dataset.IsDataset);
        Assert.Equal(ReportModule.Custom, dataset.Module);
    }

    [Fact]
    public async Task With_Master_unreachable_a_dataset_keeps_its_seeded_columns()
    {
        SalesLinesDataset dataset = new(new StubDefinitions(null));
        int seeded = dataset.Columns.Count;

        await dataset.PrepareColumnsAsync(CancellationToken.None);

        Assert.Equal(seeded, dataset.Columns.Count);
    }

    [Fact]
    public void A_custom_field_column_reads_the_stored_value_as_text_and_a_missing_one_as_null()
    {
        ReportColumn column = ReportColumn.CustomField<SalesLineRow>("site_reference", r => r.CustomFields);

        Assert.Equal("CHN-04", column.Read(Row("""{"site_reference":"CHN-04"}""")));
        Assert.Equal("12", ReportColumn.CustomField<SalesLineRow>("floors", r => r.CustomFields).Read(Row("""{"floors":12}""")));
        Assert.Null(column.Read(Row("""{"salesperson":"anita"}""")));
        Assert.Null(column.Read(new SalesLineRow { DocumentNo = "INV-1", DocumentType = "INV", CurrencyCode = "INR" }));

        // In SQL it is the jsonb key, which Npgsql translates to ->>.
        Assert.Contains("GetProperty(\"site_reference\")", column.Selector.Body.ToString());
    }

    [Fact]
    public void Pruning_takes_an_unknown_column_out_of_every_part_of_the_layout()
    {
        ReportQueryRequest layout = new()
        {
            Columns = ["customer", "custom.old", "lineTotalBase"],
            Filters = [new ReportFilterModel { Column = "custom.old", Operator = FilterOperator.Equals, Value = "x" }],
            Sorts = [new ReportSortModel { Column = "custom.old" }, new ReportSortModel { Column = "customer" }],
            GroupBy = ["custom.old"],
            Pivot = new ReportPivotModel
            {
                Rows = ["custom.old"],
                Values = [new ReportPivotValueModel { Column = "lineTotalBase" }],
            },
        };

        List<string> dropped = CustomReportRepository.Prune(layout, new HashSet<string> { "customer", "lineTotalBase" });

        Assert.Equal(["custom.old"], dropped);
        Assert.Equal(["customer", "lineTotalBase"], layout.Columns);
        Assert.Empty(layout.Filters);
        Assert.Equal("customer", Assert.Single(layout.Sorts).Column);
        Assert.Empty(layout.GroupBy);

        // A pivot left with no rows means nothing, so it goes rather than half-runs.
        Assert.Null(layout.Pivot);
    }

    [Fact]
    public void Custom_field_columns_are_not_expected_in_the_seeded_catalog()
    {
        // The branch's fields are never seed rows; the seeded dataset columns are the fixed ones.
        Assert.DoesNotContain(
            ReportCatalogSeeder.SeededColumnKeys["dataset-sales-lines"],
            key => key.StartsWith(ReportColumn.CustomKeyPrefix, StringComparison.Ordinal));
    }

    private static SalesLineRow Row(string json) =>
        new() { DocumentNo = "INV-1", DocumentType = "INV", CurrencyCode = "INR", CustomFields = JsonDocument.Parse(json) };

    private static List<CustomFieldDefinitionDto> Invoice(bool siteReferenceActive = true) =>
    [
        new()
        {
            CustomFieldDefinitionId = 1,
            EntityKind = CustomFieldEntityKind.Invoice,
            Key = "salesperson",
            Label = "Salesperson",
            DataType = CustomFieldDataType.Dropdown,
            Options = [new() { Value = "anita", Label = "Anita", Active = true }, new() { Value = "ravi", Label = "Ravi", Active = true }],
            DisplayOrder = 1,
        },
        new()
        {
            CustomFieldDefinitionId = 2,
            EntityKind = CustomFieldEntityKind.Invoice,
            Key = "site_reference",
            Label = "Site reference",
            DataType = CustomFieldDataType.Text,
            DisplayOrder = 2,
            IsActive = siteReferenceActive,
        },
    ];

    private sealed record Branch(Guid CustomerId, Guid OrgId);

    /// <summary>A fresh branch with the catalog seeded, so custom reports have datasets to name.</summary>
    private async Task<Branch> BranchAsync()
    {
        Branch branch = new(Guid.NewGuid(), Guid.NewGuid());
        await using ReportingDbContext db = _postgres.CreateContext(branch.CustomerId, branch.OrgId);
        await new ReportCatalogSeeder(db).SeedAsync(CancellationToken.None);
        return branch;
    }

    private static ReportCatalogRepository Catalog(ReportingDbContext db, IReadOnlyList<CustomFieldDefinitionDto>? invoice = null)
    {
        StubDefinitions definitions = new(invoice ?? Invoice());

        return new ReportCatalogRepository(
            [
                new SalesLinesDataset(definitions),
                new PurchaseLinesDataset(definitions),
                new LedgerRowsDataset(definitions),
                new StockMovementsDataset(definitions),
                new ItemsDataset(definitions),
                new ContactsDataset(definitions),
            ],
            db);
    }

    private static CustomReportRepository Service(ReportingDbContext db, Guid userId, IReadOnlyList<CustomFieldDefinitionDto>? invoice = null) =>
        new(db, new User(userId), Catalog(db, invoice));

    private sealed class User(Guid userId) : ICurrentUser
    {
        public Guid? UserId => userId;

        public Guid? CustomerId => null;

        public Guid? OrgId => null;

        public int? RoleId => null;
    }

    /// <summary>The same fields for every kind asked; null stands for Master not answering.</summary>
    private sealed class StubDefinitions(IReadOnlyList<CustomFieldDefinitionDto>? fields) : ICustomFieldDefinitions
    {
        public Task<IReadOnlyList<CustomFieldDefinitionDto>?> GetAsync(CustomFieldEntityKind kind, CancellationToken ct = default) =>
            Task.FromResult(fields);
    }
}
