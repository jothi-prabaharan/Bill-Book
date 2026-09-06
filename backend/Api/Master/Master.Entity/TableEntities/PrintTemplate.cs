using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Printing;
using Shared.Kernel.Tenancy;

namespace Master.Entity.TableEntities;

/// <summary>
/// One printable layout for one document type, belonging to one branch.
///
/// <b>Branch-scoped, like everything else in the tenant database.</b> OrgId is
/// the branch, and a branch is a complete set of books with its own items,
/// contacts, chart of accounts and numbering series — so it has its own
/// letterhead too. There is no customer-wide template that branches inherit:
/// the query filter requires CustomerId and OrgId together, so such a row would
/// be invisible to the very branches meant to read it.
///
/// Settings and Content are jsonb. They are strongly typed on both sides of the
/// column, and both live in Shared.Kernel because the renderer that consumes
/// them runs inside Sales, Purchase and Accounting rather than here.
/// </summary>
public class PrintTemplate : OrgScopedEntity
{
    public long PrintTemplateId { get; set; }

    /// <summary>
    /// The three-letter transaction type this layout prints — INV, POR, DLC.
    /// An unenforced reference to mst.TransactionType, which is in the other
    /// database, so it is validated in C# like every other cross-database id.
    /// </summary>
    [Required(ErrorMessage = "Document type is required.")]
    [StringLength(3, MinimumLength = 3, ErrorMessage = "Document type must be exactly 3 characters.")]
    [RegularExpression("^[A-Z]{3}$", ErrorMessage = "Document type must be three uppercase letters.")]
    public string DocumentTypeCode { get; set; } = null!;

    [Required(ErrorMessage = "Template name is required.")]
    [MaxLength(100, ErrorMessage = "Template name cannot exceed 100 characters.")]
    public string TemplateName { get; set; } = null!;

    /// <summary>
    /// The one that prints when a document names no template. At most one per
    /// branch per document type, enforced by a filtered unique index rather
    /// than by the code that sets it.
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>
    /// False once soft-deleted. Rows are never removed: documents printed
    /// against this template keep pointing at it, and a hard delete would make
    /// their history unreadable.
    /// </summary>
    public bool IsActive { get; set; } = true;

    public PrintSettings Settings { get; set; } = new();

    public PrintContent Content { get; set; } = new();

    /// <summary>
    /// The caller's optimistic-concurrency token, bumped on every save. Distinct
    /// from <see cref="Shared.Kernel.Entities.AuditableEntity.Version"/>, which
    /// is Postgres xmin and is EF's own token — this one is the integer the API
    /// hands out and takes back, and a mismatch is TEMPLATE_STALE.
    /// </summary>
    public int TemplateVersion { get; set; } = 1;

    /// <summary>
    /// Which generation of the platform's default layout this row was seeded
    /// from. Stamped by the generator so a later layout change can offer
    /// "reset to latest" without silently overwriting a customer's own edits.
    /// </summary>
    public int SeedVersion { get; set; }
}
