using Employee.Entity.Enums;
using Employee.Entity.Models;
using Employee.Entity.TableEntities;
using Employee.Repository;
using Microsoft.EntityFrameworkCore;

namespace Employee.Api.Services;

public sealed class LifecycleService
{
    private readonly EmployeeDbContext _db;
    private readonly IMasterUserClient _masterUsers;

    public LifecycleService(EmployeeDbContext db, IMasterUserClient masterUsers)
    {
        _db = db;
        _masterUsers = masterUsers;
    }

    // ---- Checklist Templates ------------------------------------------------
    public async Task<List<ChecklistTemplate>> ListTemplatesAsync(ChecklistKind? kind, CancellationToken ct)
    {
        var query = _db.ChecklistTemplates.AsNoTracking().Include(t => t.Items).AsQueryable();
        if (kind.HasValue)
        {
            query = query.Where(t => t.Kind == kind.Value);
        }
        return await query.ToListAsync(ct);
    }

    public async Task<long> SaveTemplateAsync(long? id, SaveChecklistTemplateRequest req, CancellationToken ct)
    {
        if (id is null)
        {
            var template = new ChecklistTemplate
            {
                Name = req.Name,
                Kind = req.Kind,
                Items = req.Items.Select(i => new ChecklistTemplateItem
                {
                    Title = i.Title,
                    OwnerRole = i.OwnerRole,
                    SortOrder = i.SortOrder
                }).ToList()
            };
            _db.ChecklistTemplates.Add(template);
            await _db.SaveChangesAsync(ct);
            return template.ChecklistTemplateId;
        }

        var existing = await _db.ChecklistTemplates.Include(t => t.Items).FirstOrDefaultAsync(t => t.ChecklistTemplateId == id.Value, ct)
            ?? throw new KeyNotFoundException($"Checklist template {id} not found.");

        existing.Name = req.Name;
        existing.Kind = req.Kind;
        _db.ChecklistTemplateItems.RemoveRange(existing.Items);
        existing.Items = req.Items.Select(i => new ChecklistTemplateItem
        {
            Title = i.Title,
            OwnerRole = i.OwnerRole,
            SortOrder = i.SortOrder
        }).ToList();

        await _db.SaveChangesAsync(ct);
        return existing.ChecklistTemplateId;
    }

