using Sales.Entity.Enums;
using Shared.Kernel.Validation;

namespace Sales.Repository.Repositories;

/// <summary>
/// The pure rules of table bookings and the waiting list (TK-165): when two
/// bookings clash, which states may still change, and which waiting party a
/// table that came free should take.
/// </summary>
public static class TableReservationRulesRepository
{
    /// <summary>
    /// True when two bookings for one table overlap. A booking covers
    /// [start, start + minutes): one ending at 8:00 and one starting at 8:00
    /// do not clash, so a table can be turned at the minute.
    /// </summary>
    public static bool Overlaps(DateTime start, int minutes, DateTime otherStart, int otherMinutes) =>
        start < otherStart.AddMinutes(otherMinutes) && otherStart < start.AddMinutes(minutes);

    /// <summary>A booking that still holds its table: booked, or seated and not yet over.</summary>
    public static bool HoldsTable(ReservationStatus status) =>
        status is ReservationStatus.Booked or ReservationStatus.Seated;

    /// <summary>Only a booking still booked can be changed, cancelled, marked a no-show or seated.</summary>
    public static string? ChangeRefusal(ReservationStatus status) => status switch
    {
        ReservationStatus.Booked => null,
        ReservationStatus.Seated => "This party has already been seated.",
        ReservationStatus.NoShow => "This booking was marked a no-show.",
        _ => "This booking was cancelled.",
    };

    /// <summary>Only a party still waiting can be seated or taken off the list.</summary>
    public static string? WaitingRefusal(WaitingStatus status) => status switch
    {
        WaitingStatus.Waiting => null,
        WaitingStatus.Seated => "This party has already been seated.",
        _ => "This party has already left the list.",
    };

    /// <summary>
    /// The party a table seating <paramref name="seats"/> should take: the one
    /// waiting longest that fits. A party too large waits for a bigger table
    /// rather than holding up the ones behind it.
    /// </summary>
    public static T? NextThatFits<T>(IEnumerable<T> waiting, int seats, Func<T, int> partySize, Func<T, DateTime> joinedAt)
        where T : class =>
        waiting.Where(w => partySize(w) <= seats).OrderBy(joinedAt).FirstOrDefault();

    /// <summary>A phone as the convention stores it: trimmed, spaces and dashes out, a leading + kept for a foreign number.</summary>
    public static string? CleanPhone(string? phone)
    {
        string? trimmed = PhoneNumbers.NormalizeOptional(phone);
        if (trimmed is null)
        {
            return null;
        }

        string digits = new([.. trimmed.Where(c => c != ' ' && c != '-' && c != '(' && c != ')')]);
        return digits.Length == 0 ? null : digits;
    }

    /// <summary>The note an order opened from a booking or the waiting list carries, so the floor knows whose table it is.</summary>
    public static string SeatingNote(string kind, string guestName, int partySize, string? notes)
    {
        string note = $"{kind}: {guestName.Trim()}, {partySize}";
        if (!string.IsNullOrWhiteSpace(notes))
        {
            note += $" · {notes.Trim()}";
        }

        return note.Length <= AggregatorOrderRulesRepository.NoteLimit ? note : note[..AggregatorOrderRulesRepository.NoteLimit];
    }
}
