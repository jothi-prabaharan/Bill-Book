namespace Shared.Kernel.Ordering;

/// <summary>
/// Where a dragged row was dropped: what it landed between.
///
/// A request model in <c>Shared.Kernel</c>, which is a deliberate exception to
/// keeping those in <c>{Module}.Entity/Models</c>. Five services had their own
/// identical copy and two of them had already drifted, which is the failure the
/// convention exists to prevent rather than an example of it working. Three
/// fields with no module-specific meaning are not worth five definitions.
/// </summary>
public class ReorderRequest
{
    public long MovedId { get; set; }

    /// <summary>The row now above it, or null when it was dropped at the top.</summary>
    public long? PreviousId { get; set; }

    /// <summary>The row now below it, or null when it was dropped at the bottom.</summary>
    public long? NextId { get; set; }
}

/// <summary>
/// Applies a drag-and-drop drop to a list's display order.
///
/// <b>The only copy.</b> Five services had their own — two verbatim duplicates
/// and three hand-rolled — and two of them had already drifted on the
/// drop-at-the-top threshold, so the same drag produced a different order
/// depending on which screen it happened on. That is what each copy of the gap
/// arithmetic buys: another chance to get the renumber case wrong, and no way to
/// notice.
///
/// The moved row normally lands midway between its new neighbours, touching one
/// row. Only when the neighbours have no room between them is the whole list
/// renumbered in tens.
/// </summary>
public static class Reordering
{
    public const int Gap = 10;

    /// <param name="ordered">The list in its current display order.</param>
    /// <returns>False when the moved id is not in the list.</returns>
    public static bool Apply<T>(
        List<T> ordered,
        ReorderRequest request,
        Func<T, long> idOf,
        Func<T, int> orderOf,
        Action<T, int> setOrder)
    {
        T? moved = ordered.FirstOrDefault(x => idOf(x) == request.MovedId);
        if (moved is null)
        {
            return false;
        }

        int? above = OrderOf(ordered, request.PreviousId, idOf, orderOf);
        int? below = OrderOf(ordered, request.NextId, idOf, orderOf);

        if (above is int a && below is int b && b - a >= 2)
        {
            setOrder(moved, a + ((b - a) / 2));
            return true;
        }

        if (above is int last && below is null)
        {
            setOrder(moved, last + Gap);
            return true;
        }

        if (above is null && below is int first && first > 1)
        {
            setOrder(moved, first / 2);
            return true;
        }

        Renumber(ordered, moved, request, idOf, setOrder);
        return true;
    }

    private static void Renumber<T>(
        List<T> ordered, T moved, ReorderRequest request, Func<T, long> idOf, Action<T, int> setOrder)
    {
        List<T> rest = ordered.Where(x => !ReferenceEquals(x, moved)).ToList();

        int insertAt;
        if (request.NextId is long nextId)
        {
            int index = rest.FindIndex(x => idOf(x) == nextId);
            insertAt = index < 0 ? rest.Count : index;
        }
        else if (request.PreviousId is long previousId)
        {
            int index = rest.FindIndex(x => idOf(x) == previousId);
            insertAt = index < 0 ? rest.Count : index + 1;
        }
        else
        {
            insertAt = 0;
        }

        rest.Insert(insertAt, moved);

        for (int i = 0; i < rest.Count; i++)
        {
            setOrder(rest[i], (i + 1) * Gap);
        }
    }

    private static int? OrderOf<T>(
        List<T> ordered, long? id, Func<T, long> idOf, Func<T, int> orderOf)
    {
        if (id is not long value)
        {
            return null;
        }

        T? row = ordered.FirstOrDefault(x => idOf(x) == value);
        return row is null ? null : orderOf(row);
    }
}
