using Microsoft.EntityFrameworkCore;
using Payroll.Entity.Models;
using Payroll.Entity.TableEntities;
using Payroll.Repository;

namespace Payroll.Api.Services;

public sealed class SalarySetupService
{
    private readonly PayrollDbContext _db;

    public SalarySetupService(PayrollDbContext db) => _db = db;

    // ---- Salary Components --------------------------------------------------

    public async Task<IReadOnlyList<SalaryComponentView>> ListComponentsAsync(CancellationToken ct) =>
        await _db.SalaryComponents.AsNoTracking()
            .OrderBy(c => c.Kind).ThenBy(c => c.Name)
            .Select(c => new SalaryComponentView
            {
                SalaryComponentId = c.SalaryComponentId,
                Name = c.Name,
                Kind = c.Kind,
                ValueType = c.ValueType,
                IsTaxable = c.IsTaxable,
                Formula = c.Formula,
                LedgerAccountId = c.LedgerAccountId
            })
            .ToListAsync(ct);

    public async Task<SalaryComponentView?> GetComponentAsync(long id, CancellationToken ct)
    {
        SalaryComponent? c = await _db.SalaryComponents.AsNoTracking()
            .FirstOrDefaultAsync(x => x.SalaryComponentId == id, ct);
        return c is null ? null : new SalaryComponentView
        {
            SalaryComponentId = c.SalaryComponentId,
            Name = c.Name,
            Kind = c.Kind,
            ValueType = c.ValueType,
            IsTaxable = c.IsTaxable,
            Formula = c.Formula,
            LedgerAccountId = c.LedgerAccountId
        };
    }

    public async Task<long> SaveComponentAsync(long? id, SaveSalaryComponentRequest request, CancellationToken ct)
    {
        SalaryComponent component;
        if (id is null)
        {
            component = new SalaryComponent();
            _db.SalaryComponents.Add(component);
        }
        else
        {
            component = await _db.SalaryComponents.FirstOrDefaultAsync(x => x.SalaryComponentId == id.Value, ct)
                ?? throw new KeyNotFoundException($"Salary component {id} not found.");
        }

        component.Name = request.Name;
        component.Kind = request.Kind;
        component.ValueType = request.ValueType;
        component.IsTaxable = request.IsTaxable;
        component.Formula = request.Formula;
        component.LedgerAccountId = request.LedgerAccountId;

        await _db.SaveChangesAsync(ct);
        return component.SalaryComponentId;
    }

