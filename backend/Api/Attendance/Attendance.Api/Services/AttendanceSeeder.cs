namespace Attendance.Api.Services;

/// <summary>
/// Attendance has no master data of its own (S3, TK-63): the roll is Student's.
/// The seed endpoint exists so every School service answers Master's fan-out
/// alike, and reports nothing seeded.
/// </summary>
public sealed class AttendanceSeeder
{
    public Task<Dictionary<string, int>> SeedForOrganizationAsync(Guid orgId, CancellationToken ct) =>
        Task.FromResult(new Dictionary<string, int>());
}
