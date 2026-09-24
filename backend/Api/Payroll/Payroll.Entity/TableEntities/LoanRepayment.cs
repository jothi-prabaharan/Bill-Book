using System.ComponentModel.DataAnnotations;
using Shared.Kernel.Entities;
using Shared.Kernel.Tenancy;

namespace Payroll.Entity.TableEntities;

public class LoanRepayment : OrgScopedEntity
{
    public long LoanRepaymentId { get; set; }
    public long EmployeeLoanId { get; set; }

    public DateOnly Month { get; set; }
    
    public decimal PrincipalComponent { get; set; }
    public decimal InterestComponent { get; set; }
    
    public bool Processed { get; set; }

    public EmployeeLoan Loan { get; set; } = null!;
}
