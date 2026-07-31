using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Entities;

namespace Gateway.Entity.TableEntities;

/// <summary>
/// One proxied request through the gateway. Metadata only: no request or response
/// bodies are captured. Bodies here would carry GSTINs, contact details and, on the
/// auth routes, plaintext passwords, and they would dominate the table's size.
///
/// Lives in the master database under the <c>gwy</c> schema. The gateway routes for
/// every customer, so this table is deliberately cross-tenant and carries no
/// <c>OrgId</c> query filter; <see cref="OrgId"/> and <see cref="CustomerId"/> are
/// plain columns for filtering, not a security boundary.
/// </summary>
public class RequestLog : AuditableEntity
{
    public long RequestLogId { get; set; }

    /// <summary>
    /// When the gateway began handling the request. Distinct from
    /// <c>CreatedAt</c>, which the audit interceptor stamps when the batch is
    /// flushed and can lag by seconds.
    /// </summary>
    public DateTimeOffset StartedAt { get; set; }

    /// <summary>Ties gateway rows to downstream service logs for one request.</summary>
    [MaxLength(64, ErrorMessage = "Correlation id cannot exceed 64 characters.")]
    public string? CorrelationId { get; set; }

    [Required(ErrorMessage = "HTTP method is required.")]
    [MaxLength(10, ErrorMessage = "HTTP method cannot exceed 10 characters.")]
    public string Method { get; set; } = string.Empty;

    [Required(ErrorMessage = "Request path is required.")]
    [MaxLength(2048, ErrorMessage = "Request path cannot exceed 2048 characters.")]
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Query string without the leading '?'. Stored separately from the path so a
    /// route can be grouped on regardless of its parameters.
    /// </summary>
    [MaxLength(2048, ErrorMessage = "Query string cannot exceed 2048 characters.")]
    public string? QueryString { get; set; }

    public int StatusCode { get; set; }

    /// <summary>End-to-end time in the gateway, including the downstream call.</summary>
    public int DurationMs { get; set; }

    /// <summary>YARP cluster the request was routed to, null when nothing matched.</summary>
    [MaxLength(64, ErrorMessage = "Cluster id cannot exceed 64 characters.")]
    public string? ClusterId { get; set; }

    [MaxLength(64, ErrorMessage = "Route id cannot exceed 64 characters.")]
    public string? RouteId { get; set; }

    [MaxLength(300, ErrorMessage = "Destination cannot exceed 300 characters.")]
    public string? Destination { get; set; }

    /// <summary>From the JWT when present. Anonymous routes leave these null.</summary>
    public Guid? CustomerId { get; set; }

    public Guid? OrgId { get; set; }

    public Guid? UserId { get; set; }

    /// <summary>45 characters covers an IPv4-mapped IPv6 address.</summary>
    [MaxLength(45, ErrorMessage = "Client IP cannot exceed 45 characters.")]
    public string? ClientIp { get; set; }

    [MaxLength(300, ErrorMessage = "User agent cannot exceed 300 characters.")]
    public string? UserAgent { get; set; }

    public long RequestBytes { get; set; }

    public long ResponseBytes { get; set; }

    /// <summary>
    /// Exception type and message when the gateway itself failed, for example a
    /// downstream connection refused. Never a stack trace and never a body.
    /// </summary>
    [MaxLength(500, ErrorMessage = "Error cannot exceed 500 characters.")]
    public string? Error { get; set; }
}
