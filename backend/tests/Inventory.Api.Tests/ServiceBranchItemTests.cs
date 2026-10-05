using Inventory.Entity.Enums;
using Inventory.Entity.Models;
using Inventory.Repository.Repositories;
using Shared.Kernel.Apps;
using Xunit;

namespace Inventory.Api.Tests;

/// <summary>
/// A Service branch holds no stock (TK-136), so every item it keeps is a
/// service that tracks no inventory. Every other trade is unaffected.
/// </summary>
public sealed class ServiceBranchItemTests
{
    [Fact]
    public void A_service_branch_keeps_untracked_services()
    {
        Assert.Null(ItemRepository.TradeRefusal(BusinessType.Service, ItemType.Service, trackInventory: false));
    }

    [Theory]
    [InlineData(ItemType.Goods, false)]
    [InlineData(ItemType.Goods, true)]
    [InlineData(ItemType.Service, true)]
    public void A_service_branch_refuses_anything_stocked(ItemType type, bool trackInventory)
    {
        Assert.Equal(
            SaveItemOutcome.StockedItemInServiceBranch,
            ItemRepository.TradeRefusal(BusinessType.Service, type, trackInventory));
    }

    [Theory]
    [InlineData(BusinessType.Retail)]
    [InlineData(BusinessType.Pharma)]
    [InlineData(BusinessType.Jewellery)]
    [InlineData(BusinessType.Restaurant)]
    [InlineData(null)]
    public void Other_trades_keep_stocked_goods(BusinessType? trade)
    {
        Assert.Null(ItemRepository.TradeRefusal(trade, ItemType.Goods, trackInventory: true));
    }
}
