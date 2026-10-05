using Microsoft.EntityFrameworkCore;
using Sales.Entity.Enums;
using Sales.Entity.Models;
using Sales.Entity.TableEntities;
using Sales.Repository.Clients;
using Shared.Kernel.Contacts;
using Shared.Kernel.Documents;
using Shared.Kernel.Internal;
using Shared.Kernel.Ledgers;
using Shared.Kernel.Persistence;
using Shared.Kernel.Restaurant;
using Shared.Kernel.Tax;
using Shared.Kernel.Tenancy;

namespace Sales.Repository.Repositories;

/// <summary>
/// Billing restaurant orders (TK-148). <b>There is no second posting path</b>:
/// a bill is a sales invoice made and posted by <see cref="IInvoiceRepository"/>,
/// so tax, numbering, stock and the ledger are the invoice's own. What billing
/// adds is choosing the dishes (one order, several, or some of one), the
/// service charge as taxed invoice lines, the tenders, and the tip, which is
/// the staff's money and is posted on its own to Tips Payable.
/// </summary>
public sealed class RestaurantBillingRepository
{
    /// <summary>The transaction type a tip posts under (mst.TransactionTypes).</summary>
    public const string TipTransactionType = "TIP";

    private const string SettingsDown = "The restaurant settings could not be read. Try again in a moment.";
    private const string SalesRevenue = "Sales Revenue";
    private const string TipsPayable = "Tips Payable";

    // The ledger's type and source codes, as the invoice posts them.
    private const int ControlLedgerType = 3;
    private const int TransactionLedgerSource = 3;

    private readonly SalesDbContext _db;
    private readonly IInvoiceRepository _invoices;
    private readonly IRestaurantSettingsProvider _settings;
    private readonly IWalkInContact _walkIn;
    private readonly IAccountDirectory _accounts;
    private readonly ILedgerClient _ledger;
    private readonly ITaxRateProvider _rates;
    private readonly IBranchSettingsProvider _branch;
    private readonly ITenantContext _tenant;
    private readonly TimeProvider _clock;
    private readonly IRecipeConsumptionClient? _recipes;

    public RestaurantBillingRepository(
        SalesDbContext db,
        IInvoiceRepository invoices,
        IRestaurantSettingsProvider settings,
        IWalkInContact walkIn,
        IAccountDirectory accounts,
        ILedgerClient ledger,
        ITaxRateProvider rates,
        IBranchSettingsProvider branch,
        ITenantContext tenant,
        TimeProvider clock,
        IRecipeConsumptionClient? recipes = null)
    {
        _recipes = recipes;
        _db = db;
        _invoices = invoices;
        _settings = settings;
        _walkIn = walkIn;
        _accounts = accounts;
        _ledger = ledger;
        _rates = rates;
        _branch = branch;
        _tenant = tenant;
        _clock = clock;
    }

    private DateTime Now => _clock.GetLocalNow().DateTime;

