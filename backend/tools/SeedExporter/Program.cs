using System.Text.Json;
using System.Text.Json.Serialization;

namespace SeedExporter;

public static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    public static async Task Main(string[] args)
    {
        Console.WriteLine("=== Bill-Book Seed Data Standardization & JSON Exporter ===");

        string repoRoot = FindRepoRoot();
        Console.WriteLine($"Repository root: {repoRoot}");

        string apiRoot = Path.Combine(repoRoot, "backend", "Api");

        // 1. Accounting
        Console.WriteLine("Exporting Accounting seed data...");
        string accSeedDir = Path.Combine(apiRoot, "Accounting", "Accounting.Api", "SeedData");
        Directory.CreateDirectory(accSeedDir);
        await WriteJson(Path.Combine(accSeedDir, "Accounts.json"), Accounting.Repository.SeedData.ChartOfAccountsSeed.Build(Guid.Empty));
        await WriteJson(Path.Combine(accSeedDir, "PaymentTerms.json"), Accounting.Repository.SeedData.PaymentTermsSeed.Build(Guid.Empty));
        await WriteJson(Path.Combine(accSeedDir, "TaxMasters.json"), Accounting.Repository.SeedData.TaxMasterSeed.Build(Guid.Empty, new DateOnly(2025, 4, 1)));
        await WriteJson(Path.Combine(accSeedDir, "TdsSections.json"), Accounting.Repository.SeedData.TdsSectionSeed.Build(Guid.Empty));
        await WriteJson(Path.Combine(accSeedDir, "NumberingSeries.json"), Accounting.Repository.SeedData.NumberingSeriesSeed.Build(Guid.Empty));

        // 2. Inventory
        Console.WriteLine("Exporting Inventory seed data...");
        string invSeedDir = Path.Combine(apiRoot, "Inventory", "Inventory.Api", "SeedData");
        Directory.CreateDirectory(invSeedDir);
        await WriteJson(Path.Combine(invSeedDir, "UomTypes.json"), Inventory.Repository.SeedData.UomSeed.BuildTypes(Guid.Empty));
        await WriteJson(Path.Combine(invSeedDir, "MetalPurities.json"), Inventory.Repository.SeedData.MetalPuritiesSeed.Build(Guid.Empty));
        await WriteJson(Path.Combine(invSeedDir, "NumberingSeries.json"), Inventory.Repository.SeedData.NumberingSeriesSeed.Build(Guid.Empty));

        // 3. Master (AdminDb & ContactsDb)
        Console.WriteLine("Exporting Master seed data...");
        string mstSeedDir = Path.Combine(apiRoot, "Master", "Master.Api", "SeedData");
        Directory.CreateDirectory(mstSeedDir);
        await WriteJson(Path.Combine(mstSeedDir, "ContactPersonRoles.json"), Master.Repository.SeedData.ContactPersonRolesSeed.Build(Guid.Empty));
        await WriteJson(Path.Combine(mstSeedDir, "HsnSacCodes.json"), Master.Repository.SeedData.HsnSacSeed.Build());
        await WriteJson(Path.Combine(mstSeedDir, "Menus.json"), Master.Repository.SeedData.MenuSeed.Build());
        await WriteJson(Path.Combine(mstSeedDir, "MenuPermissions.json"), Master.Repository.SeedData.MenuSeed.BuildPermissions());

        // Copy states.json if present in Master.Repository
        string existingStates = Path.Combine(apiRoot, "Master", "Master.Repository", "SeedData", "states.json");
        if (File.Exists(existingStates))
        {
            File.Copy(existingStates, Path.Combine(mstSeedDir, "states.json"), overwrite: true);
            Console.WriteLine("  Copied states.json -> Master.Api/SeedData/states.json");
        }

        // 4. Customer
        Console.WriteLine("Exporting Customer seed data...");
        string cusSeedDir = Path.Combine(apiRoot, "Customer", "Customer.Api", "SeedData");
        Directory.CreateDirectory(cusSeedDir);
        await WriteJson(Path.Combine(cusSeedDir, "SlaPolicies.json"), Customer.Repository.SeedData.SlaPolicySeed.Build(Guid.Empty));

        // 5. Printing
        Console.WriteLine("Exporting Printing seed data...");
        string prtSeedDir = Path.Combine(apiRoot, "Printing", "Printing.Api", "SeedData");
        Directory.CreateDirectory(prtSeedDir);
        await WriteJson(Path.Combine(prtSeedDir, "PrintTemplates.json"), Printing.Repository.SeedData.PrintTemplateSeed.BuildForPaperSizes(Guid.Empty, []));
        await WriteJson(Path.Combine(prtSeedDir, "LabelTemplates.json"), Printing.Repository.SeedData.LabelTemplateSeed.Build(Guid.Empty, []));

        // 6. Sales
        Console.WriteLine("Exporting Sales seed data...");
        string salSeedDir = Path.Combine(apiRoot, "Sales", "Sales.Api", "SeedData");
        Directory.CreateDirectory(salSeedDir);
        await WriteJson(Path.Combine(salSeedDir, "NumberingSeries.json"), Sales.Repository.SeedData.NumberingSeriesSeed.Build(Guid.Empty));

        // 7. Purchase
        Console.WriteLine("Exporting Purchase seed data...");
        string purSeedDir = Path.Combine(apiRoot, "Purchase", "Purchase.Api", "SeedData");
        Directory.CreateDirectory(purSeedDir);
        await WriteJson(Path.Combine(purSeedDir, "NumberingSeries.json"), Purchase.Repository.SeedData.NumberingSeriesSeed.Build(Guid.Empty));

        // 8. Site
        Console.WriteLine("Exporting Site seed data...");
        string sitSeedDir = Path.Combine(apiRoot, "Site", "Site.Api", "SeedData");
        Directory.CreateDirectory(sitSeedDir);
        await WriteJson(Path.Combine(sitSeedDir, "NumberingSeries.json"), Site.Repository.SeedData.NumberingSeriesSeed.Build(Guid.Empty));

        // 9. Admission
        Console.WriteLine("Exporting Admission seed data...");
        string admSeedDir = Path.Combine(apiRoot, "Admission", "Admission.Api", "SeedData");
        Directory.CreateDirectory(admSeedDir);
        await WriteJson(Path.Combine(admSeedDir, "NumberingSeries.json"), new[] { Admission.Repository.SeedData.AdmissionSeed.ApplicationSeries(Guid.Empty) });

        // 10. Fee
        Console.WriteLine("Exporting Fee seed data...");
        string feeSeedDir = Path.Combine(apiRoot, "Fee", "Fee.Api", "SeedData");
        Directory.CreateDirectory(feeSeedDir);
        await WriteJson(Path.Combine(feeSeedDir, "FeeHeads.json"), Fee.Repository.SeedData.FeeSeed.Heads(Guid.Empty));
        await WriteJson(Path.Combine(feeSeedDir, "NumberingSeries.json"), new[] { Fee.Repository.SeedData.FeeSeed.DemandSeries(Guid.Empty), Fee.Repository.SeedData.FeeSeed.ReceiptSeries(Guid.Empty) });

        // 11. Student
        Console.WriteLine("Exporting Student seed data...");
        string stuSeedDir = Path.Combine(apiRoot, "Student", "Student.Api", "SeedData");
        Directory.CreateDirectory(stuSeedDir);
        await WriteJson(Path.Combine(stuSeedDir, "SchoolClasses.json"), Student.Repository.SeedData.StudentSeed.Classes(Guid.Empty));
        await WriteJson(Path.Combine(stuSeedDir, "NumberingSeries.json"), new[] { Student.Repository.SeedData.StudentSeed.AdmissionSeries(Guid.Empty) });

        // 12. Employee
        Console.WriteLine("Exporting Employee seed data...");
        string empSeedDir = Path.Combine(apiRoot, "Employee", "Employee.Api", "SeedData");
        Directory.CreateDirectory(empSeedDir);
        await WriteJson(Path.Combine(empSeedDir, "Departments.json"), new[] { Employee.Repository.SeedData.EmployeeSeed.Department(Guid.Empty) });
        await WriteJson(Path.Combine(empSeedDir, "Designations.json"), new[] { Employee.Repository.SeedData.EmployeeSeed.Designation(Guid.Empty) });
        await WriteJson(Path.Combine(empSeedDir, "Grades.json"), new[] { Employee.Repository.SeedData.EmployeeSeed.Grade(Guid.Empty) });
        await WriteJson(Path.Combine(empSeedDir, "WorkLocations.json"), new[] { Employee.Repository.SeedData.EmployeeSeed.WorkLocation(Guid.Empty) });
        await WriteJson(Path.Combine(empSeedDir, "RelationshipTypes.json"), Employee.Repository.SeedData.EmployeeSeed.RelationshipTypes(Guid.Empty));
        await WriteJson(Path.Combine(empSeedDir, "NumberingSeries.json"), new[] { Employee.Repository.SeedData.EmployeeSeed.EmployeeSeries(Guid.Empty) });

        // 13. Payroll
        Console.WriteLine("Exporting Payroll seed data...");
        string paySeedDir = Path.Combine(apiRoot, "Payroll", "Payroll.Api", "SeedData");
        Directory.CreateDirectory(paySeedDir);
        await WriteJson(Path.Combine(paySeedDir, "PayGroups.json"), new[] { Payroll.Repository.SeedData.PayrollSeed.DefaultPayGroup(Guid.Empty) });
        await WriteJson(Path.Combine(paySeedDir, "SalaryComponents.json"), new[] { Payroll.Repository.SeedData.PayrollSeed.BasicSalary(Guid.Empty) });
        await WriteJson(Path.Combine(paySeedDir, "PfSettings.json"), new[] { Payroll.Repository.SeedData.PayrollSeed.DefaultPf(Guid.Empty) });
        await WriteJson(Path.Combine(paySeedDir, "EsiSettings.json"), new[] { Payroll.Repository.SeedData.PayrollSeed.DefaultEsi(Guid.Empty) });
        await WriteJson(Path.Combine(paySeedDir, "GratuitySettings.json"), new[] { Payroll.Repository.SeedData.PayrollSeed.DefaultGratuity(Guid.Empty) });

        // 14. WorkOrder
        Console.WriteLine("Exporting WorkOrder seed data...");
        string wrkSeedDir = Path.Combine(apiRoot, "WorkOrder", "WorkOrder.Api", "SeedData");
        Directory.CreateDirectory(wrkSeedDir);
        await WriteJson(Path.Combine(wrkSeedDir, "NumberingSeries.json"), new[] { WorkOrder.Repository.SeedData.WorkOrderSeed.Series(Guid.Empty) });

        Console.WriteLine("=== All seed data successfully standardized and exported to JSON! ===");
    }

    private static async Task WriteJson(string filePath, object data)
    {
        string json = JsonSerializer.Serialize(data, JsonOptions);
        await File.WriteAllTextAsync(filePath, json);
        Console.WriteLine($"  Created: {filePath} ({json.Length:N0} bytes)");
    }

    private static string FindRepoRoot()
    {
        string current = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(current))
        {
            if (File.Exists(Path.Combine(current, "BillBook.sln")) || Directory.Exists(Path.Combine(current, ".git")))
            {
                return current;
            }
            string? parent = Directory.GetParent(current)?.FullName;
            if (parent == current) break;
            current = parent!;
        }
        return Directory.GetCurrentDirectory();
    }
}
