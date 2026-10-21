using Microsoft.EntityFrameworkCore;
using Reporting.Entity.Enums;
using Reporting.Entity.Models;

namespace Reporting.Repository.Repositories;

/// <summary>
/// Business Performance (TK-203): the D-15 ratios for a period, and for a
/// comparison period when asked, grouped as profitability, efficiency,
/// financial position and cash, each with the figures it divides and the
/// change between the two periods.
///
/// <b>The arithmetic is the catalog source's</b>,
/// <see cref="BusinessPerformanceSourceRepository.Rows"/>: one pass over the
/// ledger per period, already tested ratio by ratio, so the page and the
/// catalog's report cannot disagree. This repository runs it once per period
/// and shapes the answer. Each ratio is listed even when the books cannot give
/// it (nothing posted, or nothing to divide by), with no value, so the page
/// always reads the same way.
/// </summary>
public sealed class BusinessPerformanceReportRepository
{
    private readonly ReportingDbContext _db;

    public BusinessPerformanceReportRepository(ReportingDbContext db) => _db = db;

    /// <summary>
    /// Every reason the request's dates cannot be run, by field, empty when
    /// there is none. A comparison period needs both dates or neither. Pure.
    /// </summary>
    public static IReadOnlyList<(string Field, string Message)> DateRefusals(BusinessPerformanceReportRequest request)
    {
        List<(string, string)> refusals = [];

        if (request.FromDate is DateOnly from && request.ToDate is DateOnly to && to < from)
        {
            refusals.Add((nameof(BusinessPerformanceReportRequest.ToDate), "To date cannot be before the from date."));
        }

        if (request.CompareFromDate is null && request.CompareToDate is not null)
        {
            refusals.Add((nameof(BusinessPerformanceReportRequest.CompareFromDate), "Give both comparison dates, or neither."));
        }
        else if (request.CompareFromDate is not null && request.CompareToDate is null)
        {
            refusals.Add((nameof(BusinessPerformanceReportRequest.CompareToDate), "Give both comparison dates, or neither."));
        }
        else if (request.CompareFromDate is DateOnly compareFrom && request.CompareToDate is DateOnly compareTo
            && compareTo < compareFrom)
        {
            refusals.Add((nameof(BusinessPerformanceReportRequest.CompareToDate), "Comparison to date cannot be before its from date."));
        }

        return refusals;
    }

    /// <summary>The ratios for a request the controller has already validated.</summary>
    public async Task<BusinessPerformanceReportResultDto> RunAsync(
        BusinessPerformanceReportRequest request, CancellationToken ct)
    {
        DateOnly from = request.FromDate!.Value;
        DateOnly to = request.ToDate!.Value;

        List<BusinessPerformanceRow> rows = await BusinessPerformanceSourceRepository
            .Rows(_db.Accounts, _db.Ledger, _db.FixedAssetCategories, from, to)
            .ToListAsync(ct);

        List<BusinessPerformanceRow>? compareRows = request.CompareFromDate is DateOnly compareFrom
            && request.CompareToDate is DateOnly compareTo
                ? await BusinessPerformanceSourceRepository
                    .Rows(_db.Accounts, _db.Ledger, _db.FixedAssetCategories, compareFrom, compareTo)
                    .ToListAsync(ct)
                : null;

        return Compose(rows, compareRows, from, to, request.CompareFromDate, request.CompareToDate);
    }

    /// <summary>
    /// The answer from each period's rows. Every ratio in its defined order,
    /// named and explained as the catalog source names and explains it; the
    /// change only where both periods have a value. Pure.
    /// </summary>
    public static BusinessPerformanceReportResultDto Compose(
        IReadOnlyList<BusinessPerformanceRow> rows,
        IReadOnlyList<BusinessPerformanceRow>? compareRows,
        DateOnly from,
        DateOnly to,
        DateOnly? compareFrom,
        DateOnly? compareTo)
    {
        bool comparing = compareRows is not null && compareFrom.HasValue && compareTo.HasValue;

        List<BusinessPerformanceRatioDto> ratios =
        [
            .. Enum.GetValues<BusinessPerformanceMetric>()
                .OrderBy(m => (int)m)
                .Select(metric =>
                {
                    BusinessPerformanceRow? row = rows.FirstOrDefault(r => r.MetricOrder == (int)metric);
                    BusinessPerformanceRow? compare = comparing
                        ? compareRows!.FirstOrDefault(r => r.MetricOrder == (int)metric)
                        : null;
                    (string title, BusinessPerformanceUnit unit, string calculation) =
                        BusinessPerformanceSourceRepository.Describe(metric);

                    return new BusinessPerformanceRatioDto
                    {
                        Metric = metric,
                        Group = GroupOf(metric),
                        Title = title,
                        Unit = unit,
                        Calculation = calculation,
                        Value = row?.Value,
                        Numerator = row?.Numerator,
                        Denominator = row?.Denominator,
                        CompareValue = compare?.Value,
                        CompareNumerator = compare?.Numerator,
                        CompareDenominator = compare?.Denominator,
                        Change = row?.Value is decimal value && compare?.Value is decimal before ? value - before : null,
                    };
                }),
        ];

        return new BusinessPerformanceReportResultDto
        {
            FromDate = from,
            ToDate = to,
            Days = DaysIn(from, to),
            CompareFromDate = comparing ? compareFrom : null,
            CompareToDate = comparing ? compareTo : null,
            CompareDays = comparing ? DaysIn(compareFrom!.Value, compareTo!.Value) : null,
            Ratios = ratios,
        };
    }

    /// <summary>The heading a ratio is shown under. Pure.</summary>
    public static BusinessPerformanceGroup GroupOf(BusinessPerformanceMetric metric) => metric switch
    {
        BusinessPerformanceMetric.GrossProfitMargin
            or BusinessPerformanceMetric.NetProfitMargin
            or BusinessPerformanceMetric.ReturnOnInvestment => BusinessPerformanceGroup.Profitability,
        BusinessPerformanceMetric.AverageDaysCustomersTakeToPay
            or BusinessPerformanceMetric.AverageDaysToPaySuppliers => BusinessPerformanceGroup.Efficiency,
        BusinessPerformanceMetric.CurrentAssetsToLiabilities
            or BusinessPerformanceMetric.TermAssetsToLiabilities => BusinessPerformanceGroup.FinancialPosition,
        _ => BusinessPerformanceGroup.Cash,
    };

    /// <summary>Days in a period, both ends counted, as the ratios count them. Pure.</summary>
    public static int DaysIn(DateOnly from, DateOnly to) => to.DayNumber - from.DayNumber + 1;
}