    /// <summary>
    /// What the bill will come to: the dishes, the service charge and the tax
    /// worked out by the same calculator the invoice uses, and the first thing
    /// the settings still require, so the page can say so before anyone pays.
    /// </summary>
    public async Task<OrderResult<BillPreview>> PreviewAsync(BillRestaurantOrdersRequest request, CancellationToken ct)
    {
        if (await _settings.GetSettingsAsync(ct) is not { } settings)
        {
            return OrderResult<BillPreview>.Down(SettingsDown);
        }

        (string? refusal, List<RestaurantOrder> orders, List<RestaurantOrderLine> lines) = await SelectAsync(request, ct);
        if (refusal is not null)
        {
            return orders.Count == 0 && refusal == "missing" ? OrderResult<BillPreview>.Missing() : OrderResult<BillPreview>.Refuse(refusal);
        }

        decimal percent = request.ApplyServiceCharge ? settings.ServiceChargePercent : 0m;
        IReadOnlyList<RestaurantBillingRulesRepository.ServiceChargePart> charge = RestaurantBillingRulesRepository.ServiceCharge(lines, percent);

        BranchSettings? branch = await _branch.GetSettingsAsync(ct);
        IReadOnlyDictionary<long, TaxRate>? rates = await _rates.GetRatesAsync(DateOnly.FromDateTime(Now), ct);
        if (branch is null || rates is null)
        {
            return OrderResult<BillPreview>.Down("The tax rates could not be read. Try again in a moment.");
        }

        TaxContext context = new(false, branch.DiscountBeforeTax, branch.IsUnionTerritory);
        List<TaxLineResult> computed = [];
        foreach (SaveInvoiceLineRequest line in lines.Select(RestaurantBillingRulesRepository.InvoiceLine).Concat(charge.Select(c => ChargeLine(c, percent, accountId: 0))))
        {
            computed.Add(GstCalculator.Compute(new TaxLineInput
            {
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice,
                IsPriceInclusive = line.IsPriceInclusive,
                TaxTreatment = line.TaxTreatment,
                Rate = line.TaxGroupId is long g ? rates.GetValueOrDefault(g) : null,
                ConversionFactor = 1m,
            }, context));
        }

        TaxDocumentTotals totals = GstCalculator.Totals(computed);
        decimal roundOff = Math.Round(totals.TotalAmount, 0, MidpointRounding.AwayFromZero) - totals.TotalAmount;
        decimal total = totals.TotalAmount + roundOff;
        decimal tip = settings.TipsEnabled ? Math.Max(0m, request.TipAmount) : 0m;

        return OrderResult<BillPreview>.Done(new BillPreview
        {
            SubTotal = lines.Sum(l => Math.Round(l.Quantity * l.UnitPrice, 2)),
            ServiceChargePercent = percent,
            ServiceChargeAmount = charge.Sum(c => c.Amount),
            TaxAmount = totals.CgstAmount + totals.SgstAmount + totals.IgstAmount + totals.CessAmount,
            RoundOff = roundOff,
            Total = total,
            TipAmount = tip,
            AmountToPay = total + tip,
            LineIds = [.. lines.Select(l => l.RestaurantOrderLineId)],
            Problem = RequirementRefusal(settings, orders, lines, request.ContactId),
        });
    }

