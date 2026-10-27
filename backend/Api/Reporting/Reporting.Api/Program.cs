using Shared.Kernel.Restaurant;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Reporting.Api.Services;
using Reporting.Repository.Repositories;
using Shared.Kernel.CustomFields;
using Shared.Kernel.Numbering;
using Shared.Kernel.Security;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Reporting.Repository;
using Reporting.Repository.SeedData;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Secrets;
using Shared.Kernel.Internal;
using Shared.Kernel.Persistence;
using Shared.Kernel.Tenancy;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Fail at startup rather than on the first request that needs the missing
// registration. ValidateOnBuild walks every registration, so a service asking for
// something nobody registered is a container that refuses to build — which the
// build cannot see, because it does not resolve DI, and the tests cannot see,
// because they construct their services by hand.
builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateOnBuild = true;
    options.ValidateScopes = true;
});

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(
        new System.Text.Json.Serialization.JsonStringEnumConverter()));

// Attaches the shared internal key to every service-to-service call.
builder.Services.AddTransient<InternalKeyHandler>();

builder.Services.AddOpenApi();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<ITenantDatabaseResolver, TenantDatabaseResolver>();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddTenantMigration();

// Tenancy — resolved per request from the JWT, then used to pick the database.
builder.Services.AddScoped<TenantContext>();
builder.Services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddScoped<AuditSaveChangesInterceptor>();
builder.Services.AddScoped<RlsConnectionInterceptor>();
// Key Vault when KeyVault:Uri is set, configuration otherwise — and a
// startup failure in Production if neither, rather than serving requests off
// whatever configuration happens to hold. See SecretStoreRegistration.
builder.Services.AddSecretStore(builder.Configuration, builder.Environment);

// One shared tenant database now, so the connection string is fixed at
// startup rather than resolved per request.
builder.Services.AddDbContext<ReportingDbContext>((sp, options) =>
{
    options.UseNpgsql(
        sp.GetRequiredService<ITenantDatabaseResolver>().GetConnectionString(sp.GetService<ITenantContext>()?.CustomerId, sp.GetService<ITenantContext>()?.DatabaseName),
        npgsql =>
        {
            npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "rpt");
            npgsql.CommandTimeout(180);
        });

    // RlsConnectionInterceptor is what sets app.current_org_id, and it sets it
    // transaction-locally — a SET LOCAL prefixed to every command (TK-08).
    // Never connection-level: pooled connections are reused across requests,
    // so a connection-scoped org context leaks to whoever gets that connection
    // next — which is gate G3 in Reporting.md §9.1.
    options.AddInterceptors(
        sp.GetRequiredService<AuditSaveChangesInterceptor>(),
        sp.GetRequiredService<RlsConnectionInterceptor>());
});

// The branch's base currency, which names every money column's header. Cached
// per organization for the same reason the financial year is: it changes about
// never, and the alternative is an HTTP call on every report run.
builder.Services.AddHttpClient<IBaseCurrencyProvider, HttpBaseCurrencyProvider>(client =>
{
    client.BaseAddress = new Uri(RequiredSetting("Master:BaseUrl"));
})
    .AddHttpMessageHandler<InternalKeyHandler>();

// Every report, registered as an IReportSourceRepository. The catalog service discovers
// them from DI, so adding a report is adding a line here and a seed entry —
// there is no registry to keep in step.
// The branch's financial year, for the portal's trade value this year (TK-95).
builder.Services.AddHttpClient<IFinancialYearProvider, HttpFinancialYearProvider>(client =>
{
    client.BaseAddress = new Uri(RequiredSetting("Master:BaseUrl"));
})
    .AddHttpMessageHandler<InternalKeyHandler>();
builder.Services.AddScoped<IPortalAccountData, ReportingPortalAccountData>();
builder.Services.AddScoped<PortalAccountRepository>();

builder.Services.AddHttpClient<BatchedNameResolverRepository>(client =>
{
    client.BaseAddress = new Uri(RequiredSetting("Master:BaseUrl"));
})
    .AddHttpMessageHandler<InternalKeyHandler>();
