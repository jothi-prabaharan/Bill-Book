using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SavingsScheme.Entity.TableEntities;
using SavingsScheme.Entity.Enums;
using SavingsScheme.Repository;
using Shared.Kernel.Tenancy;
using Shared.Kernel.Apps;
using Shared.Kernel.Internal;

namespace SavingsScheme.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[RequireAddOn(AddOn.SavingsScheme)]
public class SchemeDefinitionsController : ControllerBase
{
    private readonly SavingsSchemeDbContext _db;

    public SchemeDefinitionsController(SavingsSchemeDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var list = await _db.SchemeDefinitions.OrderBy(d => d.Name).ToListAsync();
        return Ok(list);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SchemeDefinition request)
    {
        _db.SchemeDefinitions.Add(request);
        await _db.SaveChangesAsync();
        return Ok(request);
    }

    [HttpPost("{id}/lapse")]
    public async Task<IActionResult> Lapse(long id)
    {
        var enrolment = await _db.SchemeEnrolments.FindAsync(id);
        if (enrolment == null) return NotFound();
        if (enrolment.Status != EnrolmentStatus.Active) return BadRequest("Scheme is not active.");

        enrolment.Status = EnrolmentStatus.Lapsed;
        enrolment.LapsedDate = DateOnly.FromDateTime(DateTime.UtcNow);
        enrolment.BonusAccrued = 0m; // No bonus for lapsed schemes

        await _db.SaveChangesAsync();
        return Ok(enrolment);
    }

    [HttpPost("{id}/redeem")]
    public async Task<IActionResult> Redeem(long id, [FromBody] decimal amountToRedeem)
    {
        var enrolment = await _db.SchemeEnrolments.FindAsync(id);
        if (enrolment == null) return NotFound();
        if (enrolment.Status == EnrolmentStatus.Redeemed) return BadRequest("Scheme already redeemed.");

        decimal available = enrolment.TotalPrincipalPaid;
        if (enrolment.Status != EnrolmentStatus.Lapsed)
        {
            available += enrolment.BonusAccrued;
        }

        if (amountToRedeem > available)
        {
            return BadRequest("Amount exceeds available balance.");
        }

        enrolment.Status = EnrolmentStatus.Redeemed;
        enrolment.RedeemedDate = DateOnly.FromDateTime(DateTime.UtcNow);

        await _db.SaveChangesAsync();
        return Ok(enrolment);
    }
}

[ApiController]
[Route("api/[controller]")]
[RequireAddOn(AddOn.SavingsScheme)]
public class SchemeEnrolmentsController : ControllerBase
{
    private readonly SavingsSchemeDbContext _db;

    public SchemeEnrolmentsController(SavingsSchemeDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var list = await _db.SchemeEnrolments.OrderBy(e => e.StartDate).ToListAsync();
        return Ok(list);
    }

    [HttpPost]
    public async Task<IActionResult> Enrol([FromBody] SchemeEnrolment request)
    {
        var definition = await _db.SchemeDefinitions.FindAsync(request.SchemeDefinitionId);
        if (definition == null) return BadRequest("Invalid scheme.");

        request.Status = EnrolmentStatus.Active;
        _db.SchemeEnrolments.Add(request);

        // Generate instalments
        for (int i = 0; i < definition.DurationMonths; i++)
        {
            _db.SchemeInstalments.Add(new SchemeInstalment
            {
                SchemeEnrolment = request,
                AmountDue = request.InstalmentAmount,
                DueDate = request.StartDate.AddMonths(i)
            });
        }

        await _db.SaveChangesAsync();
        return Ok(request);
    }

    [HttpPost("{id}/lapse")]
    public async Task<IActionResult> Lapse(long id)
    {
        var enrolment = await _db.SchemeEnrolments.FindAsync(id);
        if (enrolment == null) return NotFound();
        if (enrolment.Status != EnrolmentStatus.Active) return BadRequest("Scheme is not active.");

        enrolment.Status = EnrolmentStatus.Lapsed;
        enrolment.LapsedDate = DateOnly.FromDateTime(DateTime.UtcNow);
        enrolment.BonusAccrued = 0m; // No bonus for lapsed schemes

        await _db.SaveChangesAsync();
        return Ok(enrolment);
    }

    [HttpPost("{id}/redeem")]
    public async Task<IActionResult> Redeem(long id, [FromBody] decimal amountToRedeem)
    {
        var enrolment = await _db.SchemeEnrolments.FindAsync(id);
        if (enrolment == null) return NotFound();
        if (enrolment.Status == EnrolmentStatus.Redeemed) return BadRequest("Scheme already redeemed.");

        decimal available = enrolment.TotalPrincipalPaid;
        if (enrolment.Status != EnrolmentStatus.Lapsed)
        {
            available += enrolment.BonusAccrued;
        }

        if (amountToRedeem > available)
        {
            return BadRequest("Amount exceeds available balance.");
        }

        enrolment.Status = EnrolmentStatus.Redeemed;
        enrolment.RedeemedDate = DateOnly.FromDateTime(DateTime.UtcNow);

        await _db.SaveChangesAsync();
        return Ok(enrolment);
    }
}



