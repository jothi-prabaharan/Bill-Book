using Employee.Api.Services;
using Employee.Entity.Enums;
using Employee.Entity.Models;
using Employee.Entity.TableEntities;
using Employee.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Apps;
using Shared.Kernel.Interfaces;
using Shared.Kernel.Internal;

namespace Employee.Api.Controllers;

/// <summary>
/// Self-service profile, documents, and announcements (H8, TK-55).
/// Resolves employee strictly from the caller's JWT user claim ('sub'), never an ID in the URL.
/// Accessible to any signed-in user without requiring administrative module permissions.
/// </summary>
[ApiController]
[Authorize]
[RequireApp(App.Hrms | App.Payroll)]
[Route("api/hrm/me")]
[Route("api/me")]
public sealed class MeProfileController : ControllerBase
{
    private readonly EmployeeDbContext _db;
    private readonly ICurrentUser _currentUser;

    public MeProfileController(EmployeeDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile(CancellationToken ct)
    {
        if (_currentUser.UserId is not Guid userId)
        {
            return Forbid();
        }

        var e = await _db.Employees
            .Include(x => x.Addresses)
            .Include(x => x.Contacts)
            .Include(x => x.FamilyMembers)
            .Include(x => x.Education)
            .Include(x => x.PreviousEmployments)
            .Include(x => x.BankDetails)
            .Include(x => x.Documents)
            .FirstOrDefaultAsync(x => x.UserId == userId && x.EmployeeStatus != EmployeeStatus.Exited, ct);

        if (e is null)
        {
            return NotFound(new { message = "No active employee profile linked to your user account." });
        }

        string? deptName = await _db.Departments.Where(d => d.DepartmentId == e.DepartmentId).Select(d => d.Name).FirstOrDefaultAsync(ct);
        string? desigName = await _db.Designations.Where(d => d.DesignationId == e.DesignationId).Select(d => d.Name).FirstOrDefaultAsync(ct);
        string? gradeName = await _db.Grades.Where(g => g.GradeId == e.GradeId).Select(g => g.Name).FirstOrDefaultAsync(ct);
        string? locName = await _db.WorkLocations.Where(l => l.WorkLocationId == e.WorkLocationId).Select(l => l.Name).FirstOrDefaultAsync(ct);
        string? managerName = e.ReportsToEmployeeId.HasValue
            ? await _db.Employees.Where(m => m.EmployeeId == e.ReportsToEmployeeId.Value).Select(m => m.FirstName + (m.LastName == null ? "" : " " + m.LastName)).FirstOrDefaultAsync(ct)
            : null;

        var primaryContact = e.Contacts.FirstOrDefault(c => c.IsPrimary) ?? e.Contacts.FirstOrDefault();

        var profile = new MyProfileDto
        {
            EmployeeId = e.EmployeeId,
            EmployeeCode = e.EmployeeCode,
            FirstName = e.FirstName,
            MiddleName = e.MiddleName,
            LastName = e.LastName,
            FullName = e.FirstName + (e.LastName == null ? "" : " " + e.LastName),
            DateOfBirth = e.DateOfBirth,
            Gender = e.Gender.ToString(),
            MaritalStatus = e.MaritalStatus.ToString(),
            BloodGroup = e.BloodGroup,
            Phone = e.Phone,
            WorkEmail = e.WorkEmail,
            PersonalEmail = e.PersonalEmail,
            EmergencyContactName = primaryContact?.Name,
            EmergencyContactPhone = primaryContact?.Phone,
            Pan = SensitiveMask.Mask(e.Pan),
            Aadhaar = SensitiveMask.Mask(e.Aadhaar),
            Uan = e.Uan,
            DepartmentId = e.DepartmentId,
            DepartmentName = deptName,
            DesignationId = e.DesignationId,
            DesignationName = desigName,
            GradeId = e.GradeId,
            GradeName = gradeName,
            WorkLocationId = e.WorkLocationId,
            WorkLocationName = locName,
            ReportsToEmployeeId = e.ReportsToEmployeeId,
            ReportsToName = managerName,
            JoiningDate = e.JoiningDate,
            ConfirmationDate = e.ConfirmationDate,
            EmploymentType = e.EmploymentType.ToString(),
            EmployeeStatus = e.EmployeeStatus.ToString(),
            Addresses = e.Addresses.OrderBy(a => a.AddressKind).Select(a => new EmployeeAddressModel
            {
                AddressKind = a.AddressKind, AddressLine1 = a.AddressLine1, AddressLine2 = a.AddressLine2,
                City = a.City, StateId = a.StateId, PostalCode = a.PostalCode,
            }).ToList(),
            Contacts = e.Contacts.OrderBy(c => c.EmployeeContactId).Select(c => new EmployeeContactModel
            {
                Name = c.Name, Relationship = c.Relationship, Phone = c.Phone, IsPrimary = c.IsPrimary,
            }).ToList(),
            FamilyMembers = e.FamilyMembers.OrderBy(f => f.EmployeeFamilyMemberId).Select(f => new EmployeeFamilyMemberModel
            {
                EmployeeFamilyMemberId = f.EmployeeFamilyMemberId, Name = f.Name, Relationship = f.Relationship,
                DateOfBirth = f.DateOfBirth, IsDependent = f.IsDependent, IsEsiCovered = f.IsEsiCovered,
            }).ToList(),
            Education = e.Education.OrderBy(x => x.EmployeeEducationId).Select(x => new EmployeeEducationModel
            {
                Qualification = x.Qualification, Institution = x.Institution, YearOfPassing = x.YearOfPassing, Grade = x.Grade,
            }).ToList(),
            PreviousEmployments = e.PreviousEmployments.OrderBy(x => x.FromDate).Select(x => new PreviousEmploymentModel
            {
                Employer = x.Employer, FromDate = x.FromDate, ToDate = x.ToDate, LastDesignation = x.LastDesignation,
            }).ToList(),
            BankDetails = e.BankDetails.OrderByDescending(b => b.IsPrimary).Select(b => new EmployeeBankDetailModel
            {
                EmployeeBankDetailId = b.EmployeeBankDetailId,
                AccountHolder = b.AccountHolder,
                AccountNo = SensitiveMask.Mask(b.AccountNo)!,
                Ifsc = b.Ifsc,
                BankName = b.BankName,
                IsPrimary = b.IsPrimary,
            }).ToList(),
            Documents = e.Documents.OrderBy(d => d.EmployeeDocumentId).Select(d => new EmployeeDocumentModel
            {
                DocumentKind = d.DocumentKind, AttachmentKey = d.AttachmentKey, ValidUntil = d.ValidUntil,
            }).ToList(),
        };

        return Ok(profile);
    }

    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateMyProfileRequest req, CancellationToken ct)
    {
        if (_currentUser.UserId is not Guid userId)
        {
            return Forbid();
        }

        var e = await _db.Employees
            .Include(x => x.Contacts)
            .FirstOrDefaultAsync(x => x.UserId == userId && x.EmployeeStatus != EmployeeStatus.Exited, ct);

        if (e is null)
        {
            return NotFound(new { message = "No active employee profile linked to your user account." });
        }

        if (req.Phone is not null) e.Phone = req.Phone;
        if (req.PersonalEmail is not null) e.PersonalEmail = req.PersonalEmail;
        if (req.BloodGroup is not null) e.BloodGroup = req.BloodGroup;
        if (req.MaritalStatus.HasValue) e.MaritalStatus = req.MaritalStatus.Value;

        if (req.EmergencyContactName is not null || req.EmergencyContactPhone is not null)
        {
            var primaryContact = e.Contacts.FirstOrDefault(c => c.IsPrimary) ?? e.Contacts.FirstOrDefault();
            if (primaryContact != null)
            {
                if (req.EmergencyContactName is not null) primaryContact.Name = req.EmergencyContactName;
                if (req.EmergencyContactPhone is not null) primaryContact.Phone = req.EmergencyContactPhone;
            }
            else if (req.EmergencyContactName is not null && req.EmergencyContactPhone is not null)
            {
                e.Contacts.Add(new EmployeeContact
                {
                    Name = req.EmergencyContactName,
                    Phone = req.EmergencyContactPhone,
                    Relationship = Relationship.Other,
                    IsPrimary = true
                });
            }
        }

        await _db.SaveChangesAsync(ct);
        return Ok(new { message = "Profile updated successfully." });
    }