builder.Services.AddScoped<IReportSourceRepository, AccountMovementSourceRepository>();
builder.Services.AddScoped<IReportSourceRepository, AccountTransactionSourceRepository>();
builder.Services.AddScoped<IReportSourceRepository, TrialBalanceSourceRepository>();
builder.Services.AddScoped<IReportSourceRepository, GeneralLedgerSummarySourceRepository>();
builder.Services.AddScoped<IReportSourceRepository, JournalReportSourceRepository>();
builder.Services.AddScoped<IReportSourceRepository, BankSummarySourceRepository>();
builder.Services.AddScoped<IReportSourceRepository, ReconciliationSourceRepository>();
builder.Services.AddScoped<IReportSourceRepository, InventoryAgingSourceRepository>();
builder.Services.AddScoped<IReportSourceRepository, ItemListSourceRepository>();
builder.Services.AddScoped<IReportSourceRepository, ItemDetailSourceRepository>();
builder.Services.AddScoped<IReportSourceRepository, ItemSummarySourceRepository>();
builder.Services.AddScoped<IReportSourceRepository, BatchTrackingStatusSourceRepository>();
builder.Services.AddScoped<IReportSourceRepository, BatchTrackingDetailSourceRepository>();
builder.Services.AddScoped<IReportSourceRepository, SerialTrackingStatusSourceRepository>();
builder.Services.AddScoped<IReportSourceRepository, SerialTrackingDetailSourceRepository>();
builder.Services.AddScoped<IReportSourceRepository, WarehouseTrackingStatusSourceRepository>();
builder.Services.AddScoped<IReportSourceRepository, WarehouseTrackingDetailSourceRepository>();
builder.Services.AddScoped<IReportSourceRepository, SalesRegisterSourceRepository>();
builder.Services.AddScoped<IReportSourceRepository, FxGainLossSourceRepository>();
builder.Services.AddScoped<IReportSourceRepository, FxGainLossDetailsSourceRepository>();
builder.Services.AddScoped<IReportSourceRepository, ArAgingSummarySourceRepository>();
builder.Services.AddScoped<IReportSourceRepository, ApAgingSummarySourceRepository>();
builder.Services.AddScoped<IReportSourceRepository, CustomerStatementSourceRepository>();
builder.Services.AddScoped<IReportSourceRepository, VendorStatementSourceRepository>();
builder.Services.AddScoped<IReportSourceRepository, PurchaseRegisterSourceRepository>();
builder.Services.AddScoped<IReportSourceRepository, Gstr1SummarySourceRepository>();

// The tracker and finance reports. Registering a source is half of making a
// report reachable — ReportCatalogRepository lists one only when a source and a
// seeded rpt.Reports row agree — so each of these has an entry in
// ReportCatalogSeeder.Catalog beside it.
builder.Services.AddScoped<IReportSourceRepository, BalanceSheetSourceRepository>();
builder.Services.AddScoped<IReportSourceRepository, ProfitAndLossSourceRepository>();
builder.Services.AddScoped<IReportSourceRepository, CashFlowSourceRepository>();
builder.Services.AddScoped<IReportSourceRepository, QuotationTrackSourceRepository>();
builder.Services.AddScoped<IReportSourceRepository, SalesOrderTrackSourceRepository>();
builder.Services.AddScoped<IReportSourceRepository, DeliveryOrderTrackSourceRepository>();
builder.Services.AddScoped<IReportSourceRepository, InvoiceTrackSourceRepository>();
builder.Services.AddScoped<IReportSourceRepository, AgedReceivablesDetailSourceRepository>();
builder.Services.AddScoped<IReportSourceRepository, SalesAnalysisSourceRepository>();
builder.Services.AddScoped<IReportSourceRepository, SalesAnalysisDetailSourceRepository>();
builder.Services.AddScoped<IReportSourceRepository, PurchaseOrderTrackSourceRepository>();
builder.Services.AddScoped<IReportSourceRepository, ReceiveOrderTrackSourceRepository>();
builder.Services.AddScoped<IReportSourceRepository, BillsTrackSourceRepository>();
builder.Services.AddScoped<IReportSourceRepository, AgedPayablesDetailsSourceRepository>();
builder.Services.AddScoped<IReportSourceRepository, PurchaseAnalysisSourceRepository>();
builder.Services.AddScoped<IReportSourceRepository, ReceivableInvoiceDetailSourceRepository>();
builder.Services.AddScoped<IReportSourceRepository, ReceivableInvoiceSummarySourceRepository>();
builder.Services.AddScoped<IReportSourceRepository, InvoiceDnPaymentCollectionSourceRepository>();
builder.Services.AddScoped<IReportSourceRepository, PayableInvoiceDetailSourceRepository>();
builder.Services.AddScoped<IReportSourceRepository, PayableInvoiceSummarySourceRepository>();
builder.Services.AddScoped<IReportSourceRepository, BillDnPaymentSourceRepository>();
builder.Services.AddScoped<IReportSourceRepository, PurchaseReceiveOrderDetailsSourceRepository>();
builder.Services.AddScoped<IReportSourceRepository, MsmeAgingSourceRepository>();
builder.Services.AddScoped<IReportSourceRepository, DepreciationScheduleSourceRepository>();
builder.Services.AddScoped<IReportSourceRepository, DisposalScheduleSourceRepository>();
builder.Services.AddScoped<IReportSourceRepository, FixedAssetReconciliationSourceRepository>();
builder.Services.AddScoped<IReportSourceRepository, FixedAssetsScheduleSourceRepository>();
builder.Services.AddScoped<IReportSourceRepository, BusinessPerformanceSourceRepository>();

