using Inventory.Api.Services;
using Inventory.Entity.Enums;
using Inventory.Entity.TableEntities;
using Xunit;

namespace Inventory.Api.Tests;

/// <summary>
/// What a stock movement means in the general ledger.
///
/// This earns a test where the rest of Inventory does not, because it is the
/// one piece that fails <i>silently</i>. A wrong guard refuses a sale and
/// somebody rings up; a wrong account produces a balance sheet that still
/// balances, a gross margin that is simply untrue, and no error anywhere. It is
/// also pure — no DbContext, no HTTP — so the test asserts behaviour rather than
/// asserting that a mock behaves like a mock.
/// </summary>
public class StockLedgerMappingTests
{
    private static StockMovement Movement(
        StockMovementType type,
        StockDirection direction = StockDirection.Out,
        string? sourceType = null,
        long? sourceId = null,
        long sourceLineId = 0,
        long movementId = 77) =>
        new()
        {
            StockMovementId = movementId,
            ItemId = 5,
            MovementType = type,
            Direction = direction,
            MovementDate = new DateOnly(2026, 8, 2),
            Quantity = 3m,
            TotalCost = 300m,
            SourceType = sourceType,
            SourceId = sourceId,
            SourceLineId = sourceLineId,
        };

    [Fact]
    public void A_sale_debits_cost_of_goods_sold_and_credits_inventory()
    {
        StockPosting posting = StockLedgerMapping.For(Movement(StockMovementType.Issue))!;

        // The direction of this pair is the whole feature. Reversed, stock grows
        // as it is sold and gross profit is negative.
        Assert.Equal(StockLedgerMapping.CostOfGoodsSold, posting.DebitAccountSystemName);
        Assert.Equal(StockLedgerMapping.Inventory, posting.CreditAccountSystemName);
    }

    [Fact]
    public void A_sales_return_reverses_that_pair()
    {
        StockPosting posting = StockLedgerMapping.For(
            Movement(StockMovementType.SalesReturn, StockDirection.In))!;

        Assert.Equal(StockLedgerMapping.Inventory, posting.DebitAccountSystemName);
        Assert.Equal(StockLedgerMapping.CostOfGoodsSold, posting.CreditAccountSystemName);
    }

    [Theory]
    [InlineData(StockMovementType.TransferIn, StockDirection.In)]
    [InlineData(StockMovementType.TransferOut, StockDirection.Out)]
    public void A_transfer_posts_nothing(StockMovementType type, StockDirection direction)
    {
        // Stock is one pool per branch, so a transfer changes where it is and
        // not what the branch owns. Posting it would move value between an
        // account and itself.
        Assert.Null(StockLedgerMapping.For(Movement(type, direction)));
    }

    [Theory]
    [InlineData(StockMovementType.Receipt, StockDirection.In)]
    [InlineData(StockMovementType.PurchaseReturn, StockDirection.Out)]
    public void Goods_against_a_purchase_document_are_left_to_that_document(
        StockMovementType type, StockDirection direction)
    {
        // The other leg is Accounts Payable, and only Purchase knows the vendor
        // and how much of the amount is tax. Posting the stock half here as well
        // would double the inventory asset.
        Assert.Null(StockLedgerMapping.For(
            Movement(type, direction, sourceType: "BIL", sourceId: 900, sourceLineId: 2)));
    }

    [Fact]
    public void A_receipt_with_no_document_behind_it_is_treated_as_an_assertion_of_stock()
    {
        StockPosting posting = StockLedgerMapping.For(
            Movement(StockMovementType.Receipt, StockDirection.In))!;

        Assert.Equal(StockLedgerMapping.Inventory, posting.DebitAccountSystemName);
        Assert.Equal(StockLedgerMapping.OpeningBalanceEquity, posting.CreditAccountSystemName);
        Assert.Equal("OPB", posting.TransactionTypeCode);
    }

    [Fact]
    public void Shrinkage_is_a_cost_of_goods_sold_expense()
    {
        StockPosting posting = StockLedgerMapping.For(
            Movement(StockMovementType.Adjustment, StockDirection.Out))!;

        Assert.Equal(StockLedgerMapping.CostOfGoodsSold, posting.DebitAccountSystemName);
        Assert.Equal(StockLedgerMapping.Inventory, posting.CreditAccountSystemName);
        Assert.Equal("STA", posting.TransactionTypeCode);
    }