    public async Task<bool> DeleteComponentAsync(long id, CancellationToken ct)
    {
        SalaryComponent? component = await _db.SalaryComponents.FirstOrDefaultAsync(x => x.SalaryComponentId == id, ct);
        if (component is null) return false;

        _db.SalaryComponents.Remove(component);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ---- Salary Structures --------------------------------------------------

    public async Task<IReadOnlyList<SalaryStructureView>> ListStructuresAsync(CancellationToken ct) =>
        await _db.SalaryStructures.AsNoTracking()
            .Include(s => s.Components).ThenInclude(sc => sc.Component)
            .OrderBy(s => s.Name)
            .Select(s => new SalaryStructureView
            {
                SalaryStructureId = s.SalaryStructureId,
                Name = s.Name,
                Components = s.Components.Select(sc => new SalaryStructureComponentView
                {
                    SalaryStructureComponentId = sc.SalaryStructureComponentId,
                    SalaryComponentId = sc.SalaryComponentId,
                    ComponentName = sc.Component.Name,
                    Kind = sc.Component.Kind,
                    ValueType = sc.ValueType,
                    FlatAmount = sc.FlatAmount,
                    Percentage = sc.Percentage
                }).ToList()
            })
            .ToListAsync(ct);

    public async Task<SalaryStructureView?> GetStructureAsync(long id, CancellationToken ct)
    {
        SalaryStructure? s = await _db.SalaryStructures.AsNoTracking()
            .Include(x => x.Components).ThenInclude(sc => sc.Component)
            .FirstOrDefaultAsync(x => x.SalaryStructureId == id, ct);

        if (s is null) return null;

        return new SalaryStructureView
        {
            SalaryStructureId = s.SalaryStructureId,
            Name = s.Name,
            Components = s.Components.Select(sc => new SalaryStructureComponentView
            {
                SalaryStructureComponentId = sc.SalaryStructureComponentId,
                SalaryComponentId = sc.SalaryComponentId,
                ComponentName = sc.Component.Name,
                Kind = sc.Component.Kind,
                ValueType = sc.ValueType,
                FlatAmount = sc.FlatAmount,
                Percentage = sc.Percentage
            }).ToList()
        };
    }

    public async Task<long> SaveStructureAsync(long? id, SaveSalaryStructureRequest request, CancellationToken ct)
    {
        SalaryStructure structure;
        if (id is null)
        {
            structure = new SalaryStructure { Name = request.Name };
            _db.SalaryStructures.Add(structure);
        }
        else
        {
            structure = await _db.SalaryStructures
                .Include(s => s.Components)
                .FirstOrDefaultAsync(x => x.SalaryStructureId == id.Value, ct)
                ?? throw new KeyNotFoundException($"Salary structure {id} not found.");

            structure.Name = request.Name;
            _db.SalaryStructureComponents.RemoveRange(structure.Components);
        }

        foreach (var c in request.Components)
        {
            structure.Components.Add(new SalaryStructureComponent
            {
                SalaryComponentId = c.SalaryComponentId,
                ValueType = c.ValueType,
                FlatAmount = c.FlatAmount,
                Percentage = c.Percentage
            });
        }

        await _db.SaveChangesAsync(ct);
        return structure.SalaryStructureId;
    }

    // ---- Employee Salary & Revisions ----------------------------------------

    public async Task<EmployeeSalaryView?> GetEmployeeSalaryAsync(long employeeId, CancellationToken ct)
    {
        EmployeeSalary? es = await _db.EmployeeSalaries.AsNoTracking()
            .Include(x => x.Structure)
            .Where(x => x.EmployeeId == employeeId)
            .OrderByDescending(x => x.EffectiveFrom)
            .FirstOrDefaultAsync(ct);

        if (es is null) return null;

        return new EmployeeSalaryView
        {
            EmployeeSalaryId = es.EmployeeSalaryId,
            EmployeeId = es.EmployeeId,
            SalaryStructureId = es.SalaryStructureId,
            StructureName = es.Structure?.Name ?? "",
            AnnualCtc = es.AnnualCtc,
            EffectiveFrom = es.EffectiveFrom
        };
    }

    public async Task<long> AssignEmployeeSalaryAsync(SaveEmployeeSalaryRequest request, CancellationToken ct)
    {
        var es = new EmployeeSalary
        {
            EmployeeId = request.EmployeeId,
            SalaryStructureId = request.SalaryStructureId,
            AnnualCtc = request.AnnualCtc,
            EffectiveFrom = request.EffectiveFrom
        };

        _db.EmployeeSalaries.Add(es);
        await _db.SaveChangesAsync(ct);
        return es.EmployeeSalaryId;
    }

    public async Task<long> ReviseSalaryAsync(SaveSalaryRevisionRequest request, CancellationToken ct)
    {
        EmployeeSalary currentSalary = await _db.EmployeeSalaries
            .Where(x => x.EmployeeId == request.EmployeeId)
            .OrderByDescending(x => x.EffectiveFrom)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException($"Employee {request.EmployeeId} has no assigned salary to revise.");

        var revision = new SalaryRevision
        {
            EmployeeId = request.EmployeeId,
            EmployeeSalaryId = currentSalary.EmployeeSalaryId,
            PreviousCtc = currentSalary.AnnualCtc,
            NewCtc = request.NewCtc,
            EffectiveFrom = request.EffectiveFrom,
            Reason = request.Reason,
            ArrearsProcessed = false
        };

        _db.SalaryRevisions.Add(revision);

        // Update active salary record or add new one
        var newSalary = new EmployeeSalary
        {
            EmployeeId = request.EmployeeId,
            SalaryStructureId = currentSalary.SalaryStructureId,
            AnnualCtc = request.NewCtc,
            EffectiveFrom = request.EffectiveFrom
        };
        _db.EmployeeSalaries.Add(newSalary);

        await _db.SaveChangesAsync(ct);
        return revision.SalaryRevisionId;
    }
}
