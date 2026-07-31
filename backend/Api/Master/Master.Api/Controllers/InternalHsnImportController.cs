using Master.Repository;
using Master.Repository.SeedData;
using Microsoft.AspNetCore.Mvc;

namespace Master.Api.Controllers;

/// <summary>
/// Imports the authoritative HSN/SAC list from a CSV.
///
/// The detailed codes are not seeded in the migration on purpose: there are
/// roughly thirteen thousand, they change with each CBIC notification, and an
/// incorrect code on a GST return is a filing error. Export the current list
/// from the GST portal or the CBIC tariff and import it here.
///
/// Operator-only; not routed through the public gateway.
/// </summary>
[ApiController]
[Route("internal/hsn-sac")]
public sealed class InternalHsnImportController : ControllerBase
{
    private readonly MasterDbContext _db;

    public InternalHsnImportController(MasterDbContext db) => _db = db;

    [HttpPost("import")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<IActionResult> Import(IFormFile file, CancellationToken ct)
    {
        if (file.Length == 0)
        {
            return BadRequest(new { message = "No file was uploaded." });
        }

        await using Stream stream = file.OpenReadStream();
        HsnImportResult result = await HsnSacCsvLoader.ImportAsync(_db, stream, ct);

        return Ok(new
        {
            result.Inserted,
            result.Updated,
            ErrorCount = result.Errors.Count,
            // Cap the echo — a malformed file can produce thousands of lines.
            Errors = result.Errors.Take(50),
        });
    }
}
