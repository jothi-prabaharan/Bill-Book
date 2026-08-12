using Accounting.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Kernel.Internal;
using Shared.Kernel.Tax;

namespace Accounting.Api.Controllers;

/// <summary>
/// The GST rates in force on a date, for Sales and Purchase.
///
/// <b>Accounting owns the rates and nobody else reads the table.</b> Tax Master
/// is a Settings screen, but the data belongs here, and a service that queried
/// <c>acc.TaxMasters</c> directly would be crossing a service boundary to reach
/// a table whose effective-dating it would then have to reimplement.
///
/// <b>The date is required and there is no "current" route.</b> A caller that
/// could omit it would eventually omit it, and a backdated document taxed at
/// today's rates is a return that has to be amended. Making the date the only
/// way in means the mistake cannot be made quietly.
/// </summary>
[ApiController]
[AllowAnonymous]
[InternalOnly]
[Route("internal/tax")]
public sealed class InternalTaxController : ControllerBase
{
    private readonly TaxMasterService _taxes;

    public InternalTaxController(TaxMasterService taxes) => _taxes = taxes;

    /// <summary>
    /// Every active rate in force on <paramref name="on"/>, one per tax group.
    ///
    /// Both splits come back on every row — CGST/SGST and IGST — and the caller
    /// picks. Deciding here which applies would put the intra/inter rule in two
    /// services, and the copy that drifted would be the one nobody read.
    /// </summary>
    [HttpGet("rates")]
    public async Task<IActionResult> Rates([FromQuery] DateOnly on, CancellationToken ct)
    {
        if (on == default)
        {
            return BadRequest(new MessageResponse
            {
                Message = "A date is required. A document is taxed at the rate in force when it "
                    + "was raised, so there is no such thing as 'the current rate' here.",
            });
        }

        IReadOnlyList<Entity.Models.TaxMasterListItem> rows =
            await _taxes.ListAsync(includeHistory: true, includeInactive: false, ct);

        // In force on the date, newest first per group, and one row per group —
        // the same rule TaxMasterService.ResolveAsync applies to a single group,
        // applied across all of them in one read rather than one call per line.
        List<TaxRate> rates = [.. rows
            .Where(r => r.EffectiveFrom <= on && (r.EffectiveTo is null || r.EffectiveTo >= on))
            .GroupBy(r => r.TaxGroupId)
            .Select(g => g.OrderByDescending(r => r.EffectiveFrom).First())
            .Select(r => new TaxRate(
                r.TaxMasterId,
                r.TaxGroupId,
                r.TaxSystemName,
                r.TotalRate,
                r.CgstRate,
                r.SgstRate,
                r.IgstRate,
                r.CessRate))];

        return Ok(rates);
    }
}
