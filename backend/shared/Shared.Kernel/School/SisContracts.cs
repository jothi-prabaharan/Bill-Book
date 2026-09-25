using System.Net.Http.Json;
using Shared.Kernel.Tenancy;

namespace Shared.Kernel.School;

// What other School services ask Sis (TK-62 onward). Ids across services are
// unenforced (hard rule 8), so they are checked here before they are stored.
// Every call names the branch in its body and carries the internal key.

/// <summary>Do these ids exist in the branch? Any left null is not asked about.</summary>
public sealed class AcademicCheckRequest
{
    public Guid CustomerId { get; set; }

    public Guid OrgId { get; set; }

    public long? AcademicYearId { get; set; }

    public long? SchoolClassId { get; set; }

    public long? SectionId { get; set; }
}

public sealed class AcademicCheckResponse
{
    public bool YearExists { get; set; }

    public bool YearIsClosed { get; set; }

    public bool ClassExists { get; set; }

    public bool SectionExists { get; set; }

    /// <summary>Whether the section is the named class's, in the named year.</summary>
    public bool SectionMatches { get; set; }
}

/// <summary>
/// Create the student an application admits (S2, TK-62). Idempotent on
/// <see cref="SourceApplicationId"/>: admitting again returns the same student.
/// </summary>
public sealed class AdmitStudentRequest
{
    public Guid CustomerId { get; set; }

    public Guid OrgId { get; set; }

    public long SourceApplicationId { get; set; }

    public string FirstName { get; set; } = null!;

    public string? LastName { get; set; }

    public DateOnly DateOfBirth { get; set; }

    /// <summary>By name: Male, Female, Other, NotStated.</summary>
    public string Gender { get; set; } = "NotStated";

    public DateOnly AdmissionDate { get; set; }

    public long GuardianContactId { get; set; }

    /// <summary>By name: Father, Mother, Guardian, Other.</summary>
    public string GuardianRelationship { get; set; } = "Guardian";

    public long AcademicYearId { get; set; }

    /// <summary>Enrol straight into this section when given.</summary>
    public long? SectionId { get; set; }

    public int? RollNo { get; set; }
}

public sealed class AdmitStudentResponse
{
    public long StudentId { get; set; }

    public string AdmissionNo { get; set; } = null!;
}

/// <summary>Who is on a section's roll, and the school year it belongs to (S3, TK-63).</summary>
public sealed class SectionRollRequest
{
    public Guid CustomerId { get; set; }

    public Guid OrgId { get; set; }

    public long SectionId { get; set; }
}

public sealed class SectionRollResponse
{
    public bool SectionExists { get; set; }

    public DateOnly YearStart { get; set; }

    public DateOnly YearEnd { get; set; }

    public bool YearIsClosed { get; set; }

    public List<RollMember> Roll { get; set; } = [];
}

public sealed class RollMember
{
    public long EnrolmentId { get; set; }

    public long StudentId { get; set; }

    public string AdmissionNo { get; set; } = null!;

    public string FullName { get; set; } = null!;

    public int? RollNo { get; set; }
}

/// <summary>Sis refused the admit; <see cref="Message"/> is its sentence, safe to show.</summary>
public sealed class SisRefusedException(string message) : Exception(message);

public interface ISisClient
{
    Task<SectionRollResponse> RollAsync(long sectionId, CancellationToken ct);

    Task<AcademicCheckResponse> CheckAsync(long? academicYearId, long? schoolClassId, long? sectionId, CancellationToken ct);

    /// <summary>Throws <see cref="SisRefusedException"/> for a refusal and <see cref="HttpRequestException"/> when Sis cannot be asked.</summary>
    Task<AdmitStudentResponse> AdmitAsync(AdmitStudentRequest request, CancellationToken ct);
}

public sealed class HttpSisClient : ISisClient
{
    private readonly HttpClient _http;
    private readonly ITenantContext _tenant;

    public HttpSisClient(HttpClient http, ITenantContext tenant)
    {
        _http = http;
        _tenant = tenant;
    }

    public async Task<AcademicCheckResponse> CheckAsync(long? academicYearId, long? schoolClassId, long? sectionId, CancellationToken ct)
    {
        using HttpResponseMessage response = await _http.PostAsJsonAsync("internal/sis/academic-check", new AcademicCheckRequest
        {
            CustomerId = _tenant.CustomerId ?? Guid.Empty,
            OrgId = _tenant.OrgId ?? Guid.Empty,
            AcademicYearId = academicYearId,
            SchoolClassId = schoolClassId,
            SectionId = sectionId,
        }, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AcademicCheckResponse>(ct) ?? new AcademicCheckResponse();
    }

    public async Task<SectionRollResponse> RollAsync(long sectionId, CancellationToken ct)
    {
        using HttpResponseMessage response = await _http.PostAsJsonAsync("internal/sis/sections/roll", new SectionRollRequest
        {
            CustomerId = _tenant.CustomerId ?? Guid.Empty,
            OrgId = _tenant.OrgId ?? Guid.Empty,
            SectionId = sectionId,
        }, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SectionRollResponse>(ct) ?? new SectionRollResponse();
    }

    public async Task<AdmitStudentResponse> AdmitAsync(AdmitStudentRequest request, CancellationToken ct)
    {
        request.CustomerId = _tenant.CustomerId ?? Guid.Empty;
        request.OrgId = _tenant.OrgId ?? Guid.Empty;

        using HttpResponseMessage response = await _http.PostAsJsonAsync("internal/sis/students/admit", request, ct);
        if ((int)response.StatusCode is 409 or 422)
        {
            SisRefusal? refusal = await response.Content.ReadFromJsonAsync<SisRefusal>(ct);
            throw new SisRefusedException(refusal?.Message ?? "The student could not be admitted.");
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AdmitStudentResponse>(ct)
            ?? throw new HttpRequestException("Sis answered without a student.");
    }

    private sealed record SisRefusal(string? Message);
}
