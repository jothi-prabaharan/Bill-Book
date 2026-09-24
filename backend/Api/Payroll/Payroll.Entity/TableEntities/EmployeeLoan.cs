using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Entities;
using Shared.Kernel.Tenancy;

namespace Payroll.Entity.TableEntities;

public class EmployeeLoan : OrgScopedEntity
{
    public long EmployeeLoanId { get; set; }
    public long EmployeeId { get; set; }

    public decimal PrincipalAmount { get; set; }
    public decimal InterestRate { get; set; }
    public int TermMonths { get; set; }
    public decimal MonthlyEmi { get; set; }

    public DateOnly DisbursedDate { get; set; }
    public DateOnly DeductionStartDate { get; set; }
    
    public bool IsSettled { get; set; }

    public ICollection<LoanRepayment> Repayments { get; set; } = new List<LoanRepayment>();
}
