using System.ComponentModel.DataAnnotations;
using Sales.Entity.Enums;
using Shared.Kernel.Tenancy;

namespace Sales.Entity.TableEntities;

/// <summary>
/// A table held for a party at a later time (TK-165). A booking covers
/// <see cref="ReservedFor"/> for <see cref="DurationMinutes"/>; two live
/// bookings for one table may not overlap. Seating it opens an order on the
/// table and keeps the order's id here.
/// </summary>
public class TableReservation : OrgScopedEntity
{
    public long TableReservationId { get; set; }

    [Required(ErrorMessage = "Enter the guest's name.")]
    [MaxLength(100, ErrorMessage = "A guest's name can be at most 100 characters.")]
    public string GuestName { get; set; } = null!;

    /// <summary>Local numbers without a prefix, foreign ones with a leading +.</summary>
    [Required(ErrorMessage = "Enter a phone number to reach the guest.")]
    [MaxLength(30, ErrorMessage = "A phone number can be at most 30 characters.")]
    public string Phone { get; set; } = null!;

    [Range(1, 500, ErrorMessage = "A party is between 1 and 500 guests.")]
    public int PartySize { get; set; }

    /// <summary>When the party is expected, in branch local time.</summary>
    public DateTime ReservedFor { get; set; }

    /// <summary>How long the table is held from <see cref="ReservedFor"/>.</summary>
    [Range(15, 720, ErrorMessage = "A booking holds a table for between 15 minutes and 12 hours.")]
    public int DurationMinutes { get; set; } = 90;

    /// <summary>The table held; null for a booking not yet given one.</summary>
    public long? RestaurantTableId { get; set; }

    public ReservationStatus Status { get; set; } = ReservationStatus.Booked;

    /// <summary>The order opened when the party was seated.</summary>
    public long? RestaurantOrderId { get; set; }

    public DateTime? SeatedAt { get; set; }

    [MaxLength(250, ErrorMessage = "A note can be at most 250 characters.")]
    public string? Notes { get; set; }
}

/// <summary>
/// A party waiting for a table now (TK-165). Taken in the order they joined,
/// except that a party too large for the table that came free waits for a
/// bigger one.
/// </summary>
public class WaitingListEntry : OrgScopedEntity
{
    public long WaitingListEntryId { get; set; }

    [Required(ErrorMessage = "Enter the guest's name.")]
    [MaxLength(100, ErrorMessage = "A guest's name can be at most 100 characters.")]
    public string GuestName { get; set; } = null!;

    [MaxLength(30, ErrorMessage = "A phone number can be at most 30 characters.")]
    public string? Phone { get; set; }

    [Range(1, 500, ErrorMessage = "A party is between 1 and 500 guests.")]
    public int PartySize { get; set; }

    /// <summary>When the party joined the list, in branch local time.</summary>
    public DateTime JoinedAt { get; set; }

    public WaitingStatus Status { get; set; } = WaitingStatus.Waiting;

    public long? RestaurantTableId { get; set; }

    public long? RestaurantOrderId { get; set; }

    /// <summary>When the party was seated or left.</summary>
    public DateTime? ClosedAt { get; set; }

    [MaxLength(250, ErrorMessage = "A note can be at most 250 characters.")]
    public string? Notes { get; set; }
}
