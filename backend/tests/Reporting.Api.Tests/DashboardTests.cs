using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Reporting.Api.Controllers;
using Reporting.Entity.Models;
using Reporting.Repository;
using Reporting.Repository.ReadModels;
using Reporting.Repository.Repositories;
using Shared.Kernel.Documents;
using Shared.Kernel.Numbering;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Reporting.Api.Tests;

[Collection(nameof(PostgresCollection))]
public sealed class DashboardTests
{
    private readonly PostgresFixture _postgres;

    public DashboardTests(PostgresFixture postgres) => _postgres = postgres;

    private sealed class FixedFinancialYearProvider : IFinancialYearProvider
    {
        public Task<int> GetStartMonthAsync(CancellationToken ct = default) => Task.FromResult(4);
    }

    [SkippableFact]
    public async Task Dashboard_returns_zeros_when_database_has_no_transactions()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid customerId = Guid.NewGuid();
        Guid orgId = Guid.NewGuid();

        await using ReportingDbContext db = _postgres.CreateContext(customerId, orgId);
        var repo = new DashboardRepository(db, TimeProvider.System, new FixedFinancialYearProvider());

        DashboardSummaryDto result = await repo.GetSummaryAsync();

        Assert.Equal(0m, result.SalesToday);
        Assert.Equal(0, result.InvoicesCountToday);
        Assert.Equal(0, result.UnpaidInvoicesCountToday);
        Assert.Equal(0m, result.TotalReceivables);
        Assert.Equal(0m, result.OverdueReceivables);
        Assert.Equal(0m, result.TotalPayables);
        Assert.Equal(0m, result.OverduePayables);
        Assert.Null(result.NextPayableDueDate);
        Assert.Equal(0m, result.CashAndBankTotal);
        Assert.Equal(0, result.BankAccountsCount);
        Assert.Equal(0m, result.Revenue);
        Assert.Equal(0m, result.CostOfGoodsSold);
        Assert.Equal(0m, result.OperatingExpenses);
        Assert.Equal(0m, result.NetProfit);
        Assert.Equal(0m, result.MarginPercent);
        Assert.Empty(result.TopProducts);
        Assert.Empty(result.Outstanding);
        Assert.NotEmpty(result.UpcomingCompliance);
        Assert.Equal(6, result.MonthlyTrend.Count);
    }

    [SkippableFact]
    public async Task Dashboard_calculates_real_sales_and_invoices_when_present()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid customerId = Guid.NewGuid();
        Guid orgId = Guid.NewGuid();

        await using ReportingDbContext db = _postgres.CreateContext(customerId, orgId);
        DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);

        db.Invoices.Add(new InvoiceRead
        {
            InvoiceId = 1,
            OrgId = orgId,
            DocumentNo = "INV-001",
            TransactionTypeCode = "INV",
            DocumentDate = today,
            DueDate = today.AddDays(15),
            CurrencyCode = "INR",
            TotalAmount = 1500m,
            TotalAmountBase = 1500m,
            Status = DocumentStatus.Posted
        });
        db.Invoices.Add(new InvoiceRead
        {
            InvoiceId = 2,
            OrgId = orgId,
            DocumentNo = "INV-002",
            TransactionTypeCode = "INV",
            DocumentDate = today,
            DueDate = today.AddDays(-5), // Overdue
            CurrencyCode = "INR",
            TotalAmount = 2500m,
            TotalAmountBase = 2500m,
            Status = DocumentStatus.Posted
        });
        await db.SaveChangesAsync();

        var repo = new DashboardRepository(db, TimeProvider.System, new FixedFinancialYearProvider());
        DashboardSummaryDto result = await repo.GetSummaryAsync();

        Assert.Equal(4000m, result.SalesToday);
        Assert.Equal(2, result.InvoicesCountToday);
        Assert.Equal(4000m, result.TotalReceivables);
        Assert.Equal(2500m, result.OverdueReceivables);
    }

    [SkippableFact]
    public async Task DashboardController_returns_ok_with_summary()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid customerId = Guid.NewGuid();
        Guid orgId = Guid.NewGuid();

        await using ReportingDbContext db = _postgres.CreateContext(customerId, orgId);
        var repo = new DashboardRepository(db, TimeProvider.System, new FixedFinancialYearProvider());
        var controller = new DashboardController(repo);

        IActionResult actionResult = await controller.GetSummary(CancellationToken.None);
        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var dto = Assert.IsType<DashboardSummaryDto>(okResult.Value);

        Assert.NotNull(dto);
        Assert.NotEmpty(dto.UpcomingCompliance);
    }
}
