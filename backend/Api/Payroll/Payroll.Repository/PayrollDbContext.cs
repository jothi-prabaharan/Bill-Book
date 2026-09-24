using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Tenancy;
using Payroll.Entity.TableEntities;

namespace Payroll.Repository;

public class PayrollDbContext : TenantDbContext
{
    public PayrollDbContext(DbContextOptions<PayrollDbContext> options, TenantContext tenantContext)
        : base(options, tenantContext)
    {
    }

    public DbSet<PayGroup> PayGroups => Set<PayGroup>();
    public DbSet<SalaryComponent> SalaryComponents => Set<SalaryComponent>();
    public DbSet<SalaryStructure> SalaryStructures => Set<SalaryStructure>();
    public DbSet<SalaryStructureComponent> SalaryStructureComponents => Set<SalaryStructureComponent>();
    public DbSet<EmployeeSalary> EmployeeSalaries => Set<EmployeeSalary>();
    public DbSet<SalaryRevision> SalaryRevisions => Set<SalaryRevision>();
    public DbSet<OneTimePayment> OneTimePayments => Set<OneTimePayment>();
    public DbSet<SalaryHold> SalaryHolds => Set<SalaryHold>();
    public DbSet<EmployeeLoan> EmployeeLoans => Set<EmployeeLoan>();
    public DbSet<LoanRepayment> LoanRepayments => Set<LoanRepayment>();
    public DbSet<MonthlyAttendanceInput> MonthlyAttendanceInputs => Set<MonthlyAttendanceInput>();
    public DbSet<PayrollRun> PayrollRuns => Set<PayrollRun>();
    public DbSet<Payslip> Payslips => Set<Payslip>();
    public DbSet<PayslipLine> PayslipLines => Set<PayslipLine>();
    public DbSet<PfSetting> PfSettings => Set<PfSetting>();
    public DbSet<EsiSetting> EsiSettings => Set<EsiSetting>();
    public DbSet<ProfessionalTaxSlab> ProfessionalTaxSlabs => Set<ProfessionalTaxSlab>();
    public DbSet<LwfSetting> LwfSettings => Set<LwfSetting>();
    public DbSet<GratuitySetting> GratuitySettings => Set<GratuitySetting>();
    public DbSet<BonusSetting> BonusSettings => Set<BonusSetting>();
    public DbSet<StatutoryReturn> StatutoryReturns => Set<StatutoryReturn>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("pay");

        modelBuilder.Entity<SalaryStructureComponent>(b =>
        {
            b.HasOne(e => e.Structure).WithMany(s => s.Components).HasForeignKey(e => e.SalaryStructureId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(e => e.Component).WithMany().HasForeignKey(e => e.SalaryComponentId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Payslip>(b =>
        {
            b.HasOne(e => e.Run).WithMany(r => r.Payslips).HasForeignKey(e => e.PayrollRunId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PayslipLine>(b =>
        {
            b.HasOne(e => e.Payslip).WithMany(p => p.Lines).HasForeignKey(e => e.PayslipId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<EmployeeSalary>(b =>
        {
            b.HasOne(e => e.Structure).WithMany().HasForeignKey(e => e.SalaryStructureId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<LoanRepayment>(b =>
        {
            b.HasOne(e => e.Loan).WithMany(l => l.Repayments).HasForeignKey(e => e.EmployeeLoanId).OnDelete(DeleteBehavior.Cascade);
        });

        // Last, so the base class sees every entity configured above.
        base.OnModelCreating(modelBuilder);
    }
}
