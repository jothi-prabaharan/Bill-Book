using Hrm.Entity.Enums;
using Hrm.Entity.Models;
using Hrm.Entity.TableEntities;
using Hrm.Repository;
using Hrm.Repository.SeedData;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Numbering;

namespace Hrm.Api.Services;

/// <summary>
/// The shared employee master (H1, TK-48): an employee and every child table,
/// saved together, with the employment history written beside it.
///
/// <list type="bullet">
/// <item><b>The code comes from <c>EMP</c></b>, taken in the same transaction as
/// the row, so a refused save gives the number back.</item>
/// <item><b>PAN, Aadhaar and bank numbers are masked</b> on the list always, and on
/// the detail unless the caller holds <c>payroll.view</c> or is the employee. A
/// masked value sent back on save keeps what is stored
/// (<see cref="SensitiveMask.Resolve"/>).</item>
/// <item><b>History is appended, never updated</b>: a Joined row at creation, and a
/// row at every change of department, designation, grade, location, manager,
/// confirmation or exit.</item>
/// </list>
/// </summary>
public sealed class EmployeeService
{
    public const string SalaryPermission = "payroll.view";
    private const int MinimumAgeAtJoining = 14;

    private readonly HrmDbContext _db;
    private readonly INumberGenerator _numbers;
    private readonly ICallerPermissions _caller;
    private readonly TimeProvider _clock;

    public EmployeeService(HrmDbContext db, INumberGenerator numbers, ICallerPermissions caller, TimeProvider clock)
    {
        _db = db;
        _numbers = numbers;
        _caller = caller;
        _clock = clock;
    }

    // ---- Read ------------------------------------------------------------

    public async Task<EmployeeListPage> ListAsync(
        string? search, long? departmentId, EmployeeStatus? status, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        IQueryable<Employee> query = _db.Employees.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            string term = search.Trim().ToLower();
            query = query.Where(e =>
                e.EmployeeCode.ToLower().Contains(term)
                || e.FirstName.ToLower().Contains(term)
                || (e.LastName != null && e.LastName.ToLower().Contains(term))
                || e.Phone.Contains(term));
        }

        if (departmentId is long department)
        {
            query = query.Where(e => e.DepartmentId == department);
        }

        if (status is EmployeeStatus wanted)
        {
            query = query.Where(e => e.EmployeeStatus == wanted);
        }

        int total = await query.CountAsync(ct);