// HRMS and Payroll reports (TK-59)
builder.Services.AddScoped<IReportSourceRepository, HeadcountSummarySource>();
builder.Services.AddScoped<IReportSourceRepository, JoinersLeaversSource>();
builder.Services.AddScoped<IReportSourceRepository, AttritionRateSource>();
builder.Services.AddScoped<IReportSourceRepository, ProbationDueSource>();
builder.Services.AddScoped<IReportSourceRepository, BirthdaysAnniversariesSource>();
builder.Services.AddScoped<IReportSourceRepository, DocumentExpirySource>();

builder.Services.AddScoped<IReportSourceRepository, DailyAttendanceSource>();
builder.Services.AddScoped<IReportSourceRepository, MonthlyMusterRollSource>();
builder.Services.AddScoped<IReportSourceRepository, LateEarlyOutSource>();
builder.Services.AddScoped<IReportSourceRepository, OvertimeSummarySource>();
builder.Services.AddScoped<IReportSourceRepository, AttendanceRegularisationsSource>();

builder.Services.AddScoped<IReportSourceRepository, LeaveRegisterSource>();
builder.Services.AddScoped<IReportSourceRepository, LeaveBalancesSource>();
builder.Services.AddScoped<IReportSourceRepository, LeaveEncashmentSource>();
builder.Services.AddScoped<IReportSourceRepository, TeamAvailabilitySource>();

builder.Services.AddScoped<IReportSourceRepository, SalaryRegisterSource>();
builder.Services.AddScoped<IReportSourceRepository, PayslipSummarySource>();
builder.Services.AddScoped<IReportSourceRepository, CtcReportSource>();
builder.Services.AddScoped<IReportSourceRepository, SalaryVarianceSource>();
builder.Services.AddScoped<IReportSourceRepository, BankAdviceSource>();
builder.Services.AddScoped<IReportSourceRepository, HeldSalariesSource>();
builder.Services.AddScoped<IReportSourceRepository, SalaryArrearsSource>();
builder.Services.AddScoped<IReportSourceRepository, LoansOutstandingSource>();

builder.Services.AddScoped<IReportSourceRepository, PfStatementSource>();
builder.Services.AddScoped<IReportSourceRepository, EsiStatementSource>();
builder.Services.AddScoped<IReportSourceRepository, PtStatementSource>();
builder.Services.AddScoped<IReportSourceRepository, LwfStatementSource>();
builder.Services.AddScoped<IReportSourceRepository, GratuityProvisionSource>();
builder.Services.AddScoped<IReportSourceRepository, BonusRegisterSource>();
builder.Services.AddScoped<IReportSourceRepository, TdsSummarySource>();

builder.Services.AddScoped<IReportSourceRepository, RecruitmentPipelineSource>();
builder.Services.AddScoped<IReportSourceRepository, TimeToHireSource>();
builder.Services.AddScoped<IReportSourceRepository, SourceEffectivenessSource>();
builder.Services.AddScoped<IReportSourceRepository, OfferAcceptanceSource>();

builder.Services.AddScoped<IReportSourceRepository, ClaimsByCategorySource>();
builder.Services.AddScoped<IReportSourceRepository, ClaimsByEmployeeSource>();
builder.Services.AddScoped<IReportSourceRepository, ClaimsPendingApprovalSource>();

// Project reports (TK-107)
builder.Services.AddScoped<IReportSourceRepository, ProjectProfitabilitySource>();
builder.Services.AddScoped<IReportSourceRepository, ProjectBudgetSource>();
builder.Services.AddScoped<IReportSourceRepository, ProjectUnbilledSource>();
builder.Services.AddScoped<IReportSourceRepository, ProjectTimeByUserSource>();

// Budget reports (TK-109)
builder.Services.AddScoped<IReportSourceRepository, BudgetAgainstActualSource>();
builder.Services.AddScoped<IReportSourceRepository, BudgetAgainstActualByProjectSource>();
builder.Services.AddScoped<IReportSourceRepository, BudgetMonthlyTrendSource>();