    [HttpGet("documents")]
    public async Task<IActionResult> GetDocuments(CancellationToken ct)
    {
        if (_currentUser.UserId is not Guid userId)
        {
            return Forbid();
        }

        var e = await _db.Employees
            .Include(x => x.Documents)
            .FirstOrDefaultAsync(x => x.UserId == userId && x.EmployeeStatus != EmployeeStatus.Exited, ct);

        if (e is null)
        {
            return NotFound(new { message = "No active employee profile linked to your user account." });
        }

        DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);
        var policies = await _db.PolicyDocuments
            .Where(p => p.EffectiveDate <= today && p.IsActive)
            .OrderByDescending(p => p.EffectiveDate)
            .Select(p => new
            {
                p.PolicyDocumentId,
                p.Title,
                p.EffectiveDate,
                p.AttachmentKey,
                p.IsAcknowledgementRequired
            })
            .ToListAsync(ct);

        var employeeDocs = e.Documents.Select(d => new
        {
            d.EmployeeDocumentId,
            d.DocumentKind,
            d.AttachmentKey,
            d.ValidUntil
        }).ToList();

        return Ok(new
        {
            employeeDocuments = employeeDocs,
            companyPolicies = policies
        });
    }

    [HttpGet("announcements")]
    public async Task<IActionResult> GetAnnouncements(CancellationToken ct)
    {
        if (_currentUser.UserId is not Guid userId)
        {
            return Forbid();
        }

        var e = await _db.Employees
            .FirstOrDefaultAsync(x => x.UserId == userId && x.EmployeeStatus != EmployeeStatus.Exited, ct);

        DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);
        var query = _db.Announcements
            .Where(a => a.PublishDate <= today && (a.ExpiryDate == null || a.ExpiryDate >= today));

        if (e is not null)
        {
            query = query.Where(a => a.Audience == AnnouncementAudience.Everyone
                || (a.Audience == AnnouncementAudience.Department && a.AudienceRefId == e.DepartmentId)
                || (a.Audience == AnnouncementAudience.Location && a.AudienceRefId == e.WorkLocationId)
                || (a.Audience == AnnouncementAudience.Grade && a.AudienceRefId == e.GradeId));
        }

        var announcements = await query
            .OrderByDescending(a => a.PublishDate)
            .Select(a => new
            {
                a.AnnouncementId,
                a.Title,
                a.Body,
                a.PublishDate,
                a.ExpiryDate,
                a.IsPinned
            })
            .ToListAsync(ct);

        return Ok(announcements);
    }
}
