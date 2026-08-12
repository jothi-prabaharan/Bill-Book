namespace Shared.Kernel.Tax;

/// <summary>Why a place of supply was refused. Every value is something a user can act on.</summary>
public enum PlaceOfSupplyOutcome
{
    Ok = 0,

    /// <summary>The branch has no state code, so nothing can be decided against it.</summary>
    BranchStateUnknown = 1,

    /// <summary>No place of supply, and no GSTIN to fall back to.</summary>
    PlaceOfSupplyUnknown = 2,

    /// <summary>
    /// The GSTIN's first two digits name a different state from the place of
    /// supply. Refused rather than resolved: see <see cref="PlaceOfSupply"/>.
    /// </summary>
    GstinContradictsPlaceOfSupply = 3,

    /// <summary>The GSTIN is not fifteen characters, or does not start with two digits.</summary>
    GstinMalformed = 4,
}

/// <summary>The resolved place of supply, and whether the supply crosses a state line.</summary>
public sealed record PlaceOfSupplyResult(
    PlaceOfSupplyOutcome Outcome,
    string? StateCode = null,
    bool IsInterState = false,
    string? Detail = null)
{
    public bool IsOk => Outcome == PlaceOfSupplyOutcome.Ok;
}

/// <summary>
/// Where a supply is made, and therefore whether it carries CGST + SGST or IGST.
///
/// <b>This is decided once, on the document, and stored.</b> Re-deriving it on
/// read would restate a filed return the day a branch or a contact changes
/// address — see <c>DocumentHeaderBase.IsInterState</c>.
///
/// <b>A GSTIN that contradicts the place of supply is refused, not resolved.</b>
/// The two disagreeing means one of them is wrong, and there is no way from here
/// to know which. Picking either silently produces a document that balances,
/// prints and posts under the wrong head of tax — and the first anyone hears of
/// it is a mismatch on a return, by which time the money has been collected at
/// the wrong rate from a customer who has already claimed the credit.
/// </summary>
public static class PlaceOfSupply
{
    /// <summary>A GSTIN's first two digits are the state code it is registered in.</summary>
    public static string? StateCodeOfGstin(string? gstin)
    {
        if (gstin is not { Length: 15 })
        {
            return null;
        }

        return char.IsAsciiDigit(gstin[0]) && char.IsAsciiDigit(gstin[1])
            ? gstin[..2]
            : null;
    }

    /// <summary>
    /// Resolves the place of supply for a document.
    /// </summary>
    /// <param name="branchStateCode">The branch's own two-digit state code.</param>
    /// <param name="placeOfSupplyStateCode">
    /// What the document says, when it says anything. This wins when both are
    /// present and agree — and it is what an SEZ or an export supply sets to
    /// something other than the customer's registration.
    /// </param>
    /// <param name="contactGstin">
    /// The contact's GSTIN. Used as the fallback when no place of supply was
    /// given, and as the contradiction check when one was.
    /// </param>
    public static PlaceOfSupplyResult Resolve(
        string? branchStateCode,
        string? placeOfSupplyStateCode,
        string? contactGstin)
    {
        if (Normalize(branchStateCode) is not string branch)
        {
            return new PlaceOfSupplyResult(
                PlaceOfSupplyOutcome.BranchStateUnknown,
                Detail: "This branch has no state code, so GST cannot be determined. "
                    + "Set it under Settings › Organization.");
        }

        string? gstinState = null;

        if (!string.IsNullOrWhiteSpace(contactGstin))
        {
            gstinState = StateCodeOfGstin(contactGstin);

            if (gstinState is null)
            {
                return new PlaceOfSupplyResult(
                    PlaceOfSupplyOutcome.GstinMalformed,
                    Detail: "The GSTIN is not a 15-character registration beginning with a "
                        + "two-digit state code, so no state can be read from it.");
            }
        }

        string? stated = Normalize(placeOfSupplyStateCode);

        if (stated is null)
        {
            // No place of supply given: fall back to where the customer is
            // registered, which is right for the ordinary case and is why the
            // field can be left alone on most documents.
            if (gstinState is null)
            {
                return new PlaceOfSupplyResult(
                    PlaceOfSupplyOutcome.PlaceOfSupplyUnknown,
                    Detail: "Set a place of supply, or a GSTIN to take it from. Without one "
                        + "there is no way to tell an intra-state supply from an inter-state one.");
            }

            return Decide(branch, gstinState);
        }

        if (gstinState is not null && gstinState != stated)
        {
            return new PlaceOfSupplyResult(
                PlaceOfSupplyOutcome.GstinContradictsPlaceOfSupply,
                Detail: $"The GSTIN is registered in state {gstinState} but the place of supply "
                    + $"is {stated}. One of the two is wrong, and choosing between them here "
                    + "would file the tax under the wrong head. Correct whichever is mistaken.");
        }

        return Decide(branch, stated);
    }

    private static PlaceOfSupplyResult Decide(string branch, string supply) =>
        new(PlaceOfSupplyOutcome.Ok, supply, IsInterState: branch != supply);

    /// <summary>
    /// A two-digit state code, or null. Single digits are padded — a state code
    /// is written <c>09</c> on a GSTIN and is easily stored as <c>9</c>, and the
    /// two comparing unequal would make every supply in that state inter-state.
    /// </summary>
    private static string? Normalize(string? code)
    {
        string trimmed = (code ?? string.Empty).Trim();

        if (trimmed.Length == 0)
        {
            return null;
        }

        if (trimmed.Length == 1 && char.IsAsciiDigit(trimmed[0]))
        {
            return $"0{trimmed}";
        }

        return trimmed.Length == 2 && char.IsAsciiDigit(trimmed[0]) && char.IsAsciiDigit(trimmed[1])
            ? trimmed
            : null;
    }
}
