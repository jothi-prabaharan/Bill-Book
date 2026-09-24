using Hrm.Api.Services;
using Hrm.Entity.Enums;
using Hrm.Entity.Models;
using Hrm.Entity.TableEntities;
using Hrm.Repository;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Hrm.Api.Tests;

[Collection(nameof(PostgresCollection))]
public sealed class LifecycleServiceTests
{
    private readonly PostgresFixture _postgres;

    public LifecycleServiceTests(PostgresFixture postgres) => _postgres = postgres;

    private sealed class FakeMasterUserClient : IMasterUserClient
    {
        public List<Guid> DeactivatedUsers { get; } = [];

        public Task DeactivateUserAsync(Guid userId, CancellationToken ct)
        {
            DeactivatedUsers.Add(userId);
            return Task.CompletedTask;
        }
    }

    [SkippableFact]
    public async Task Checklist_template_can_be_copied_to_employee_and_items_marked_done()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid customerId = Guid.NewGuid(), orgId = Guid.NewGuid();
        await using HrmDbContext db = _postgres.CreateContext(customerId, orgId);
        var fakeMaster = new FakeMasterUserClient();
        var lifecycle = new LifecycleService(db, fakeMaster);

        // 1. Create Onboarding template
        long templateId = await lifecycle.SaveTemplateAsync(null, new SaveChecklistTemplateRequest
        {
            Name = "Standard Onboarding",
            Kind = ChecklistKind.Onboarding,
            Items =
            [
                new SaveChecklistTemplateItemRequest { Title = "Collect PAN & Aadhaar", OwnerRole = ChecklistOwnerRole.Hr, SortOrder = 1 },
                new SaveChecklistTemplateItemRequest { Title = "Assign Laptop", OwnerRole = ChecklistOwnerRole.It, SortOrder = 2 }
            ]
        }, default);

        Assert.True(templateId > 0);

        // 2. Assign to employee
        long employeeId = 501;
        long checklistId = await lifecycle.CreateEmployeeChecklistAsync(new CreateEmployeeChecklistRequest
        {
            EmployeeId = employeeId,
            Kind = ChecklistKind.Onboarding,
            ChecklistTemplateId = templateId
        }, default);

        var checklist = await lifecycle.GetEmployeeChecklistAsync(employeeId, ChecklistKind.Onboarding, default);
        Assert.NotNull(checklist);
        Assert.Equal(2, checklist.Items.Count);

        // 3. Mark first item done
        var firstItem = checklist.Items.First();
        await lifecycle.UpdateChecklistItemAsync(firstItem.EmployeeChecklistItemId, new UpdateChecklistItemRequest
        {
            IsDone = true,
            Remarks = "Documents collected and verified"
        }, default);

        var updated = await lifecycle.GetEmployeeChecklistAsync(employeeId, ChecklistKind.Onboarding, default);
        Assert.NotNull(updated);
        var updatedItem = updated.Items.First(i => i.EmployeeChecklistItemId == firstItem.EmployeeChecklistItemId);
        Assert.True(updatedItem.IsDone);
        Assert.NotNull(updatedItem.DoneDate);
    }

    [SkippableFact]
    public async Task Separation_calculates_notice_shortfall_and_settlement_marks_exited_and_deactivates_user()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid customerId = Guid.NewGuid(), orgId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();
        await using HrmDbContext db = _postgres.CreateContext(customerId, orgId);
        var fakeMaster = new FakeMasterUserClient();
        var lifecycle = new LifecycleService(db, fakeMaster);

        // Create an employee with 30-day notice and a linked user login
        var emp = new Employee
        {
            EmployeeCode = "EMP-099",
            FirstName = "Alice",
            LastName = "Smith",
            Phone = "9876543210",
            NoticePeriodDays = 30,
            EmployeeStatus = EmployeeStatus.Active,
            JoiningDate = new DateOnly(2025, 1, 1),
            DateOfBirth = new DateOnly(1995, 1, 1),
            Gender = Gender.Female,
            DepartmentId = 1,
            DesignationId = 1,
            GradeId = 1,
            WorkLocationId = 1,
            UserId = userId
        };
        db.Employees.Add(emp);
        await db.SaveChangesAsync();

        // Resign with only 10 days notice (shortfall of 20 days)
        DateOnly reqDate = new DateOnly(2026, 9, 1);
        DateOnly lwd = new DateOnly(2026, 9, 11);

        long sepId = await lifecycle.SubmitSeparationAsync(new SaveSeparationRequest
        {
            EmployeeId = emp.EmployeeId,
            Kind = SeparationKind.Resignation,
            RequestDate = reqDate,
            LastWorkingDate = lwd,
            IsNoticeWaived = false,
            Reason = "Career move"
        }, default);

        var sep = await lifecycle.GetSeparationAsync(emp.EmployeeId, default);
        Assert.NotNull(sep);
        Assert.Equal(20m, sep.NoticeShortfallDays);
        Assert.Equal(SeparationStatus.Submitted, sep.Status);

        // Employee moves to OnNotice
        var employeeOnNotice = await db.Employees.FirstAsync(e => e.EmployeeId == emp.EmployeeId);
        Assert.Equal(EmployeeStatus.OnNotice, employeeOnNotice.EmployeeStatus);

        // Settle employee
        await lifecycle.SettleSeparationAsync(emp.EmployeeId, lwd, default);

        var settledEmp = await db.Employees.FirstAsync(e => e.EmployeeId == emp.EmployeeId);
        Assert.Equal(EmployeeStatus.Exited, settledEmp.EmployeeStatus);
        Assert.Equal(lwd, settledEmp.ExitDate);

        // Master user is deactivated
        Assert.Contains(userId, fakeMaster.DeactivatedUsers);
    }
}
