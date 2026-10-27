using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Entity.Models;
using Reporting.Repository.ReadModels;

namespace Reporting.Repository.Repositories;

public sealed class FixedAssetReconciliationReportRepository
{
    private readonly ReportingDbContext _db;

    public FixedAssetReconciliationReportRepository(ReportingDbContext db)
    {
        _db = db;
    }

    public static IEnumerable<(string Field, string Message)> Refusals(FixedAssetReconciliationReportRequest request)
    {
        if (request.From == null)
        {
            yield return (nameof(request.From), "From date is required.");
        }

        if (request.To == null)
        {
            yield return (nameof(request.To), "To date is required.");
        }

        if (request.From != null && request.To != null && request.To < request.From)
        {
            yield return (nameof(request.To), "To date cannot be earlier than From date.");
        }
    }

    public async Task<FixedAssetReconciliationReportResponse> RunAsync(
        FixedAssetReconciliationReportRequest request,
        CancellationToken ct = default)
    {
        DateOnly from = request.From ?? throw new ArgumentException("From date is required.");
        DateOnly to = request.To ?? throw new ArgumentException("To date is required.");

        var registerRows = FixedAssetRegisterRepository.Rows(_db, from, to);
        var rowsQuery = FixedAssetReconciliationSourceRepository.Rows(
            registerRows,
            _db.FixedAssetCategories,
            _db.Accounts,
            _db.Ledger,
            from,
            to);

        var rows = await rowsQuery.ToListAsync(ct);

        var accumDepAccountIds = await _db.FixedAssetCategories
            .Select(c => c.AccumulatedDepreciationAccountId)
            .Distinct()
            .ToListAsync(ct);

        var accumDepSet = new HashSet<long>(accumDepAccountIds);

        return Compose(rows, accumDepSet, request, from, to);
    }

    public static FixedAssetReconciliationReportResponse Compose(
        IEnumerable<FixedAssetReconciliationRow> rows,
        ISet<long>? accumDepAccountIds,
        FixedAssetReconciliationReportRequest request,
        DateOnly from,
        DateOnly to)
    {
        accumDepAccountIds ??= new HashSet<long>();

        var accountGroups = rows
            .GroupBy(r => new { r.AccountId, r.AccountCode, r.Account })
            .OrderBy(g => g.Key.AccountCode)
            .Select(g =>
            {
                var reg = g.FirstOrDefault(r => r.Source == nameof(ReconciliationSide.Register)) ?? new FixedAssetReconciliationRow();
                var led = g.FirstOrDefault(r => r.Source == nameof(ReconciliationSide.Ledger)) ?? new FixedAssetReconciliationRow();

                bool isAccumDep = accumDepAccountIds.Contains(g.Key.AccountId) || (reg.ClosingAccumDep > 0 && reg.ClosingCost == 0);

                var regDto = new ReconciliationSideBalanceDto
                {
                    OpeningCost = reg.OpeningCost,
                    CostDebits = reg.CostDebits,
                    CostCredits = reg.CostCredits,
                    ClosingCost = reg.ClosingCost,
                    OpeningAccumDep = reg.OpeningAccumDep,
                    AccumDepDebits = reg.AccumDepDebits,
                    AccumDepCredits = reg.AccumDepCredits,
                    ClosingAccumDep = reg.ClosingAccumDep,
                    OpeningBookValue = reg.OpeningBookValue,
                    ClosingBookValue = reg.ClosingBookValue,
                    ClosingBalance = isAccumDep ? reg.ClosingAccumDep : reg.ClosingCost,
                };

                var ledDto = new ReconciliationSideBalanceDto
                {
                    OpeningCost = led.OpeningCost,
                    CostDebits = led.CostDebits,
                    CostCredits = led.CostCredits,
                    ClosingCost = led.ClosingCost,
                    OpeningAccumDep = led.OpeningAccumDep,
                    AccumDepDebits = led.AccumDepDebits,
                    AccumDepCredits = led.AccumDepCredits,
                    ClosingAccumDep = led.ClosingAccumDep,
                    OpeningBookValue = led.OpeningBookValue,
                    ClosingBookValue = led.ClosingBookValue,
                    ClosingBalance = isAccumDep ? led.ClosingAccumDep : led.ClosingCost,
                };

                decimal diff = regDto.ClosingBalance - ledDto.ClosingBalance;
                bool isReconciled = diff == 0m;

                string explanation;
                if (isReconciled)
                {
                    explanation = "Register matches ledger";
                }
                else if (diff < 0)
                {
                    explanation = $"Register is {Math.Abs(diff):N2} below the ledger";
                }
                else
                {
                    explanation = $"Register is {diff:N2} above the ledger";
                }

                return new FixedAssetReconciliationAccountDto
                {
                    AccountId = g.Key.AccountId,
                    AccountCode = g.Key.AccountCode,
                    AccountName = g.Key.Account,
                    AccountType = isAccumDep ? "AccumulatedDepreciation" : "Asset",
                    Register = regDto,
                    Ledger = ledDto,
                    ClosingDifference = diff,
                    IsReconciled = isReconciled,
                    DifferenceExplanation = explanation,
                };
            }).ToList();

        int totalAccounts = accountGroups.Count;
        int reconciledCount = accountGroups.Count(a => a.IsReconciled);
        int discrepantCount = totalAccounts - reconciledCount;

        decimal registerNbv = accountGroups.Sum(a => a.Register.ClosingBookValue);
        decimal ledgerNbv = accountGroups.Sum(a => a.Ledger.ClosingBookValue);
        decimal totalNbvDiff = registerNbv - ledgerNbv;

        string totalExplanation;
        if (totalNbvDiff == 0m)
        {
            totalExplanation = "Register net book value matches the general ledger";
        }
        else if (totalNbvDiff < 0)
        {
            totalExplanation = $"Register net book value is {Math.Abs(totalNbvDiff):N2} below the general ledger";
        }
        else
        {
            totalExplanation = $"Register net book value is {totalNbvDiff:N2} above the general ledger";
        }

        return new FixedAssetReconciliationReportResponse
        {
            FromDate = from,
            ToDate = to,
            Currency = "INR",
            Accounts = accountGroups,
            Summary = new FixedAssetReconciliationSummaryDto
            {
                TotalAccounts = totalAccounts,
                ReconciledAccounts = reconciledCount,
                DiscrepantAccounts = discrepantCount,
                RegisterTotalNbv = registerNbv,
                LedgerTotalNbv = ledgerNbv,
                TotalNbvDifference = totalNbvDiff,
                Explanation = totalExplanation,
            },
        };
    }
}