// Restaurant reports (TK-150). A Restaurant branch only, and hidden while the
// switch a report reads is off in Settings › Restaurant.
builder.Services.AddScoped<IReportSourceRepository, RestaurantDayEndSource>();
builder.Services.AddScoped<IReportSourceRepository, RestaurantItemSalesSource>();
builder.Services.AddScoped<IReportSourceRepository, RestaurantCategorySalesSource>();
builder.Services.AddScoped<IReportSourceRepository, RestaurantWaiterSalesSource>();
builder.Services.AddScoped<IReportSourceRepository, RestaurantTableTurnoverSource>();
builder.Services.AddScoped<IReportSourceRepository, RestaurantFoodCostSource>();
builder.Services.AddScoped<IReportSourceRepository, RestaurantWastageSource>();
builder.Services.AddScoped<IReportSourceRepository, RestaurantKotCancellationsSource>();

// Site reports (TK-176)
builder.Services.AddScoped<IReportSourceRepository, Reporting.Repository.Repositories.Sources.Site.SiteLaborerAttendanceSource>();
builder.Services.AddScoped<IReportSourceRepository, Reporting.Repository.Repositories.Sources.Site.SiteMaterialReconciliationSource>();
builder.Services.AddScoped<IReportSourceRepository, Reporting.Repository.Repositories.Sources.Site.SiteProfitSummarySource>();

builder.Services.AddScoped<RestaurantReportRepository>();
// The Salary Increment Report: its own endpoint and page, not a source (TK-198).
builder.Services.AddScoped<SalaryIncrementReportRepository>();
// The Balance Sheet: its own endpoint and page (TK-199).
builder.Services.AddScoped<BalanceSheetReportRepository>();
builder.Services.AddScoped<ProfitAndLossReportRepository>();
builder.Services.AddScoped<CashFlowReportRepository>();
builder.Services.AddScoped<BankReconciliationReportRepository>();
builder.Services.AddScoped<BusinessPerformanceReportRepository>();
builder.Services.AddScoped<BudgetVsActualReportRepository>();
builder.Services.AddScoped<BudgetVsActualByProjectReportRepository>();
builder.Services.AddScoped<BudgetMonthlyTrendReportRepository>();
builder.Services.AddScoped<JournalReportRepository>();
builder.Services.AddScoped<AccountTransactionReportRepository>();
builder.Services.AddScoped<BankSummaryReportRepository>();
builder.Services.AddScoped<ForeignCurrencyGainLossReportRepository>();
builder.Services.AddScoped<ForeignCurrencyGainLossDetailsReportRepository>();
builder.Services.AddScoped<AgedReceivablesSummaryReportRepository>();
builder.Services.AddScoped<AgedReceivablesDetailReportRepository>();
builder.Services.AddScoped<ReceivableInvoiceSummaryReportRepository>();
builder.Services.AddScoped<ReceivableInvoiceDetailReportRepository>();
builder.Services.AddScoped<InvoiceTrackReportRepository>();
builder.Services.AddScoped<InvoiceDnPaymentCollectionReportRepository>();
builder.Services.AddScoped<SalesAnalysisReportRepository>();
builder.Services.AddScoped<SalesAnalysisDetailReportRepository>();
builder.Services.AddScoped<SalesOrderTrackReportRepository>();
builder.Services.AddScoped<QuotationTrackReportRepository>();
builder.Services.AddScoped<DeliveryOrderTrackReportRepository>();
builder.Services.AddScoped<AgedPayablesSummaryReportRepository>();
builder.Services.AddScoped<AgedPayablesDetailsReportRepository>();
builder.Services.AddScoped<PayableInvoiceSummaryReportRepository>();
builder.Services.AddScoped<PayableInvoiceDetailReportRepository>();
builder.Services.AddScoped<BillsTrackReportRepository>();

// The report builder's datasets (TK-113). Their custom field columns come
// from Master's definitions, cached five minutes per branch and kind.
builder.Services.AddHttpClient<ICustomFieldDefinitions, HttpCustomFieldDefinitions>(client =>
{
    client.BaseAddress = new Uri(RequiredSetting("Master:BaseUrl"));
})
    .AddHttpMessageHandler<InternalKeyHandler>();
builder.Services.AddScoped<IReportSourceRepository, SalesLinesDataset>();
builder.Services.AddScoped<IReportSourceRepository, PurchaseLinesDataset>();
builder.Services.AddScoped<IReportSourceRepository, LedgerRowsDataset>();
builder.Services.AddScoped<IReportSourceRepository, StockMovementsDataset>();
builder.Services.AddScoped<IReportSourceRepository, ItemsDataset>();
builder.Services.AddScoped<IReportSourceRepository, ContactsDataset>();
builder.Services.AddScoped<CustomReportRepository>();

