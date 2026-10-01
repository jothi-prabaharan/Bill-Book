using Inventory.Entity.TableEntities;
using Inventory.Repository.Repositories;
using Shared.Kernel.Restaurant;
using Shared.TestKernel;

namespace Inventory.Api.Tests.Repositories;

public class MenuRepositoryTests : DbTestBase<InventoryTestDbContext>
{
    private readonly MenuRepository _repo;

    public MenuRepositoryTests()
    {
        _repo = new MenuRepository(Db);
    }

    [Fact]
    public async Task GetMenu_FiltersByAvailability_And_Schedule()
    {
        // Arrange
        var item = new MenuItem
        {
            MenuItemId = 1,
            IsAvailable = true,
            Station = MenuStation.Kitchen
        };
        
        var price = new MenuItemPrice
        {
            MenuItemId = 1,
            OrderType = OrderType.DineIn,
            Price = 100
        };

        Db.MenuItems.Add(item);
        Db.MenuItemPrices.Add(price);
        await Db.SaveChangesAsync();

        // Act
        var result = await _repo.GetMenuAsync(OrderType.DineIn, new TimeSpan(12, 0, 0));

        // Assert
        Assert.Single(result);
        Assert.Equal(1, result[0].MenuItemId);
    }
}
