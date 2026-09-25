using Employee.Entity.Enums;
using Employee.Entity.TableEntities;
using Employee.Repository;
using Employee.Repository.SeedData;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Employees;
using Shared.Kernel.Numbering;

namespace Employee.Api.Services;

/// <summary>
/// The employee as other services see them (TK-49): leave and attendance read
/// the grade, location and reporting line to pick a policy and a chain. No
/// statutory numbers, no pay, no bank details ever leave by this route.
/// </summary>
public sealed class EmployeeProfileService
{
    private readonly EmployeeDbContext _db;
    private readonly INumberGenerator _numbers;

    public EmployeeProfileService(EmployeeDbContext db, INumberGenerator numbers)
    {
        _db = db;
        _numbers = numbers;
    }

    public async Task<List<EmployeeProfile>> FindAsync(EmployeeLookupRequest request, CancellationToken ct)
    {
        IQueryable<Employee.Entity.TableEntities.EmployeeRecord> query = _db.Employees.AsNoTracking();

        if (request.EmployeeId is long id)
        {
            query = query.Where(e => e.EmployeeId == id);
        }
        else if (request.UserId is Guid user)
        {
            query = query.Where(e => e.UserId == user);
        }
        else if (request.EmployeeIds.Count > 0)
        {
            query = query.Where(e => request.EmployeeIds.Contains(e.EmployeeId));
        }
        else
        {
            query = query.Where(e => e.EmployeeStatus != EmployeeStatus.Exited);
        }

        return await query
            .OrderBy(e => e.EmployeeCode)
            .Select(e => new EmployeeProfile
            {
                EmployeeId = e.EmployeeId,
                EmployeeCode = e.EmployeeCode,
                FullName = e.FirstName + (e.LastName == null ? "" : " " + e.LastName),
                UserId = e.UserId,
                DepartmentId = e.DepartmentId,
                GradeId = e.GradeId,
                WorkLocationId = e.WorkLocationId,
                ReportsToEmployeeId = e.ReportsToEmployeeId,
                JoiningDate = e.JoiningDate,
                ProbationEndDate = e.ProbationEndDate,
                ExitDate = e.ExitDate,
                Gender = e.Gender.ToString(),
                EmployeeStatus = e.EmployeeStatus.ToString(),
            })
            .ToListAsync(ct);
    }

    /// <summary>
    /// Creates or finds an onboarding employee from an accepted offer (TK-57).
    /// Idempotent: matching personal or work email returns the existing employee.
    /// </summary>
    public async Task<OnboardEmployeeResult> OnboardAsync(OnboardEmployeeRequest request, CancellationToken ct)
    {
        // 1. Idempotency check: see if employee already exists by email
        var existing = await _db.Employees
            .FirstOrDefaultAsync(e => (e.PersonalEmail != null && e.PersonalEmail.ToLower() == request.Email.ToLower())
                                   || (e.WorkEmail != null && e.WorkEmail.ToLower() == request.Email.ToLower()), ct);

        if (existing is not null)
        {
            return new OnboardEmployeeResult
            {
                EmployeeId = existing.EmployeeId,
                EmployeeCode = existing.EmployeeCode,
                AlreadyExisted = true,
            };
        }

        // 2. Allocate employee code from EMP series
        var alloc = await _numbers.NextAsync(EmployeeSeed.EmployeeSeriesCode, request.JoiningDate, ct);

        var grade = await _db.Grades.FirstOrDefaultAsync(g => g.GradeId == request.GradeId, ct);
        int notice = grade?.NoticePeriodDays ?? 30;

        _ = Enum.TryParse<EmploymentType>(request.EmploymentType, true, out var employmentType);

        var employee = new Employee.Entity.TableEntities.EmployeeRecord
        {
            EmployeeCode = alloc.Code,
            FirstName = request.FirstName,
            LastName = request.LastName,
            PersonalEmail = request.Email,
            Phone = request.Phone,
            DepartmentId = request.DepartmentId,
            DesignationId = request.DesignationId,
            GradeId = request.GradeId,
            WorkLocationId = request.WorkLocationId,
            JoiningDate = request.JoiningDate,
            NoticePeriodDays = notice > 0 ? notice : 30,
            EmploymentType = employmentType != 0 ? employmentType : EmploymentType.Permanent,
            EmployeeStatus = EmployeeStatus.Onboarding,
        };

        _db.Employees.Add(employee);
        await _db.SaveChangesAsync(ct);

        _db.EmploymentHistories.Add(new EmploymentHistory
        {
            EmployeeId = employee.EmployeeId,
            EffectiveDate = request.JoiningDate,
            ChangeKind = EmploymentChangeKind.Joined,
            DepartmentId = employee.DepartmentId,
            DesignationId = employee.DesignationId,
            GradeId = employee.GradeId,
            WorkLocationId = employee.WorkLocationId,
            Remarks = request.Remarks ?? $"Onboarded from Offer #{request.OfferId}",
        });
        await _db.SaveChangesAsync(ct);

        // 3. Create onboarding checklist if template exists
        var template = await _db.ChecklistTemplates.Include(t => t.Items)
            .FirstOrDefaultAsync(t => t.Kind == ChecklistKind.Onboarding, ct);

        if (template is not null && template.Items.Count > 0)
        {
            var checklist = new EmployeeChecklist
            {
                EmployeeId = employee.EmployeeId,
                Kind = ChecklistKind.Onboarding,
                ChecklistTemplateId = template.ChecklistTemplateId,
                Items = template.Items.OrderBy(i => i.SortOrder).Select(i => new EmployeeChecklistItem
                {
                    Title = i.Title,
                    OwnerRole = i.OwnerRole,
                    IsDone = false,
                }).ToList(),
            };
            _db.EmployeeChecklists.Add(checklist);
            await _db.SaveChangesAsync(ct);
        }

        return new OnboardEmployeeResult
        {
            EmployeeId = employee.EmployeeId,
            EmployeeCode = employee.EmployeeCode,
            AlreadyExisted = false,
        };
    }
}