// Scheduled report email (TK-114). Master lists the branches and their users
// with current permissions, and sends the mail (through Notification.Worker
// where it runs); the worker claims due schedules with a guarded update.
builder.Services.AddHttpClient<IBranchUsers, HttpBranchUsers>(client =>
{
    client.BaseAddress = new Uri(RequiredSetting("Master:BaseUrl"));
})
    .AddHttpMessageHandler<InternalKeyHandler>();
builder.Services.AddHttpClient<IReportMailer, MasterReportMailer>(client =>
{
    client.BaseAddress = new Uri(RequiredSetting("Master:BaseUrl"));
    client.Timeout = TimeSpan.FromMinutes(2);
})
    .AddHttpMessageHandler<InternalKeyHandler>();
builder.Services.AddHttpClient<ITenantEnumerator, HttpTenantEnumerator>(client =>
{
    client.BaseAddress = new Uri(RequiredSetting("Master:BaseUrl"));
})
    .AddHttpMessageHandler<InternalKeyHandler>();
builder.Services.TryAddSingleton(TimeProvider.System);
builder.Services.AddBillBookWorkerErrorAudit<ReportingDbContext>();
builder.Services.AddScoped<IScheduledReportExporter, ScheduledReportExporterRepository>();
builder.Services.AddScoped<ReportScheduleRunRepository>();
builder.Services.AddScoped<ReportScheduleRepository>();

// The branch's own GSTIN for the GSTR-1 export (TK-115), as Sales reads it for its documents.
builder.Services.AddHttpClient<IOrgIdentityProvider, HttpOrgIdentityProvider>(client =>
{
    client.BaseAddress = new Uri(RequiredSetting("Master:BaseUrl"));
})
    .AddHttpMessageHandler<InternalKeyHandler>();
builder.Services.AddHostedService<ReportScheduleWorker>();

builder.Services.AddScoped<ReportCatalogRepository>();
builder.Services.AddScoped<ReportAudienceRepository>();
builder.Services.AddScoped<ReportRunnerRepository>();
builder.Services.AddScoped<SavedViewRepository>();
builder.Services.AddScoped<ReportCatalogSeeder>();
builder.Services.AddScoped<DashboardRepository>();

// Every write endpoint runs in a transaction and rolls back unless the action
// succeeded, and every failure is translated through the SQLSTATE catalogue,
// recorded in ReportingDbContext's own ErrorLogs table, and answered with the exact
// error in Development or a curated sentence everywhere else. Registered as one
// call because half of it is worse than neither: transactions without the
// handler leak SQL to callers, the handler without transactions reports a
// failure that was partly applied.
builder.Services.AddBillBookReliability<ReportingDbContext>();

// Must match Master's key exactly: Master mints the tokens, Reporting only
// validates them. Never fall back to a constant here.
builder.Services.AddBillBookAuthentication(builder.Configuration);

// Default deny: a controller added later is authenticated because nobody did
// anything about it. The internal seed endpoint says [AllowAnonymous] explicitly,
// which makes the exception visible on the controller itself.
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// A Restaurant branch's switches and billing rules, from Master (TK-143).
builder.Services.AddRestaurantSettings(builder.Configuration);

WebApplication app = builder.Build();

app.MapOpenApi().AllowAnonymous();

// First in the pipeline. A failure in authentication or in the tenant
// middleware below is answered in the product's shape rather than by Kestrel.
app.UseBillBookErrorHandling();

app.UseAuthentication();
app.UseAuthorization();

// After authentication so the claims are available, before any DbContext use.
app.UseMiddleware<TenantMiddleware>();
app.UseTenantMigration<ReportingDbContext>();

app.MapControllers();

app.Run();

// Settings with no safe default. Blank is treated as missing: appsettings.json ships
// every key present but empty so the shape is discoverable, which means a `??` fallback
// would never fire and a broken value would flow on silently instead.
string RequiredSetting(string key) =>
    builder.Configuration[key] is { Length: > 0 } value
        ? value
        : throw new InvalidOperationException(
            $"{key} is not configured. Set it in appsettings.{{Environment}}.json or via the " +
            $"{key.Replace(':', '_').Replace("_", "__")} environment variable.");

#pragma warning disable CS8321
string RequiredConnectionString(string name) =>
    builder.Configuration.GetConnectionString(name) is { Length: > 0 } value
        ? value
        : throw new InvalidOperationException(
            $"ConnectionStrings:{name} is not configured. Set it in appsettings.{{Environment}}.json " +
            $"or via the ConnectionStrings__{name} environment variable.");


