using System.ComponentModel.DataAnnotations;
using Sales.Entity.Enums;

namespace Sales.Entity.Models;

/// <summary>Book or change a table booking (TK-165).</summary>
public sealed class SaveReservationRequest
{
    [Required(ErrorMessage = "Enter the guest's name.")]
    [MaxLength(100, ErrorMessage = "A guest's name can be at most 100 characters.")]
    public string GuestName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter a phone number to reach the guest.")]
    [MaxLength(30, ErrorMessage = "A phone number can be at most 30 characters.")]
    public string Phone { get; set; } = string.Empty;

    [Range(1, 500, ErrorMessage = "A party is between 1 and 500 guests.")]
    public int PartySize { get; set; } = 2;

    /// <summary>When the party is expected, in branch local time.</summary>
    public DateTime ReservedFor { get; set; }

    [Range(15, 720, ErrorMessage = "A booking holds a table for between 15 minutes and 12 hours.")]
    public int DurationMinutes { get; set; } = 90;

    public long? RestaurantTableId { get; set; }

    [MaxLength(250, ErrorMessage = "A note can be at most 250 characters.")]
    public string? Notes { get; set; }
}

/// <summary>Seat a booking or a waiting party: the table, when it is not the one booked, and the waiter if the branch asks for one.</summary>
public sealed class SeatPartyRequest
{
    public long? RestaurantTableId { get; set; }

    public Guid? WaiterUserId { get; set; }
}

public sealed class ReservationView
{
    public long TableReservationId { get; set; }

    public string GuestName { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;

    public int PartySize { get; set; }

    public DateTime ReservedFor { get; set; }

    public int DurationMinutes { get; set; }

    public long? RestaurantTableId { get; set; }

    public string? TableCode { get; set; }

    public ReservationStatus Status { get; set; }

    public long? RestaurantOrderId { get; set; }

    public DateTime? SeatedAt { get; set; }

    public string? Notes { get; set; }
}

/// <summary>Add a party to the waiting list (TK-165).</summary>
public sealed class AddWaitingPartyRequest
{
    [Required(ErrorMessage = "Enter the guest's name.")]
    [MaxLength(100, ErrorMessage = "A guest's name can be at most 100 characters.")]
    public string GuestName { get; set; } = string.Empty;

    [MaxLength(30, ErrorMessage = "A phone number can be at most 30 characters.")]
    public string? Phone { get; set; }

    [Range(1, 500, ErrorMessage = "A party is between 1 and 500 guests.")]
    public int PartySize { get; set; } = 2;

    [MaxLength(250, ErrorMessage = "A note can be at most 250 characters.")]
    public string? Notes { get; set; }
}

public sealed class WaitingPartyView
{
    public long WaitingListEntryId { get; set; }

    public string GuestName { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public int PartySize { get; set; }

    public DateTime JoinedAt { get; set; }

    public WaitingStatus Status { get; set; }

    public long? RestaurantTableId { get; set; }

    public string? TableCode { get; set; }

    public long? RestaurantOrderId { get; set; }

    public DateTime? ClosedAt { get; set; }

    public string? Notes { get; set; }
}

/// <summary>A party seated: the order opened for them.</summary>
public sealed class SeatedParty
{
    public long RestaurantOrderId { get; set; }

    public long RestaurantTableId { get; set; }
}
