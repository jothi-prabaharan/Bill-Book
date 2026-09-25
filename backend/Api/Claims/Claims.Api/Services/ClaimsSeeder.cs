using Claims.Entity.Enums;
using Claims.Entity.TableEntities;
using Claims.Repository;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Numbering;

namespace Claims.Api.Services;

public sealed class ClaimsSeeder
{
    private readonly ClaimsDbContext _db;

    public ClaimsSeeder(ClaimsDbContext db) => _db = db;

    public async Task<Dictionary<string, int>> SeedAsync(Guid customerId, Guid orgId, CancellationToken ct)
    {
        var seeded = new Dictionary<string, int>();

        // 1. Numbering Series for CLM
        seeded["numberingSeries"] = await AddWhenEmptyAsync(
            _db.NumberingSeries.IgnoreQueryFilters().AnyAsync(n => n.OrgId == orgId && n.SeriesCode == "CLM", ct),
            () => _db.NumberingSeries.Add(new NumberingSeries
            {
                CustomerId = customerId,
                OrgId = orgId,
                SeriesCode = "CLM",
                SeriesName = "Expense Claims",
                SeriesSystemName = "EXPENSE_CLAIM",
                SeriesFor = SeriesFor.Document,
                Prefix = "CLM",
                Separator = "-",
                IncludeFinancialYear = true,
                FinancialYearFormat = FinancialYearFormat.Compact,
                NumberLength = 5,
                NextNumber = 1,
                IsActive = true
            }));

        // 2. Default Claim Categories
        seeded["categories"] = await AddWhenEmptyAsync(
            _db.ClaimCategories.IgnoreQueryFilters().AnyAsync(c => c.OrgId == orgId, ct),
            () =>
            {
                _db.ClaimCategories.AddRange(
                    new ClaimCategory
                    {
                        CustomerId = customerId,
                        OrgId = orgId,
                        Code = "TRAV",
                        Name = "Travel & Conveyance",
                        IsReceiptRequired = true,
                        IsActive = true
                    },
                    new ClaimCategory
                    {
                        CustomerId = customerId,
                        OrgId = orgId,
                        Code = "MEAL",
                        Name = "Meals & Entertainment",
                        IsReceiptRequired = true,
                        IsActive = true
                    },
                    new ClaimCategory
                    {
                        CustomerId = customerId,
                        OrgId = orgId,
                        Code = "STAY",
                        Name = "Accommodation & Lodging",
                        IsReceiptRequired = true,
                        IsActive = true
                    },
                    new ClaimCategory
                    {
                        CustomerId = customerId,
                        OrgId = orgId,
                        Code = "COMM",
                        Name = "Mobile & Internet",
                        IsReceiptRequired = true,
                        IsActive = true
                    },
                    new ClaimCategory
                    {
                        CustomerId = customerId,
                        OrgId = orgId,
                        Code = "MISC",
                        Name = "Miscellaneous Expense",
                        IsReceiptRequired = false,
                        IsActive = true
                    }
                );
            });

        // 3. Default Approval Workflow
        seeded["workflows"] = await AddWhenEmptyAsync(
            _db.ApprovalWorkflows.IgnoreQueryFilters().AnyAsync(w => w.OrgId == orgId, ct),
            () =>
            {
                var wf = new ApprovalWorkflow
                {
                    CustomerId = customerId,
                    OrgId = orgId,
                    Name = "Default Expense Claim Workflow",
                    RequestKind = ClaimRequestKind.Claim,
                    EffectiveFrom = new DateOnly(2026, 1, 1),
                    IsActive = true,
                    Levels = new List<ApprovalWorkflowLevel>
                    {
                        new ApprovalWorkflowLevel
                        {
                            CustomerId = customerId,
                            OrgId = orgId,
                            Sequence = 1,
                            Label = "Reporting Manager",
                            ApproverKind = ApproverKind.ReportingChain,
                            ReportingDepth = 1
                        },
                        new ApprovalWorkflowLevel
                        {
                            CustomerId = customerId,
                            OrgId = orgId,
                            Sequence = 2,
                            Label = "Finance Approval",
                            ApproverKind = ApproverKind.SpecificRole,
                            SpecificRoleId = 3
                        }
                    }
                };
                _db.ApprovalWorkflows.Add(wf);
            });

        await _db.SaveChangesAsync(ct);
        return seeded;
    }

    private static async Task<int> AddWhenEmptyAsync(Task<bool> exists, Action add)
    {
        if (await exists)
        {
            return 0;
        }

        add();
        return 1;
    }
}
