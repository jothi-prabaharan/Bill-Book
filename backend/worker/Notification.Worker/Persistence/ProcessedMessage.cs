using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Entities;

namespace Notification.Worker.Persistence;

/// <summary>
/// One message the worker has taken on, keyed by its message id (TK-19).
///
/// Service Bus delivers at least once, so the same request can arrive twice —
/// after a lock expired mid-send, or a completion that never reached the broker.
/// A row here is the worker's claim on a message: <see cref="SentAt"/> set means
/// it went out and a redelivery is completed without sending; unset means a send
/// is in flight, or died, and <see cref="ClaimedAt"/> says how long ago.
/// </summary>
public class ProcessedMessage : AuditableEntity
{
    [Required(ErrorMessage = "Message id is required.")]
    [MaxLength(64, ErrorMessage = "Message id cannot exceed 64 characters.")]
    public string MessageId { get; set; } = null!;

    [Required(ErrorMessage = "Topic is required.")]
    [MaxLength(100, ErrorMessage = "Topic cannot exceed 100 characters.")]
    public string Topic { get; set; } = null!;

    public DateTimeOffset ClaimedAt { get; set; }

    public DateTimeOffset? SentAt { get; set; }
}