        var rows = await (
            from e in query
            join d in _db.Departments on e.DepartmentId equals d.DepartmentId
            join g in _db.Designations on e.DesignationId equals g.DesignationId
            join w in _db.WorkLocations on e.WorkLocationId equals w.WorkLocationId
            orderby e.EmployeeCode
            select new
            {
                e.EmployeeId, e.EmployeeCode, e.FirstName, e.MiddleName, e.LastName,
                Department = d.Name, Designation = g.Name, Location = w.Name,
                e.JoiningDate, e.EmployeeStatus, e.EmploymentType, e.Phone, e.Pan, e.Aadhaar, e.UserId,
            })
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new EmployeeListPage
        {
            Total = total,
            Items = rows.Select(r => new EmployeeListItem
            {
                EmployeeId = r.EmployeeId,
                EmployeeCode = r.EmployeeCode,
                FullName = FullName(r.FirstName, r.MiddleName, r.LastName),
                DepartmentName = r.Department,
                DesignationName = r.Designation,
                WorkLocationName = r.Location,
                JoiningDate = r.JoiningDate,
                EmployeeStatus = r.EmployeeStatus.ToString(),
                EmploymentType = r.EmploymentType.ToString(),
                Phone = r.Phone,
                // Masked on the list, always, whoever asks.
                MaskedPan = SensitiveMask.Mask(r.Pan),
                MaskedAadhaar = SensitiveMask.Mask(r.Aadhaar),
                HasLogin = r.UserId.HasValue,
            }).ToList(),
        };
    }

    public async Task<EmployeeDetail?> GetAsync(long employeeId, CancellationToken ct)
    {
        Employee? e = await LoadAsync(employeeId, tracking: false, ct);
        if (e is null)
        {
            return null;
        }

        bool shown = MaySeeSensitive(e);
        List<EmploymentHistory> history = await _db.EmploymentHistories.AsNoTracking()
            .Where(h => h.EmployeeId == employeeId)
            .OrderBy(h => h.EffectiveDate).ThenBy(h => h.EmploymentHistoryId)
            .ToListAsync(ct);

        List<EmployeeFamilyMember> family = e.FamilyMembers.OrderBy(f => f.EmployeeFamilyMemberId).ToList();

        return new EmployeeDetail
        {
            EmployeeId = e.EmployeeId,
            EmployeeCode = e.EmployeeCode,
            SensitiveShown = shown,
            FirstName = e.FirstName,
            MiddleName = e.MiddleName,
            LastName = e.LastName,
            DateOfBirth = e.DateOfBirth,
            Gender = e.Gender,
            MaritalStatus = e.MaritalStatus,
            BloodGroup = e.BloodGroup,
            DepartmentId = e.DepartmentId,
            DesignationId = e.DesignationId,
            GradeId = e.GradeId,
            WorkLocationId = e.WorkLocationId,
            CostCentreId = e.CostCentreId,
            ReportsToEmployeeId = e.ReportsToEmployeeId,
            JoiningDate = e.JoiningDate,
            ProbationEndDate = e.ProbationEndDate,
            ConfirmationDate = e.ConfirmationDate,
            NoticePeriodDays = e.NoticePeriodDays,
            EmploymentType = e.EmploymentType,
            EmployeeStatus = e.EmployeeStatus,
            ExitDate = e.ExitDate,
            UserId = e.UserId,
            WorkEmail = e.WorkEmail,
            PersonalEmail = e.PersonalEmail,
            Phone = e.Phone,
            Pan = shown ? e.Pan : SensitiveMask.Mask(e.Pan),
            Aadhaar = shown ? e.Aadhaar : SensitiveMask.Mask(e.Aadhaar),
            Uan = e.Uan,
            PfNumber = e.PfNumber,
            EsiNumber = e.EsiNumber,
            IsPfApplicable = e.IsPfApplicable,
            IsEsiApplicable = e.IsEsiApplicable,
            IsPtApplicable = e.IsPtApplicable,
            IsLwfApplicable = e.IsLwfApplicable,
            Addresses = e.Addresses.OrderBy(a => a.AddressKind).Select(a => new EmployeeAddressModel
            {
                AddressKind = a.AddressKind, AddressLine1 = a.AddressLine1, AddressLine2 = a.AddressLine2,
                City = a.City, StateId = a.StateId, PostalCode = a.PostalCode,
            }).ToList(),
            Contacts = e.Contacts.OrderBy(c => c.EmployeeContactId).Select(c => new EmployeeContactModel
            {
                Name = c.Name, Relationship = c.Relationship, Phone = c.Phone, IsPrimary = c.IsPrimary,
            }).ToList(),
            FamilyMembers = family.Select(f => new EmployeeFamilyMemberModel
            {
                EmployeeFamilyMemberId = f.EmployeeFamilyMemberId, Name = f.Name, Relationship = f.Relationship,
                DateOfBirth = f.DateOfBirth, IsDependent = f.IsDependent, IsEsiCovered = f.IsEsiCovered,
            }).ToList(),
            Nominees = e.Nominees.OrderBy(n => n.EmployeeNomineeId).Select(n => new EmployeeNomineeModel
            {
                FamilyMemberIndex = family.FindIndex(f => f.EmployeeFamilyMemberId == n.FamilyMemberId),
                NominationKind = n.NominationKind,
                SharePercent = n.SharePercent,
            }).ToList(),
            Education = e.Education.OrderBy(x => x.EmployeeEducationId).Select(x => new EmployeeEducationModel
            {
                Qualification = x.Qualification, Institution = x.Institution, YearOfPassing = x.YearOfPassing, Grade = x.Grade,
            }).ToList(),
            PreviousEmployments = e.PreviousEmployments.OrderBy(x => x.FromDate).Select(x => new PreviousEmploymentModel
            {
                Employer = x.Employer, FromDate = x.FromDate, ToDate = x.ToDate, LastDesignation = x.LastDesignation,
            }).ToList(),
            BankDetails = e.BankDetails.OrderByDescending(b => b.IsPrimary).ThenBy(b => b.EmployeeBankDetailId).Select(b => new EmployeeBankDetailModel
            {
                EmployeeBankDetailId = b.EmployeeBankDetailId,
                AccountHolder = b.AccountHolder,
                AccountNo = shown ? b.AccountNo : SensitiveMask.Mask(b.AccountNo)!,
                Ifsc = b.Ifsc,
                BankName = b.BankName,
                IsPrimary = b.IsPrimary,
            }).ToList(),
            Documents = e.Documents.OrderBy(d => d.EmployeeDocumentId).Select(d => new EmployeeDocumentModel
            {
                DocumentKind = d.DocumentKind, AttachmentKey = d.AttachmentKey, ValidUntil = d.ValidUntil,
            }).ToList(),
            AssetIssues = e.AssetIssues.OrderBy(a => a.IssuedDate).Select(a => new AssetIssueModel
            {
                AssetName = a.AssetName, AssetTag = a.AssetTag, IssuedDate = a.IssuedDate,
                ReturnedDate = a.ReturnedDate, RecoveryAmount = a.RecoveryAmount,
            }).ToList(),
            History = history.Select(h => new EmploymentHistoryRow
            {
                EffectiveDate = h.EffectiveDate, ChangeKind = h.ChangeKind.ToString(),
                DepartmentId = h.DepartmentId, DesignationId = h.DesignationId, GradeId = h.GradeId,
                WorkLocationId = h.WorkLocationId, ReportsToEmployeeId = h.ReportsToEmployeeId, Remarks = h.Remarks,
            }).ToList(),
        };
    }

    // ---- Write -----------------------------------------------------------

    public async Task<EmployeeResult> CreateAsync(SaveEmployeeRequest request, CancellationToken ct)
    {
        string? pan = NormalizePan(request.Pan);
        string? aadhaar = NormalizeAadhaar(request.Aadhaar);

        EmployeeOutcome check = await ValidateAsync(null, request, pan, aadhaar, ct);
        if (check != EmployeeOutcome.Ok)
        {
            return new EmployeeResult(check);
        }

        int notice = request.NoticePeriodDays
            ?? await _db.Grades.Where(g => g.GradeId == request.GradeId).Select(g => g.NoticePeriodDays).FirstAsync(ct);

        NumberAllocation code = await _numbers.NextAsync(HrmSeed.EmployeeSeriesCode, request.JoiningDate, ct);

        var employee = new Employee { EmployeeCode = code.Code, NoticePeriodDays = notice };
        Apply(employee, request, pan, aadhaar);
        ReplaceChildren(employee, request, storedAccounts: new Dictionary<long, string>());
        _db.Employees.Add(employee);
        await _db.SaveChangesAsync(ct);

        _db.EmploymentHistories.Add(History(employee, EmploymentChangeKind.Joined, request.JoiningDate, request.Remarks));
        await _db.SaveChangesAsync(ct);

        return new EmployeeResult(EmployeeOutcome.Ok, employee.EmployeeId, employee.EmployeeCode);
    }

    public async Task<EmployeeResult> UpdateAsync(long employeeId, SaveEmployeeRequest request, CancellationToken ct)
    {
        Employee? employee = await LoadAsync(employeeId, tracking: true, ct);
        if (employee is null)
        {
            return new EmployeeResult(EmployeeOutcome.NotFound);
        }

        string? pan = SensitiveMask.Resolve(NormalizePan(request.Pan), employee.Pan);
        string? aadhaar = SensitiveMask.Resolve(NormalizeAadhaar(request.Aadhaar), employee.Aadhaar);

        EmployeeOutcome check = await ValidateAsync(employeeId, request, pan, aadhaar, ct);
        if (check != EmployeeOutcome.Ok)
        {
            return new EmployeeResult(check);
        }

        EmploymentChangeKind? change = await ChangeOfAsync(employee, request, ct);
        Dictionary<long, string> storedAccounts = employee.BankDetails.ToDictionary(b => b.EmployeeBankDetailId, b => b.AccountNo);

        // The children are replaced, not merged. Every stored child goes, and
        // the request's list goes in, so what is saved is exactly what was sent.
        // Nominees go first: they point at family members.
        _db.EmployeeNominees.RemoveRange(employee.Nominees);
        _db.EmployeeAddresses.RemoveRange(employee.Addresses);
        _db.EmployeeContacts.RemoveRange(employee.Contacts);
        _db.EmployeeEducation.RemoveRange(employee.Education);
        _db.PreviousEmployments.RemoveRange(employee.PreviousEmployments);
        _db.EmployeeBankDetails.RemoveRange(employee.BankDetails);
        _db.EmployeeDocuments.RemoveRange(employee.Documents);
        _db.AssetIssues.RemoveRange(employee.AssetIssues);
        await _db.SaveChangesAsync(ct);
        _db.EmployeeFamilyMembers.RemoveRange(employee.FamilyMembers);
        await _db.SaveChangesAsync(ct);

        employee.Nominees.Clear();
        employee.Addresses.Clear();
        employee.Contacts.Clear();
        employee.Education.Clear();
        employee.PreviousEmployments.Clear();
        employee.BankDetails.Clear();
        employee.Documents.Clear();
        employee.AssetIssues.Clear();
        employee.FamilyMembers.Clear();

        if (request.NoticePeriodDays is int notice)
        {
            employee.NoticePeriodDays = notice;
        }

        Apply(employee, request, pan, aadhaar);
        ReplaceChildren(employee, request, storedAccounts);

        if (change is EmploymentChangeKind kind)
        {
            DateOnly effective = kind switch
            {
                EmploymentChangeKind.Exit => request.ExitDate ?? Today(),
                EmploymentChangeKind.Confirmed => request.ConfirmationDate ?? Today(),
                _ => request.EffectiveDate ?? Today(),
            };
            _db.EmploymentHistories.Add(History(employee, kind, effective, request.Remarks));
        }

        await _db.SaveChangesAsync(ct);
        return new EmployeeResult(EmployeeOutcome.Ok, employee.EmployeeId, employee.EmployeeCode);
    }

    // ---- Rules -----------------------------------------------------------

    /// <summary>Everything a save must satisfy, in the order a person would fix it.</summary>
    private async Task<EmployeeOutcome> ValidateAsync(
        long? employeeId, SaveEmployeeRequest r, string? pan, string? aadhaar, CancellationToken ct)
    {
        if (!await _db.Departments.AnyAsync(x => x.DepartmentId == r.DepartmentId, ct)
            || !await _db.Designations.AnyAsync(x => x.DesignationId == r.DesignationId, ct)
            || !await _db.Grades.AnyAsync(x => x.GradeId == r.GradeId, ct)
            || !await _db.WorkLocations.AnyAsync(x => x.WorkLocationId == r.WorkLocationId, ct)
            || (r.CostCentreId is long cc && !await _db.CostCentres.AnyAsync(x => x.CostCentreId == cc, ct))
            || (r.ReportsToEmployeeId is long m && !await _db.Employees.AnyAsync(x => x.EmployeeId == m, ct)))
        {
            return EmployeeOutcome.InvalidReference;
        }

        if (employeeId is long self && r.ReportsToEmployeeId is long manager
            && await ManagerChainReachesAsync(manager, self, ct))
        {
            return EmployeeOutcome.ManagerCycle;
        }

        return await RulesOutcomeAsync(employeeId, r, pan, aadhaar, ct);
    }

    private async Task<EmployeeOutcome> RulesOutcomeAsync(
        long? employeeId, SaveEmployeeRequest r, string? pan, string? aadhaar, CancellationToken ct)
    {
        EmployeeOutcome pure = CheckRules(r, pan, aadhaar);
        if (pure != EmployeeOutcome.Ok)
        {
            return pure;
        }

        if (r.UserId is Guid user
            && await _db.Employees.AnyAsync(e => e.UserId == user && e.EmployeeId != (employeeId ?? 0), ct))
        {
            return EmployeeOutcome.UserAlreadyLinked;
        }

        return EmployeeOutcome.Ok;
    }

    /// <summary>The rules that need no database. Public for the tests.</summary>
    public static EmployeeOutcome CheckRules(SaveEmployeeRequest r, string? pan, string? aadhaar)
    {
        if (r.DateOfBirth.AddYears(MinimumAgeAtJoining) > r.JoiningDate)
        {
            return EmployeeOutcome.TooYoung;
        }

        if (r.EmployeeStatus == EmployeeStatus.Exited && r.ExitDate is null)
        {
            return EmployeeOutcome.ExitDateRequired;
        }

        if ((pan is not null && !System.Text.RegularExpressions.Regex.IsMatch(pan, "^[A-Z]{5}[0-9]{4}[A-Z]$"))
            || (aadhaar is not null && !System.Text.RegularExpressions.Regex.IsMatch(aadhaar, "^[0-9]{12}$")))
        {
            return EmployeeOutcome.InvalidIdentityNumber;
        }

        if (r.Addresses.GroupBy(a => a.AddressKind).Any(g => g.Count() > 1))
        {
            return EmployeeOutcome.DuplicateAddress;
        }

        if (r.BankDetails.Count > 0 && r.BankDetails.Count(b => b.IsPrimary) != 1)
        {
            return EmployeeOutcome.PrimaryBank;
        }

        if (r.Nominees.Any(n => n.FamilyMemberIndex < 0 || n.FamilyMemberIndex >= r.FamilyMembers.Count))
        {
            return EmployeeOutcome.NomineeFamilyMember;
        }

        if (r.Nominees.GroupBy(n => n.NominationKind).Any(g => g.Sum(n => n.SharePercent) != 100m))
        {
            return EmployeeOutcome.NomineeShares;
        }

        return EmployeeOutcome.Ok;
    }

    /// <summary>Whether walking up from <paramref name="start"/> through managers reaches <paramref name="target"/>.</summary>
    private async Task<bool> ManagerChainReachesAsync(long start, long target, CancellationToken ct)
    {
        Dictionary<long, long?> managers = await _db.Employees.AsNoTracking()
            .ToDictionaryAsync(e => e.EmployeeId, e => e.ReportsToEmployeeId, ct);

        int steps = 0;
        for (long? at = start; at is long current && steps <= managers.Count; at = managers.GetValueOrDefault(current), steps++)
        {
            if (current == target)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The history row a save writes, if any: the first of exit, confirmation, then the move itself.</summary>
    private async Task<EmploymentChangeKind?> ChangeOfAsync(Employee e, SaveEmployeeRequest r, CancellationToken ct)
    {
        if (r.EmployeeStatus == EmployeeStatus.Exited && e.EmployeeStatus != EmployeeStatus.Exited)
        {
            return EmploymentChangeKind.Exit;
        }

        if (r.ConfirmationDate is not null && e.ConfirmationDate is null)
        {
            return EmploymentChangeKind.Confirmed;
        }

        if (r.DepartmentId != e.DepartmentId || r.WorkLocationId != e.WorkLocationId)
        {
            return EmploymentChangeKind.Transfer;
        }

        if (r.GradeId != e.GradeId)
        {
            int[] orders = await _db.Grades.Where(g => g.GradeId == r.GradeId || g.GradeId == e.GradeId)
                .OrderBy(g => g.GradeId == e.GradeId ? 0 : 1)
                .Select(g => g.SortOrder).ToArrayAsync(ct);
            return orders.Length == 2 && orders[1] > orders[0]
                ? EmploymentChangeKind.Promotion
                : EmploymentChangeKind.GradeChange;
        }

        if (r.DesignationId != e.DesignationId)
        {
            return EmploymentChangeKind.Redesignation;
        }

        if (r.ReportsToEmployeeId != e.ReportsToEmployeeId)
        {
            return EmploymentChangeKind.ManagerChange;
        }

        return null;
    }

    // ---- Mapping ---------------------------------------------------------

    private static void Apply(Employee e, SaveEmployeeRequest r, string? pan, string? aadhaar)
    {
        e.FirstName = r.FirstName.Trim();
        e.MiddleName = Blank(r.MiddleName);
        e.LastName = Blank(r.LastName);
        e.DateOfBirth = r.DateOfBirth;
        e.Gender = r.Gender;
        e.MaritalStatus = r.MaritalStatus;
        e.BloodGroup = Blank(r.BloodGroup);
        e.DepartmentId = r.DepartmentId;
        e.DesignationId = r.DesignationId;
        e.GradeId = r.GradeId;
        e.WorkLocationId = r.WorkLocationId;
        e.CostCentreId = r.CostCentreId;
        e.ReportsToEmployeeId = r.ReportsToEmployeeId;
        e.JoiningDate = r.JoiningDate;
        e.ProbationEndDate = r.ProbationEndDate;
        e.ConfirmationDate = r.ConfirmationDate;
        e.EmploymentType = r.EmploymentType;
        e.EmployeeStatus = r.EmployeeStatus;
        e.ExitDate = r.ExitDate;
        e.UserId = r.UserId;
        e.WorkEmail = Blank(r.WorkEmail);
        e.PersonalEmail = Blank(r.PersonalEmail);
        e.Phone = r.Phone.Trim();
        e.Pan = pan;
        e.Aadhaar = aadhaar;
        e.Uan = Blank(r.Uan);
        e.PfNumber = Blank(r.PfNumber);
        e.EsiNumber = Blank(r.EsiNumber);
        e.IsPfApplicable = r.IsPfApplicable;
        e.IsEsiApplicable = r.IsEsiApplicable;
        e.IsPtApplicable = r.IsPtApplicable;
        e.IsLwfApplicable = r.IsLwfApplicable;
    }

    private static void ReplaceChildren(Employee e, SaveEmployeeRequest r, IReadOnlyDictionary<long, string> storedAccounts)
    {
        foreach (EmployeeAddressModel a in r.Addresses)
        {
            e.Addresses.Add(new EmployeeAddress
            {
                AddressKind = a.AddressKind, AddressLine1 = a.AddressLine1.Trim(), AddressLine2 = Blank(a.AddressLine2),
                City = Blank(a.City), StateId = a.StateId, PostalCode = Blank(a.PostalCode),
            });
        }

        foreach (EmployeeContactModel c in r.Contacts)
        {
            e.Contacts.Add(new EmployeeContact { Name = c.Name.Trim(), Relationship = c.Relationship, Phone = c.Phone.Trim(), IsPrimary = c.IsPrimary });
        }

        List<EmployeeFamilyMember> family = r.FamilyMembers.Select(f => new EmployeeFamilyMember
        {
            Name = f.Name.Trim(), Relationship = f.Relationship, DateOfBirth = f.DateOfBirth,
            IsDependent = f.IsDependent, IsEsiCovered = f.IsEsiCovered,
        }).ToList();
        foreach (EmployeeFamilyMember member in family)
        {
            e.FamilyMembers.Add(member);
        }

        // A nominee points at a family member by its place in the request, and
        // the navigation lets EF fill in the id once both are inserted.
        foreach (EmployeeNomineeModel n in r.Nominees)
        {
            e.Nominees.Add(new EmployeeNominee
            {
                FamilyMember = family[n.FamilyMemberIndex],
                NominationKind = n.NominationKind,
                SharePercent = n.SharePercent,
            });
        }

        foreach (EmployeeEducationModel x in r.Education)
        {
            e.Education.Add(new EmployeeEducation { Qualification = x.Qualification.Trim(), Institution = x.Institution.Trim(), YearOfPassing = x.YearOfPassing, Grade = Blank(x.Grade) });
        }

        foreach (PreviousEmploymentModel x in r.PreviousEmployments)
        {
            e.PreviousEmployments.Add(new PreviousEmployment { Employer = x.Employer.Trim(), FromDate = x.FromDate, ToDate = x.ToDate, LastDesignation = Blank(x.LastDesignation) });
        }

        foreach (EmployeeBankDetailModel b in r.BankDetails)
        {
            string? stored = b.EmployeeBankDetailId is long id ? storedAccounts.GetValueOrDefault(id) : null;
            e.BankDetails.Add(new EmployeeBankDetail
            {
                AccountHolder = b.AccountHolder.Trim(),
                AccountNo = SensitiveMask.Resolve(b.AccountNo.Trim(), stored)!,
                Ifsc = b.Ifsc.Trim().ToUpperInvariant(),
                BankName = b.BankName.Trim(),
                IsPrimary = b.IsPrimary,
            });
        }

        foreach (EmployeeDocumentModel d in r.Documents)
        {
            e.Documents.Add(new EmployeeDocument { DocumentKind = d.DocumentKind, AttachmentKey = d.AttachmentKey, ValidUntil = d.ValidUntil });
        }

        foreach (AssetIssueModel a in r.AssetIssues)
        {
            e.AssetIssues.Add(new AssetIssue { AssetName = a.AssetName.Trim(), AssetTag = Blank(a.AssetTag), IssuedDate = a.IssuedDate, ReturnedDate = a.ReturnedDate, RecoveryAmount = a.RecoveryAmount });
        }
    }

    private static EmploymentHistory History(Employee e, EmploymentChangeKind kind, DateOnly on, string? remarks) => new()
    {
        EmployeeId = e.EmployeeId,
        EffectiveDate = on,
        ChangeKind = kind,
        DepartmentId = e.DepartmentId,
        DesignationId = e.DesignationId,
        GradeId = e.GradeId,
        WorkLocationId = e.WorkLocationId,
        ReportsToEmployeeId = e.ReportsToEmployeeId,
        Remarks = Blank(remarks),
    };

    private bool MaySeeSensitive(Employee e) =>
        _caller.Has(SalaryPermission) || (e.UserId is Guid own && own == _caller.UserId);

    private async Task<Employee?> LoadAsync(long employeeId, bool tracking, CancellationToken ct)
    {
        IQueryable<Employee> query = _db.Employees
            .Include(e => e.Addresses)
            .Include(e => e.Contacts)
            .Include(e => e.FamilyMembers)
            .Include(e => e.Nominees)
            .Include(e => e.Education)
            .Include(e => e.PreviousEmployments)
            .Include(e => e.BankDetails)
            .Include(e => e.Documents)
            .Include(e => e.AssetIssues)
            .AsSplitQuery();

        if (!tracking)
        {
            query = query.AsNoTracking();
        }

        return await query.FirstOrDefaultAsync(e => e.EmployeeId == employeeId, ct);
    }

    public static string FullName(string first, string? middle, string? last) =>
        string.Join(' ', new[] { first, middle, last }.Where(p => !string.IsNullOrWhiteSpace(p)));

    private DateOnly Today() => DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>A PAN as stored: trimmed, upper case, null when blank.</summary>
    public static string? NormalizePan(string? value) => Blank(value)?.ToUpperInvariant();

    /// <summary>An Aadhaar as stored: digits only, null when blank.</summary>
    public static string? NormalizeAadhaar(string? value) => Blank(value)?.Replace(" ", string.Empty);
}