    // ---- Employee Checklists ------------------------------------------------
    public async Task<EmployeeChecklist?> GetEmployeeChecklistAsync(long employeeId, ChecklistKind kind, CancellationToken ct) =>
        await _db.EmployeeChecklists.AsNoTracking()
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.EmployeeId == employeeId && c.Kind == kind, ct);

    public async Task<long> CreateEmployeeChecklistAsync(CreateEmployeeChecklistRequest req, CancellationToken ct)
    {
        var existing = await _db.EmployeeChecklists.FirstOrDefaultAsync(c => c.EmployeeId == req.EmployeeId && c.Kind == req.Kind, ct);
        if (existing is not null)
        {
            return existing.EmployeeChecklistId;
        }

        var checklist = new EmployeeChecklist
        {
            EmployeeId = req.EmployeeId,
            Kind = req.Kind,
            ChecklistTemplateId = req.ChecklistTemplateId,
        };

        if (req.ChecklistTemplateId.HasValue)
        {
            var template = await _db.ChecklistTemplates.Include(t => t.Items)
                .FirstOrDefaultAsync(t => t.ChecklistTemplateId == req.ChecklistTemplateId.Value, ct);

            if (template is not null)
            {
                checklist.Items = template.Items.OrderBy(i => i.SortOrder).Select(i => new EmployeeChecklistItem
                {
                    Title = i.Title,
                    OwnerRole = i.OwnerRole,
                    IsDone = false
                }).ToList();
            }
        }

        _db.EmployeeChecklists.Add(checklist);
        await _db.SaveChangesAsync(ct);
        return checklist.EmployeeChecklistId;
    }

    public async Task UpdateChecklistItemAsync(long itemId, UpdateChecklistItemRequest req, CancellationToken ct)
    {
        var item = await _db.EmployeeChecklistItems.FirstOrDefaultAsync(i => i.EmployeeChecklistItemId == itemId, ct)
            ?? throw new KeyNotFoundException($"Checklist item {itemId} not found.");

        item.IsDone = req.IsDone;
        item.DoneDate = req.IsDone ? DateOnly.FromDateTime(DateTime.UtcNow) : null;
        item.Remarks = req.Remarks;

        await _db.SaveChangesAsync(ct);
    }

    // ---- Separation ---------------------------------------------------------
    public async Task<Separation?> GetSeparationAsync(long employeeId, CancellationToken ct) =>
        await _db.Separations.AsNoTracking()
            .OrderByDescending(s => s.RequestDate)
            .FirstOrDefaultAsync(s => s.EmployeeId == employeeId, ct);

    public async Task<long> SubmitSeparationAsync(SaveSeparationRequest req, CancellationToken ct)
    {
        var employee = await _db.Employees.FirstOrDefaultAsync(e => e.EmployeeId == req.EmployeeId, ct)
            ?? throw new KeyNotFoundException($"Employee {req.EmployeeId} not found.");

        int noticeDays = employee.NoticePeriodDays > 0 ? employee.NoticePeriodDays : 30;
        DateOnly expectedLwd = req.RequestDate.AddDays(noticeDays);
        decimal shortfall = 0m;
        if (!req.IsNoticeWaived && req.LastWorkingDate < expectedLwd)
        {
            shortfall = expectedLwd.DayNumber - req.LastWorkingDate.DayNumber;
        }

        var separation = new Separation
        {
            EmployeeId = req.EmployeeId,
            Kind = req.Kind,
            RequestDate = req.RequestDate,
            LastWorkingDate = req.LastWorkingDate,
            NoticeShortfallDays = shortfall,
            IsNoticeWaived = req.IsNoticeWaived,
            Reason = req.Reason,
            ExitInterviewNotes = req.ExitInterviewNotes,
            Status = SeparationStatus.Submitted
        };

        employee.EmployeeStatus = EmployeeStatus.OnNotice;

        _db.Separations.Add(separation);
        await _db.SaveChangesAsync(ct);
        return separation.SeparationId;
    }

    public async Task ApproveSeparationAsync(long separationId, CancellationToken ct)
    {
        var sep = await _db.Separations.FirstOrDefaultAsync(s => s.SeparationId == separationId, ct)
            ?? throw new KeyNotFoundException($"Separation {separationId} not found.");

        sep.Status = SeparationStatus.Approved;
        await _db.SaveChangesAsync(ct);
    }

    public async Task ClearSeparationAsync(long separationId, CancellationToken ct)
    {
        var sep = await _db.Separations.FirstOrDefaultAsync(s => s.SeparationId == separationId, ct)
            ?? throw new KeyNotFoundException($"Separation {separationId} not found.");

        sep.Status = SeparationStatus.Cleared;
        await _db.SaveChangesAsync(ct);
    }

    public async Task SettleSeparationAsync(long employeeId, DateOnly? lastWorkingDate, CancellationToken ct)
    {
        var employee = await _db.Employees.FirstOrDefaultAsync(e => e.EmployeeId == employeeId, ct)
            ?? throw new KeyNotFoundException($"Employee {employeeId} not found.");

        var sep = await _db.Separations
            .Where(s => s.EmployeeId == employeeId && s.Status != SeparationStatus.Withdrawn)
            .OrderByDescending(s => s.RequestDate)
            .FirstOrDefaultAsync(ct);

        DateOnly exitDate = lastWorkingDate ?? (sep?.LastWorkingDate ?? DateOnly.FromDateTime(DateTime.UtcNow));

        if (sep is not null)
        {
            sep.Status = SeparationStatus.Settled;
            sep.LastWorkingDate = exitDate;
        }

        employee.EmployeeStatus = EmployeeStatus.Exited;
        employee.ExitDate = exitDate;

        _db.EmploymentHistories.Add(new EmploymentHistory
        {
            EmployeeId = employeeId,
            ChangeKind = EmploymentChangeKind.Exit,
            EffectiveDate = exitDate,
            Remarks = "Full and final settlement completed."
        });

        await _db.SaveChangesAsync(ct);

        // Deactivate linked Master user login
        if (employee.UserId.HasValue)
        {
            await _masterUsers.DeactivateUserAsync(employee.UserId.Value, ct);
        }
    }
}
