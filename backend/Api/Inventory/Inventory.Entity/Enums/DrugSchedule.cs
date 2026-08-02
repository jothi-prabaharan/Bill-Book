namespace Inventory.Entity.Enums;

/// <summary>
/// The schedule a medicine falls under in the Drugs and Cosmetics Rules, 1945.
///
/// It decides whether a prescription is required, and H1 additionally requires
/// the sale to be recorded in a bound register kept for three years — which is
/// why the schedule is a property of the item rather than a note on it.
///
/// Stored as a string, so a schedule added later reads correctly in rows written
/// before it existed. The check constraint on the table refers to these names
/// directly, and <c>ItemService</c> derives <c>IsPrescriptionRequired</c> from
/// them, so the spellings are load-bearing rather than cosmetic.
/// </summary>
public enum DrugSchedule
{
    /// <summary>Not a scheduled drug — general goods, devices, most OTC lines.</summary>
    None = 0,

    /// <summary>Schedule G: carries a mandatory caution, but no prescription requirement.</summary>
    G = 1,

    /// <summary>Schedule H: prescription only.</summary>
    H = 2,

    /// <summary>
    /// Schedule H1: prescription only, and every sale goes in a separate
    /// register. Mostly antibiotics and habit-forming drugs.
    /// </summary>
    H1 = 3,

    /// <summary>
    /// Schedule X: prescription only, on a form the chemist retains for two
    /// years, and the stock is kept under lock. Narcotics and psychotropics.
    /// </summary>
    X = 4,
}