    /// <summary>
    /// Bills the chosen dishes on one invoice and posts it, with its tenders;
    /// posts the tip to Tips Payable; marks each dish billed, and each order
    /// whose every dish is now billed or cancelled as Billed, with its table.
    /// </summary>
    public async Task<OrderResult<RestaurantBillView>> BillAsync(BillRestaurantOrdersRequest request, CancellationToken ct)
    {
        if (await _settings.GetSettingsAsync(ct) is not { } settings)
        {
            return OrderResult<RestaurantBillView>.Down(SettingsDown);
        }

        (string? selectRefusal, List<RestaurantOrder> orders, List<RestaurantOrderLine> lines) = await SelectAsync(request, ct);
        if (selectRefusal is not null)
        {
            return orders.Count == 0 && selectRefusal == "missing" ? OrderResult<RestaurantBillView>.Missing() : OrderResult<RestaurantBillView>.Refuse(selectRefusal);
        }

        string? refusal = RequirementRefusal(settings, orders, lines, request.ContactId)
            ?? RestaurantBillingRulesRepository.TipRefusal(settings.TipsEnabled, request.TipAmount, request.TipBankAccountId);
        if (refusal is not null)
        {
            return OrderResult<RestaurantBillView>.Refuse(refusal);
        }

        long? contactId = request.ContactId ?? orders.Select(o => o.ContactId).FirstOrDefault(c => c is not null);
        bool walkIn = false;
        if (contactId is null)
        {
            contactId = await _walkIn.GetAsync(ct);
            walkIn = true;
            if (contactId is null)
            {
                return OrderResult<RestaurantBillView>.Refuse("This branch has no walk-in customer. Choose the customer the bill is for.");
            }
        }

        if (walkIn && request.Tenders.Count == 0)
        {
            return OrderResult<RestaurantBillView>.Refuse("A bill to the walk-in customer must be paid. Add how it was paid, or choose a customer to bill on account.");
        }

        decimal percent = request.ApplyServiceCharge ? settings.ServiceChargePercent : 0m;
        IReadOnlyList<RestaurantBillingRulesRepository.ServiceChargePart> charge = RestaurantBillingRulesRepository.ServiceCharge(lines, percent);
        long serviceChargeAccount = 0;
        if (charge.Count > 0)
        {
            // A free-text invoice line names its account; the service charge is
            // revenue, so it names the account the invoice credits revenue to.
            serviceChargeAccount = (await _accounts.AccountsAsync([4], [], ct))
                .FirstOrDefault(a => a.AccountSystemName == SalesRevenue)?.AccountId ?? 0;
            if (serviceChargeAccount == 0)
            {
                return OrderResult<RestaurantBillView>.Refuse("The chart of accounts has no Sales Revenue account to charge the service charge to.");
            }
        }

        await using ITransactionScope tx = await _db.Database.BeginScopeAsync(ct);

        DateTime now = Now;
        DateOnly today = DateOnly.FromDateTime(now);
        RestaurantOrder first = orders[0];
        string tables = string.Join(", ", (await TableCodesAsync(orders, ct)).Distinct());

        InvoiceResult created = await _invoices.CreateAsync(new SaveInvoiceRequest
        {
            DocumentDate = today,
            DueDate = today,
            ContactId = contactId.Value,
            Notes = $"Restaurant {string.Join(", ", orders.Select(o => o.OrderNo))}{(tables.Length > 0 ? $", table {tables}" : string.Empty)}",
            Lines = [.. lines.Select(RestaurantBillingRulesRepository.InvoiceLine), .. charge.Select(c => ChargeLine(c, percent, serviceChargeAccount))],
        }, ct);
        if (created.Outcome != InvoiceOutcome.Ok)
        {
            return OrderResult<RestaurantBillView>.Refuse(created.Detail ?? $"The invoice was refused ({created.Outcome}).");
        }

        Invoice invoice = await _db.Invoices.FirstAsync(i => i.InvoiceId == created.InvoiceId, ct);
        decimal change = 0m;
        if (request.Tenders.Count > 0)
        {
            (PosSaleOutcome check, decimal given, string? detail) = PosSaleRepository.CheckTenders(invoice.TotalAmount, request.Tenders);
            if (check != PosSaleOutcome.Ok)
            {
                return OrderResult<RestaurantBillView>.Refuse(detail ?? "The payments do not cover the bill.");
            }

            change = given;
            invoice.TenderedAmount = request.Tenders.Sum(t => t.Amount);
            invoice.ChangeAmount = change;
            invoice.PaymentMode = request.Tenders.Select(t => t.Mode).Distinct().Count() == 1 ? request.Tenders[0].Mode.ToString() : "Split";
            _db.InvoiceTenders.AddRange(request.Tenders.Select(t => new InvoiceTender
            {
                InvoiceId = invoice.InvoiceId,
                Mode = t.Mode,
                Amount = t.Amount,
                BankAccountId = t.BankAccountId,
                Reference = string.IsNullOrWhiteSpace(t.Reference) ? null : t.Reference.Trim(),
            }));
            await _db.SaveChangesAsync(ct);
        }

        InvoiceResult posted = await _invoices.PostAsync(invoice.InvoiceId, ct);
        if (posted.Outcome != InvoiceOutcome.Ok)
        {
            return OrderResult<RestaurantBillView>.Refuse(posted.Detail ?? $"The invoice could not be posted ({posted.Outcome}).");
        }

        RestaurantBill bill = new()
        {
            InvoiceId = invoice.InvoiceId,
            ServiceChargePercent = percent,
            ServiceChargeAmount = charge.Sum(c => c.Amount),
            TipAmount = request.TipAmount,
            TipBankAccountId = request.TipAmount > 0m ? request.TipBankAccountId : null,
            BilledAt = now,
        };
        _db.RestaurantBills.Add(bill);
        await _db.SaveChangesAsync(ct);

        if (bill.TipAmount > 0m && await PostTipAsync(bill, today, contactId.Value, ct) is string tipRefusal)
        {
            return OrderResult<RestaurantBillView>.Refuse(tipRefusal);
        }

        foreach (RestaurantOrderLine line in lines)
        {
            line.RestaurantBillId = bill.RestaurantBillId;
        }

        // The ingredients leave stock last, once the bill is otherwise sure to
        // stand: Inventory files each issue under the bill, so a retry of the
        // same bill issues nothing twice (TK-149).
        if (settings.RecipesEnabled && _recipes is not null && await ConsumeAsync(bill, today, lines, ct) is string consumeRefusal)
        {
            return OrderResult<RestaurantBillView>.Refuse(consumeRefusal);
        }

        List<long> closed = [];
        foreach (RestaurantOrder order in orders)
        {
            if (RestaurantBillingRulesRepository.Billable(order.Lines).Any())
            {
                continue;
            }

            order.Status = RestaurantOrderStatus.Billed;
            closed.Add(order.RestaurantOrderId);
            if (order.RestaurantTableId is long tableId)
            {
                foreach (RestaurantTable t in await _db.RestaurantTables.Where(t => t.RestaurantTableId == tableId || t.MergedIntoTableId == tableId).ToListAsync(ct))
                {
                    t.Status = TableStatus.Billed;
                }
            }
        }

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return OrderResult<RestaurantBillView>.Done(new RestaurantBillView
        {
            RestaurantBillId = bill.RestaurantBillId,
            InvoiceId = invoice.InvoiceId,
            InvoiceNo = invoice.DocumentNo,
            Total = invoice.TotalAmount,
            ServiceChargeAmount = bill.ServiceChargeAmount,
            TipAmount = bill.TipAmount,
            Change = change,
            BilledAt = bill.BilledAt,
            ClosedOrderIds = closed,
        });
    }

