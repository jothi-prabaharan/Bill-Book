using Shared.Kernel.Ordering;
using Xunit;

namespace Shared.Kernel.Tests;

/// <summary>
/// Drag-and-drop ordering. The interesting case is not the drag — it is what
/// happens when two neighbours have no room between them, because that is the
/// path that rewrites every row rather than one, and it is the path nobody
/// exercises by hand.
///
/// Three services still carry their own copy of this arithmetic (PLAN 5.11).
/// These tests are against the canonical one, so they are also the specification
/// the other two have to match when they are folded in.
/// </summary>
public class ReorderingTests
{
    private sealed class Row
    {
        public long Id { get; init; }

        public int Order { get; set; }
    }

    private static List<Row> Rows(params (long Id, int Order)[] rows) =>
        [.. rows.Select(r => new Row { Id = r.Id, Order = r.Order })];

    private static bool Apply(List<Row> rows, ReorderRequest request) =>
        Reordering.Apply(rows, request, r => r.Id, r => r.Order, (r, order) => r.Order = order);

    private static int OrderOf(List<Row> rows, long id) => rows.Single(r => r.Id == id).Order;

    [Fact]
    public void Apply_ReturnsFalseWhenTheMovedRowIsNotInTheList()
    {
        List<Row> rows = Rows((1, 10), (2, 20));

        bool moved = Apply(rows, new ReorderRequest { MovedId = 99, PreviousId = 1, NextId = 2 });

        Assert.False(moved);
        Assert.Equal(10, OrderOf(rows, 1));
        Assert.Equal(20, OrderOf(rows, 2));
    }

    [Fact]
    public void Apply_PutsTheRowMidwayBetweenItsNeighbours()
    {
        List<Row> rows = Rows((1, 10), (2, 20), (3, 30));

        Apply(rows, new ReorderRequest { MovedId = 3, PreviousId = 1, NextId = 2 });

        // Only the moved row is written. That is the reason for gaps of ten:
        // an ordinary drag is a one-row update, not a rewrite of the table.
        Assert.Equal(15, OrderOf(rows, 3));
        Assert.Equal(10, OrderOf(rows, 1));
        Assert.Equal(20, OrderOf(rows, 2));
    }

    [Fact]
    public void Apply_PutsARowDroppedAtTheBottomAfterTheLast()
    {
        List<Row> rows = Rows((1, 10), (2, 20), (3, 30));

        Apply(rows, new ReorderRequest { MovedId = 1, PreviousId = 3, NextId = null });

        Assert.Equal(40, OrderOf(rows, 1));
    }

    [Fact]
    public void Apply_PutsARowDroppedAtTheTopBeforeTheFirst()
    {
        List<Row> rows = Rows((1, 10), (2, 20), (3, 30));

        Apply(rows, new ReorderRequest { MovedId = 3, PreviousId = null, NextId = 1 });

        Assert.Equal(5, OrderOf(rows, 3));
    }

    [Fact]
    public void Apply_RenumbersEverythingWhenTheNeighboursAreAdjacent()
    {
        // 10 and 11 have no integer between them. This is the case that has to
        // rewrite the list, and getting it wrong leaves two rows sharing an
        // order — which shows as a list that reshuffles itself on reload.
        List<Row> rows = Rows((1, 10), (2, 11), (3, 30));

        Apply(rows, new ReorderRequest { MovedId = 3, PreviousId = 1, NextId = 2 });

        Assert.Equal(10, OrderOf(rows, 1));
        Assert.Equal(20, OrderOf(rows, 3));
        Assert.Equal(30, OrderOf(rows, 2));
    }

    [Fact]
    public void Apply_RenumbersWhenThereIsNoRoomAboveTheFirstRow()
    {
        // Nothing fits below 1, so dropping at the top has to renumber.
        List<Row> rows = Rows((1, 1), (2, 20), (3, 30));

        Apply(rows, new ReorderRequest { MovedId = 3, PreviousId = null, NextId = 1 });

        Assert.Equal(10, OrderOf(rows, 3));
        Assert.Equal(20, OrderOf(rows, 1));
        Assert.Equal(30, OrderOf(rows, 2));
    }

    [Fact]
    public void Apply_LeavesNoTwoRowsSharingAnOrderAfterARenumber()
    {
        List<Row> rows = Rows((1, 5), (2, 5), (3, 5), (4, 5));

        Apply(rows, new ReorderRequest { MovedId = 4, PreviousId = 1, NextId = 2 });

        Assert.Equal(4, rows.Select(r => r.Order).Distinct().Count());
    }

    [Fact]
    public void Apply_RenumbersInTensSoTheNextDragHasRoomAgain()
    {
        List<Row> rows = Rows((1, 10), (2, 11), (3, 30));

        Apply(rows, new ReorderRequest { MovedId = 3, PreviousId = 1, NextId = 2 });

        // A renumber that closed the gaps would force another renumber on the
        // very next drag, which is how a one-row update becomes a table rewrite
        // every time.
        Assert.Equal(new[] { 10, 20, 30 }, rows.OrderBy(r => r.Order).Select(r => r.Order).ToArray());
    }
}
