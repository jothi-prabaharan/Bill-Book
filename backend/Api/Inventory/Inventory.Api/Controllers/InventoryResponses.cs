using Inventory.Entity.Models;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.Api.Controllers;

public class MessageResponse
{
    public string Message { get; set; } = null!;
}

/// <summary>
/// One place that turns an outcome into a response. Shared because every master
/// in this service fails in the same handful of ways, and a message written once
/// cannot drift between controllers.
/// </summary>
public static class InventoryResponses
{
    public static IActionResult From(
        ControllerBase controller, SaveMasterOutcome outcome, Func<IActionResult> onOk) =>
        outcome switch
        {
            SaveMasterOutcome.Ok => onOk(),
            SaveMasterOutcome.NotFound => controller.NotFound(),
            SaveMasterOutcome.DuplicateCode => Bad(controller, "That code is already in use."),
            SaveMasterOutcome.DuplicateName => Bad(controller, "That name is already in use."),
            SaveMasterOutcome.InUse => Bad(
                controller,
                "This is already in use, so it cannot be removed or repriced. Make it inactive instead."),
            SaveMasterOutcome.SystemRowUndeletable => Bad(
                controller, "A built-in row cannot be deleted. Rename it, or make it inactive."),
            SaveMasterOutcome.CategoryCycle => Bad(
                controller, "A category cannot be moved inside one of its own subcategories."),
            SaveMasterOutcome.CategoryTooDeep => Bad(
                controller,
                "Categories go three levels deep. Deeper than that and reports stop agreeing "
                    + "on which level they group by."),
            SaveMasterOutcome.HasChildren => Bad(
                controller, "Move or remove the subcategories first."),
            SaveMasterOutcome.DefaultWarehouseLocked => Bad(
                controller,
                "This is the default warehouse. Make another one the default before deactivating it."),
            SaveMasterOutcome.InvalidValue => Bad(
                controller, "One of the selected options is not a recognised value."),
            _ => controller.StatusCode(StatusCodes.Status500InternalServerError),
        };

    public static IActionResult From(
        ControllerBase controller, SaveUomOutcome outcome, Func<IActionResult> onOk) =>
        outcome switch
        {
            SaveUomOutcome.Ok => onOk(),
            SaveUomOutcome.NotFound => controller.NotFound(),
            SaveUomOutcome.DuplicateName => Bad(controller, "That name is already in use."),
            SaveUomOutcome.DuplicateCode => Bad(controller, "That unit code is already in use."),
            SaveUomOutcome.TypeInUse => Bad(
                controller,
                "Items are using units of this type. Removing or deactivating it would take those "
                    + "units out of every picker while the items keep trading in them."),
            SaveUomOutcome.UnitInUse => Bad(
                controller,
                "Items already hold stock in this unit, so its type and conversion factor are "
                    + "fixed — changing them would restate quantities already recorded."),
            SaveUomOutcome.SystemRowUndeletable => Bad(
                controller, "A built-in row cannot be deleted. Rename it, or make it inactive."),
            SaveUomOutcome.BaseUnitRequired => Bad(
                controller,
                "A type with units needs exactly one base unit. Make another unit the base first."),
            SaveUomOutcome.BaseUnitFactorFixed => Bad(
                controller, "The base unit defines the scale, so its own factor is always 1."),
            SaveUomOutcome.BaseChangeBlocked => Bad(
                controller,
                "Items already use units of this type. Moving the base would rescale every factor "
                    + "in it and restate the quantities recorded under them."),
            SaveUomOutcome.InvalidValue => Bad(
                controller, "One of the selected options is not a recognised value."),
            _ => controller.StatusCode(StatusCodes.Status500InternalServerError),
        };

    public static IActionResult From(
        ControllerBase controller, SaveItemOutcome outcome, Func<IActionResult> onOk) =>
        outcome switch
        {
            SaveItemOutcome.Ok => onOk(),
            SaveItemOutcome.NotFound => controller.NotFound(),
            SaveItemOutcome.DuplicateCode => Bad(controller, "Another item already uses that code."),
            SaveItemOutcome.DuplicateBarcode => Bad(
                controller,
                "That barcode is already on another item. One scan has to resolve to one item."),
            SaveItemOutcome.UnitTypeMismatch => Bad(
                controller,
                "The inventory, sales, purchase and report units must all belong to the item's "
                    + "unit type — that is what makes them convertible."),
            SaveItemOutcome.UnknownUnit => Bad(controller, "One of the selected units no longer exists."),
            SaveItemOutcome.UnknownCategory => Bad(controller, "That category no longer exists."),
            SaveItemOutcome.ProfileDetailsMissing => Bad(
                controller, "Fill in the details for the selected item profile."),
            SaveItemOutcome.ProfileDetailsUnexpected => Bad(
                controller, "Those details do not belong to the selected item profile."),
            SaveItemOutcome.LockedAfterMovement => Bad(
                controller,
                "Stock has already moved for this item, so its unit type, inventory unit, costing "
                    + "method and tracking options are fixed. Every recorded quantity and cost was "
                    + "written under them."),
            SaveItemOutcome.CostingTrackingMismatch => Bad(
                controller,
                "That costing method needs matching tracking: FEFO needs batch and expiry, and "
                    + "specific identification needs serial numbers."),
            SaveItemOutcome.ServiceCannotBeStocked => Bad(
                controller, "A service item cannot track inventory."),
            SaveItemOutcome.InvalidValue => Bad(
                controller, "One of the selected options is not a recognised value."),
            _ => controller.StatusCode(StatusCodes.Status500InternalServerError),
        };

    private static IActionResult Bad(ControllerBase controller, string message) =>
        controller.BadRequest(new MessageResponse { Message = message });
}
