using Accounting.Entity.Enums;
using Accounting.Entity.TableEntities;

namespace Accounting.Repository.SeedData;

/// <summary>
/// The payment terms written when an organization is created. Due on Receipt is
/// the default because most Indian retail is settled at the counter; a
/// distributor changes it once and never thinks about it again.
/// </summary>
public static class PaymentTermsSeed
{
    public static IReadOnlyList<PaymentTerm> Build(Guid orgId) =>
    [
        Term(orgId, 10, "DUE_ON_RECEIPT", "Due on Receipt", PaymentTermType.DueOnReceipt, 0, isDefault: true),
        Term(orgId, 20, "NET15", "Net 15", PaymentTermType.Net, 15),
        Term(orgId, 30, "NET30", "Net 30", PaymentTermType.Net, 30),
        Term(orgId, 40, "NET45", "Net 45", PaymentTermType.Net, 45),
        Term(orgId, 50, "NET60", "Net 60", PaymentTermType.Net, 60),
        Term(orgId, 60, "EOM", "End of Month", PaymentTermType.EndOfMonth, 0),
    ];

    private static PaymentTerm Term(
        Guid orgId,
        int displayOrder,
        string systemName,
        string name,
        PaymentTermType type,
        int dueDays,
        bool isDefault = false) =>
        new()
        {
            OrgId = orgId,
            TermSystemName = systemName,
            TermName = name,
            TermType = type,
            DueDays = dueDays,
            DueDayOfMonth = null,
            DiscountPercent = 0,
            DiscountDays = 0,
            IsSales = true,
            IsPurchase = true,
            IsDefault = isDefault,
            IsSystem = true,
            IsActive = true,
            DisplayOrder = displayOrder,
        };
}
