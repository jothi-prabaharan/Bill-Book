namespace Shared.Kernel.Errors;

/// <summary>
/// Writes the failure to the service's own <c>ErrorLogs</c> table and hands back
/// the reference the caller is given.
///
/// Returns null when the row could not be written, which is not a failure of
/// this method — it is the documented consequence of the table being tenant
/// scoped. An error raised before the tenant is known has nowhere to go but
/// <c>ILogger</c>, and the caller then gets a response with no reference on it.
/// This method never throws: an error handler that can itself fail is an error
/// handler that turns a 409 into a 500.
/// </summary>
public interface IErrorLogStore
{
    Task<Guid?> RecordAsync(ErrorLog entry, CancellationToken ct);
}