    /// <summary>The invoice a bill was made on, for printing it through the invoice's template.</summary>
    public Task<long?> InvoiceOfAsync(long billId, CancellationToken ct) =>
        _db.RestaurantBills.Where(b => b.RestaurantBillId == billId).Select(b => (long?)b.InvoiceId).FirstOrDefaultAsync(ct);

    /// <summary>The branch's cash and bank accounts, for choosing where a payment or a tip went.</summary>
    public Task<IReadOnlyList<BankAccountSummary>> TenderAccountsAsync(CancellationToken ct) => _accounts.BankAccountsAsync(ct);

    // ---- Helpers ----

    /// <summary>
    /// The orders and the dishes to bill: every order open, every dish chosen
    /// still billable. "missing" for an order that does not exist, so the
    /// caller answers 404 rather than a sentence.
    /// </summary>
    private async Task<(string? Refusal, List<RestaurantOrder> Orders, List<RestaurantOrderLine> Lines)> SelectAsync(BillRestaurantOrdersRequest request, CancellationToken ct)
    {
        List<long> ids = request.OrderIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return ("Choose at least one order to bill.", [], []);
        }

        List<RestaurantOrder> orders = await _db.RestaurantOrders
            .Include(o => o.Lines).ThenInclude(l => l.Modifiers)
            .Where(o => ids.Contains(o.RestaurantOrderId))
            .OrderBy(o => o.OpenedAt)
            .ToListAsync(ct);
        if (orders.Count != ids.Count)
        {
            return ("missing", [], []);
        }

        if (orders.FirstOrDefault(o => o.Status != RestaurantOrderStatus.Open) is { } closed)
        {
            return ($"Order {closed.OrderNo} is not open.", orders, []);
        }

        List<RestaurantOrderLine> available = [.. orders.SelectMany(o => RestaurantBillingRulesRepository.Billable(o.Lines))];
        if (RestaurantBillingRulesRepository.SelectionRefusal(available, request.LineIds) is string refusal)
        {
            return (refusal, orders, []);
        }

