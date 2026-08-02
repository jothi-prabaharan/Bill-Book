using Inventory.Entity.Enums;
using Inventory.Entity.TableEntities;

namespace Inventory.Api.Services;

/// <summary>
/// What a stock movement means in the general ledger.
///
/// Pure, static and the only place the mapping lives, because it is the part
/// that is wrong quietly. A movement posted to the wrong account does not fail —
/// it produces a balance sheet that balances and a gross margin that lies.
/// </summary>
public static class StockLedgerMapping
{
    /// <summary>
    /// The control accounts this maps onto, by the immutable
    /// <c>AccountSystemName</c> Accounting seeds them under.
    ///
    /// Strings rather than a shared enum: Accounting owns the chart of accounts,
    /// and a project reference across a service boundary to borrow one enum is
    /// how two services come to share a deployment. These three names are stored
    /// data on Accounting's side and can never change once an organization has
    /// been seeded, which is what makes a string safe here.
    /// </summary>
    public const string Inventory = "Inventory";

    public const string CostOfGoodsSold = "Cost of Goods Sold";

    public const string OpeningBalanceEquity = "Opening Balance Equity";

    /// <summary>Cost of goods sold. Both legs of a stock posting are that leg of the document.</summary>
    public const int CogsLedgerType = 4;

    private const int SourceTransaction = 1;
    private const int SourceOpeningBalance = 13;
    private const int SourceStockAdjustment = 15;

    /// <summary>Stock adjustment — what an unsourced movement files itself under.</summary>
    private const string StockAdjustmentCode = "STA";

    private const string OpeningBalanceCode = "OPB";

    /// <summary>
    /// The posting for a movement, or <c>null</c> when it has none — which is a
    /// real answer, not a failure, and is why the caller marks those
    /// <see cref="LedgerStatus.NotApplicable"/> rather than retrying them.
    /// </summary>
    public static StockPosting? For(StockMovement movement)
    {
        // A transfer changes where stock is, not what the branch owns. Posting
        // it would move value between two accounts that are the same account.
        if (movement.MovementType is StockMovementType.TransferIn or StockMovementType.TransferOut)
        {
            return null;
        }

        bool sourced = movement.SourceType is { Length: > 0 };

        // Goods arriving against a purchase document, or going back against one,
        // are posted by that document: its other leg is Accounts Payable, and
        // only Purchase knows which vendor and how much of it is tax. Posting
        // the stock half here as well would double the inventory asset.
        if (sourced
            && movement.MovementType is StockMovementType.Receipt or StockMovementType.PurchaseReturn)
        {
            return null;
        }

        (string debit, string credit, int ledgerSource) = movement.MovementType switch
        {
            // A migration, and its counterpart: a receipt with no document
            // behind it. Both are the business asserting stock it holds, and
            // Opening Balance Equity is the account whose whole purpose is to
            // carry the other side of an assertion. Purchase supersedes this
            // branch for receipts the day it lands — a real receipt will arrive
            // carrying its bill, and take the sourced path above instead.
            StockMovementType.Opening or StockMovementType.Receipt =>
                (Inventory, OpeningBalanceEquity, SourceOpeningBalance),

            StockMovementType.PurchaseReturn =>
                (OpeningBalanceEquity, Inventory, SourceOpeningBalance),

            // The sale. This posting is the entire reason gross profit exists:
            // revenue is Income and this is Expense, and a report can only
            // subtract one from the other because they are different types.
            StockMovementType.Issue =>
                (CostOfGoodsSold, Inventory, sourced ? SourceTransaction : SourceStockAdjustment),

            StockMovementType.SalesReturn =>
                (Inventory, CostOfGoodsSold, sourced ? SourceTransaction : SourceStockAdjustment),

            // Shrinkage, breakage and count corrections. Cost of goods sold
            // rather than an account of their own: stock that has gone without
            // being sold still cost what it cost, and inventing a shrinkage
            // account here would put it outside the margin every report reads.
            StockMovementType.Adjustment => movement.Direction == StockDirection.Out
                ? (CostOfGoodsSold, Inventory, SourceStockAdjustment)
                : (Inventory, CostOfGoodsSold, SourceStockAdjustment),

            _ => (string.Empty, string.Empty, 0),
        };

        if (debit.Length == 0)
        {
            return null;
        }

        // Which document these rows belong to. An unsourced movement is its own
        // document — there is nothing else to file it under, and giving it the
        // movement id keeps every posting traceable to one row.
        string typeCode = sourced
            ? movement.SourceType!
            : ledgerSource == SourceOpeningBalance ? OpeningBalanceCode : StockAdjustmentCode;

        long transactionId = sourced ? movement.SourceId ?? 0 : movement.StockMovementId;

        // The document's own line, when there is one. A movement is unique per
        // (document, line) already, so this cannot collide, and it means a
        // reader looking at an invoice sees its line numbers rather than
        // inventory's internal ids.
        long detailId = sourced ? movement.SourceLineId : movement.StockMovementId;

        return new StockPosting(typeCode, transactionId, detailId, ledgerSource, debit, credit);
    }
}

/// <summary>
/// One stock posting: two legs, equal and opposite, filed under a document.
///
/// Always exactly two legs and always the same amount, so it cannot be
/// unbalanced by construction. There is no case where stock leaving is worth a
/// different amount to the cost of stock leaving.
/// </summary>
/// <param name="TransactionTypeCode">The document type these rows file under.</param>
/// <param name="TransactionId">The document.</param>
/// <param name="TransactionDetailId">The document line.</param>
/// <param name="LedgerSourceId">What produced the posting.</param>
/// <param name="DebitAccountSystemName">The account taking the debit.</param>
/// <param name="CreditAccountSystemName">The account taking the credit.</param>
public sealed record StockPosting(
    string TransactionTypeCode,
    long TransactionId,
    long TransactionDetailId,
    int LedgerSourceId,
    string DebitAccountSystemName,
    string CreditAccountSystemName);
