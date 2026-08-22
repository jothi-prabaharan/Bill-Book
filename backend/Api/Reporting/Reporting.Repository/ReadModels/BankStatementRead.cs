#pragma warning disable CS8618
using Shared.Kernel.Tenancy;

namespace Reporting.Repository.ReadModels;

/// <summary>
/// <c>acc.BankStatements</c>, read-only. One imported statement — the header the
/// Reconciliation report groups its lines under.
/// </summary>
public class BankStatementRead : OrgScopedEntity
{
    public long BankStatementId { get; set; }

    public long BankAccountId { get; set; }

    public string? StatementReference { get; set; }

    public DateOnly FromDate { get; set; }

    public DateOnly ToDate { get; set; }

    public decimal? OpeningBalance { get; set; }

    public decimal? ClosingBalance { get; set; }

    public string? FileName { get; set; }

    public DateTimeOffset ImportedAt { get; set; }
}