        List<RestaurantOrderLine> lines = request.LineIds.Count == 0
            ? available
            : [.. available.Where(l => request.LineIds.Contains(l.RestaurantOrderLineId))];
        return (null, orders, [.. lines.OrderBy(l => l.RestaurantOrderId).ThenBy(l => l.LineNo)]);
    }

    /// <summary>What the effective settings require that the bill lacks, or null: each order's table, customer and waiter, and every dish sent to the kitchen when that is required.</summary>
    private static string? RequirementRefusal(RestaurantSettingsView settings, List<RestaurantOrder> orders, List<RestaurantOrderLine> lines, long? contactOverride)
    {
        foreach (RestaurantOrder order in orders)
        {
            if (RestaurantOrderRulesRepository.RequirementRefusal(settings, order.OrderType, order.RestaurantTableId, contactOverride ?? order.ContactId, order.WaiterUserId) is string missing)
            {
                return $"Order {order.OrderNo}: {missing}";
            }
        }

        return KotRulesRepository.BillingRefusal(settings, lines);
    }

    private static SaveInvoiceLineRequest ChargeLine(RestaurantBillingRulesRepository.ServiceChargePart part, decimal percent, long accountId) => new()
    {
        Description = $"Service charge {percent:0.##}%",
        Quantity = 1m,
        UnitPrice = part.Amount,
        IsPriceInclusive = part.IsPriceInclusive,
        TaxTreatment = part.TaxTreatment,
        TaxGroupId = part.TaxGroupId,
        LineType = DocumentLineType.Expense,
        AccountId = accountId,
    };

    private async Task<List<string>> TableCodesAsync(List<RestaurantOrder> orders, CancellationToken ct)
    {
        List<long> ids = orders.Where(o => o.RestaurantTableId is not null).Select(o => o.RestaurantTableId!.Value).ToList();
        return ids.Count == 0 ? [] : await _db.RestaurantTables.Where(t => ids.Contains(t.RestaurantTableId)).Select(t => t.Code).ToListAsync(ct);
    }

    /// <summary>
    /// Asks Inventory to take the billed dishes' ingredients out of stock: a
    /// dish made to order by its recipe, a stocked dish only for its choices
    /// (the invoice issued the dish itself). A refusal names what is short.
    /// </summary>
    private async Task<string?> ConsumeAsync(RestaurantBill bill, DateOnly date, List<RestaurantOrderLine> lines, CancellationToken ct)
    {
        List<DishToConsume> dishes = lines
            .Select(l => new DishToConsume(
                l.ItemId,
                l.Quantity,
                l.PortionFactor,
                !l.IsStocked,
                [.. l.Modifiers.Where(m => m.LinkedItemId is not null && m.LinkedQuantity is not null).Select(m => (m.LinkedItemId!.Value, m.LinkedQuantity!.Value))]))
            .ToList();

        ConsumptionResult result = await _recipes!.ConsumeAsync(bill.RestaurantBillId, date, dishes, ct);
        return result.Done
            ? null
            : result.Refusal ?? "Inventory could not be reached to take the ingredients out of stock. Try again in a moment.";
    }

    /// <summary>The tip, posted apart from the invoice: Dr where it was paid in, Cr Tips Payable. A refusal names why.</summary>
    private async Task<string?> PostTipAsync(RestaurantBill bill, DateOnly date, long contactId, CancellationToken ct)
    {
        (Guid customerId, Guid orgId) = _tenant.Require();
        PostLedgerOutcomeResult result = await _ledger.PostAsync(new PostLedgerRequest
        {
            CustomerId = customerId,
            OrgId = orgId,
            TransactionTypeCode = TipTransactionType,
            TransactionId = bill.RestaurantBillId,
            LedgerDate = date,
            ContactId = contactId,
            SourceDocumentId = bill.InvoiceId,
            Legs =
            [
                new LedgerLegRequest
                {
                    LedgerTypeId = ControlLedgerType,
                    LedgerSourceId = TransactionLedgerSource,
                    TransactionDetailId = 0,
                    BankAccountId = bill.TipBankAccountId,
                    DebitAmount = bill.TipAmount,
                    TransactionDesc = "Tip received",
                },
                new LedgerLegRequest
                {
                    LedgerTypeId = ControlLedgerType,
                    LedgerSourceId = TransactionLedgerSource,
                    TransactionDetailId = 0,
                    AccountSystemName = TipsPayable,
                    CreditAmount = bill.TipAmount,
                    TransactionDesc = "Tip for the staff",
                },
            ],
        }, ct);

        return result.Posted ? null : result.Detail ?? "The tip could not be posted.";
    }
}