    [Fact]
    public void An_upward_count_correction_runs_the_other_way()
    {
        StockPosting posting = StockLedgerMapping.For(
            Movement(StockMovementType.Adjustment, StockDirection.In))!;

        Assert.Equal(StockLedgerMapping.Inventory, posting.DebitAccountSystemName);
        Assert.Equal(StockLedgerMapping.CostOfGoodsSold, posting.CreditAccountSystemName);
    }

    [Fact]
    public void A_sourced_sale_files_under_the_document_and_its_own_line()
    {
        StockPosting posting = StockLedgerMapping.For(
            Movement(StockMovementType.Issue, sourceType: "INV", sourceId: 4321, sourceLineId: 7))!;

        // Under the invoice, not under the movement. That is what puts the cost
        // of a sale beside the revenue from it.
        Assert.Equal("INV", posting.TransactionTypeCode);
        Assert.Equal(4321, posting.TransactionId);
        Assert.Equal(7, posting.TransactionDetailId);
    }

    [Fact]
    public void An_unsourced_movement_is_its_own_document()
    {
        StockPosting posting = StockLedgerMapping.For(
            Movement(StockMovementType.Issue, movementId: 512))!;

        // There is nothing else to file it under, and using the movement id
        // keeps every posting traceable back to exactly one row.
        Assert.Equal("STA", posting.TransactionTypeCode);
        Assert.Equal(512, posting.TransactionId);
        Assert.Equal(512, posting.TransactionDetailId);
    }

    [Fact]
    public void Every_posting_names_two_different_accounts()
    {
        // A posting whose legs land on one account is a debit and a credit that
        // cancel: it balances, it commits, and it records nothing at all.
        foreach (StockMovementType type in Enum.GetValues<StockMovementType>())
        {
            foreach (StockDirection direction in Enum.GetValues<StockDirection>())
            {
                if (StockLedgerMapping.For(Movement(type, direction)) is not { } posting)
                {
                    continue;
                }

                Assert.NotEqual(posting.DebitAccountSystemName, posting.CreditAccountSystemName);
            }
        }
    }

    // --- Delivery challans (TK-90) ---

    [Fact]
    public void Goods_out_on_a_sale_challan_wait_in_goods_delivered_not_invoiced()
    {
        StockPosting posting = StockLedgerMapping.For(
            Movement(StockMovementType.Issue, sourceType: "DLC", sourceId: 31, sourceLineId: 4))!;

        // Not cost of sales yet: there is no revenue until the invoice, which
        // moves the cost on. Filed on the challan's own line and the COGS leg
        // type, the key the challan's provisional posting uses.
        Assert.Equal(StockLedgerMapping.GoodsDeliveredNotInvoiced, posting.DebitAccountSystemName);
        Assert.Equal(StockLedgerMapping.Inventory, posting.CreditAccountSystemName);
        Assert.Equal("DLC", posting.TransactionTypeCode);
        Assert.Equal(31, posting.TransactionId);
        Assert.Equal(4, posting.TransactionDetailId);
    }

    [Fact]
    public void An_invoices_issue_still_goes_straight_to_cost_of_goods_sold()
    {
        StockPosting posting = StockLedgerMapping.For(
            Movement(StockMovementType.Issue, sourceType: "INV", sourceId: 9, sourceLineId: 2))!;

        Assert.Equal(StockLedgerMapping.CostOfGoodsSold, posting.DebitAccountSystemName);
    }

    [Theory]
    [InlineData(StockMovementType.Issue, StockDirection.Out)]
    [InlineData(StockMovementType.Adjustment, StockDirection.Out)]
    public void A_movement_the_document_exempted_posts_nothing(StockMovementType type, StockDirection direction)
    {
        // A job-work, approval, transfer or sample challan: the goods are still
        // the branch's own, so nothing moves in the ledger.
        StockMovement movement = Movement(type, direction, sourceType: "DLC", sourceId: 31, sourceLineId: 4);
        movement.LedgerExempt = true;

        Assert.Null(StockLedgerMapping.For(movement));
    }
}
