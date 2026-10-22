using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Reporting.Entity.Enums;
using Reporting.Repository;
using Reporting.Repository.ReadModels;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Reporting.Api.Tests;

/// <summary>
/// TK-213: the owning services store these enums by name, so each read model
/// property over one of those columns must be its enum read as a string. Read as
/// an int, a comparison failed in SQL ("character varying = integer") and a
/// projection failed on the first row. Built from the model alone: no database.
/// </summary>
public sealed class ReadModelEnumMappingTests
{
    private static IModel Model()
    {
        var options = new DbContextOptionsBuilder<ReportingDbContext>()
            .UseNpgsql("Host=localhost;Database=model-only")
            .Options;
        using var db = new ReportingDbContext(options, new TenantContext { CustomerId = Guid.NewGuid(), OrgId = Guid.NewGuid() });
        return db.Model;
    }

    public static TheoryData<Type, string, Type, int> Columns => new()
    {
        { typeof(StockMovementRead), nameof(StockMovementRead.MovementType), typeof(StockMovementType), 15 },
        { typeof(StockMovementRead), nameof(StockMovementRead.Direction), typeof(StockDirection), 3 },
        { typeof(StockMovementRead), nameof(StockMovementRead.CostingStatus), typeof(CostingStatus), 12 },
        { typeof(StockMovementRead), nameof(StockMovementRead.LedgerStatus), typeof(LedgerStatus), 14 },
        { typeof(ItemRead), nameof(ItemRead.ItemProfile), typeof(ItemProfile), 15 },
        { typeof(ItemRead), nameof(ItemRead.ItemType), typeof(ItemType), 10 },
        { typeof(ItemRead), nameof(ItemRead.TaxPreference), typeof(TaxPreference), 15 },
        { typeof(ItemRead), nameof(ItemRead.CostingType), typeof(CostingType), 25 },
        { typeof(ItemCategoryRead), nameof(ItemCategoryRead.DefaultItemProfile), typeof(ItemProfile?), 15 },
        { typeof(ItemCategoryRead), nameof(ItemCategoryRead.DefaultCostingType), typeof(CostingType?), 20 },
        { typeof(ItemSerialRead), nameof(ItemSerialRead.Status), typeof(SerialStatus), 10 },
        { typeof(WarehouseRead), nameof(WarehouseRead.WarehouseType), typeof(WarehouseType), 15 },
        { typeof(WarehouseRead), nameof(WarehouseRead.StorageType), typeof(StorageCondition), 15 },
        { typeof(SubAccountRead), nameof(SubAccountRead.ReferenceType), typeof(SubAccountReferenceType), 20 },
        { typeof(SubAccountRead), nameof(SubAccountRead.Purpose), typeof(SubAccountPurpose), 20 },
        { typeof(JournalRead), nameof(JournalRead.Status), typeof(JournalStatus), 10 },
        { typeof(SalesOrderRead), nameof(SalesOrderRead.FulfilmentStatus), typeof(FulfilmentStatus), 16 },
        { typeof(DeliveryChallanRead), nameof(DeliveryChallanRead.ChallanType), typeof(ChallanType), 16 },
        { typeof(PurchaseOrderRead), nameof(PurchaseOrderRead.FulfilmentStatus), typeof(PurchaseFulfilmentStatus), 16 },
        { typeof(ReceiveMoneyRead), nameof(ReceiveMoneyRead.PaymentMethod), typeof(PaymentMethod), 20 },
        { typeof(SpendMoneyRead), nameof(SpendMoneyRead.PaymentMethod), typeof(PaymentMethod), 20 },
        { typeof(ContactLicenceRead), nameof(ContactLicenceRead.LicenceType), typeof(LicenceType), 20 },
    };

    [Theory]
    [MemberData(nameof(Columns))]
    public void Each_enum_column_is_its_enum_read_as_its_name(Type readModel, string property, Type enumType, int maxLength)
    {
        IProperty column = Model().FindEntityType(readModel)!.FindProperty(property)!;

        Assert.Equal(enumType, column.ClrType);
        Assert.Equal(typeof(string), column.GetTypeMapping().Converter?.ProviderClrType);
        Assert.Equal(maxLength, column.GetMaxLength());
    }

    [Fact]
    public void No_read_model_property_is_a_bare_number_named_like_an_enum()
    {
        string[] enumLike = ["Status", "Type", "Direction", "Purpose", "Method", "Profile", "Preference"];

        var offenders = Model().GetEntityTypes()
            .Where(e => e.ClrType.Namespace == typeof(StockMovementRead).Namespace)
            .SelectMany(e => e.GetProperties().Select(p => (Entity: e.ClrType.Name, Property: p)))
            .Where(x => x.Property.ClrType == typeof(int) || x.Property.ClrType == typeof(int?))
            .Where(x => enumLike.Any(word => x.Property.Name.EndsWith(word, StringComparison.Ordinal)))
            // Site stores its enums as numbers, so its read models' numbers are right.
            .Where(x => !x.Entity.StartsWith("Site", StringComparison.Ordinal))
            .Select(x => $"{x.Entity}.{x.Property.Name}")
            .ToList();

        Assert.Empty(offenders);
    }

    [Theory]
    [InlineData("Inventory", ItemType.Inventory)]
    [InlineData("Goods", ItemType.Goods)]
    public void Both_names_inventory_gives_its_stock_items_read_as_the_same_type(string stored, ItemType expected) =>
        Assert.Equal(expected, Enum.Parse<ItemType>(stored));

    [Fact]
    public void Settlements_are_mapped_read_only_from_accounting()
    {
        IEntityType ratios = Model().FindEntityType(typeof(TransactionRatioRead))!;

        Assert.Equal("TransactionRatios", ratios.GetTableName());
        Assert.Equal("acc", ratios.GetSchema());
        Assert.True(ratios.IsTableExcludedFromMigrations());
        Assert.NotEmpty(ratios.GetDeclaredQueryFilters());
    }
}
