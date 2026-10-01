namespace Shared.Kernel.Apps;

/// <summary>
/// The trade or business vertical of a branch (D-36, TK-126).
/// One per branch, nullable: a branch can have no business type if the customer
/// bought only standalone add-ons like HRMS or Payroll.
/// </summary>
public enum BusinessType
{
    /// <summary>General retail/trading branch. Seeded with and shown all general retail features.</summary>
    General = 1,

    /// <summary>Pharmacy retail branch with batch, expiry, and drug schedule tracking.</summary>
    Pharma = 2,

    /// <summary>Jewellery retail branch with metal purity, making charges, and hallmarking.</summary>
    Jewellery = 3,

    /// <summary>School/educational institution branch with students, terms, exams, and fees.</summary>
    School = 4,

    /// <summary>Services business (e.g. repairs, consultants).</summary>
    Service = 5,

    /// <summary>Restaurant or food service.</summary>
    Restaurant = 6,
}

/// <summary>
/// Standalone or modular add-ons that can be enabled for an organization (D-36, TK-126).
/// Flags enum stored as an integer bitmask.
/// </summary>
[Flags]
public enum AddOn
{
    None = 0,

    /// <summary>Human Resource Management System: employees, departments, designations.</summary>
    Hrms = 1 << 0,

    /// <summary>Payroll processing: salary structures, pay runs, payslips, statutory deductions.</summary>
    Payroll = 1 << 1,

    /// <summary>Customer Relationship Management: leads, pipelines, deals, opportunities.</summary>
    Crm = 1 << 2,

    /// <summary>Point of Sale: quick billing, barcode scanning, shift and cash drawer management.</summary>
    Pos = 1 << 3,

    /// <summary>Fixed Assets management: asset register, depreciation schedules, asset disposal.</summary>
    FixedAssets = 1 << 4,

    /// <summary>Client / Customer portal: customer statement, outstanding invoices, payments.</summary>
    ClientPortal = 1 << 5,

    /// <summary>E-Invoicing and E-Way bill generation and compliance via GSTN portal.</summary>
    EInvoicing = 1 << 6,

    /// <summary>Budgeting: expense and revenue budget tracking with approval variance limits.</summary>
    Budget = 1 << 7,

    /// <summary>Recurring billing: subscription schedules, automatic invoice generation.</summary>
    RecurringBilling = 1 << 8,

    /// <summary>Savings Scheme: customer enrolments and bonus maturity (Jewellery only).</summary>
    SavingsScheme = 1 << 9,

    /// <summary>All add-ons bitwise combined.</summary>
    All = Hrms | Payroll | Crm | Pos | FixedAssets | ClientPortal | EInvoicing | Budget | RecurringBilling | SavingsScheme,
}

/// <summary>
/// Business rules governing which <see cref="AddOn"/> flags are allowed for a given <see cref="BusinessType"/> (D-36, TK-126).
/// </summary>
public static class AddOnRules
{
    /// <summary>
    /// Returns the combined flags of all add-ons allowed for the specified business type.
    /// <para>
    /// Rules:
    /// - <see cref="AddOn.Hrms"/> and <see cref="AddOn.Payroll"/> are allowed for every type and for none (null).
    /// - <see cref="AddOn.Crm"/>, <see cref="AddOn.FixedAssets"/>, <see cref="AddOn.ClientPortal"/>,
    ///   <see cref="AddOn.Budget"/>, and <see cref="AddOn.RecurringBilling"/> are allowed for all four types.
    /// - <see cref="AddOn.Pos"/> and <see cref="AddOn.EInvoicing"/> are allowed for General, Pharma, and Jewellery (not School, not null).
    /// </para>
    /// </summary>
    public static AddOn Allowed(BusinessType? businessType) =>
        businessType switch
        {
            null => AddOn.Hrms | AddOn.Payroll,
            BusinessType.School or BusinessType.Service => AddOn.Hrms | AddOn.Payroll | AddOn.Crm | AddOn.FixedAssets | AddOn.ClientPortal | AddOn.Budget | AddOn.RecurringBilling,
            BusinessType.General or BusinessType.Pharma => AddOn.All & ~AddOn.SavingsScheme,
            BusinessType.Restaurant => AddOn.All & ~AddOn.SavingsScheme & ~AddOn.Pos,
            BusinessType.Jewellery => AddOn.All,
            _ => AddOn.None,
        };

    /// <summary>
    /// Returns the combined flags of all add-ons that are free to enable (no separate purchase required) 
    /// for the specified business type.
    /// </summary>
    public static AddOn Free(BusinessType? businessType)
    {
        return Allowed(businessType) & (AddOn.RecurringBilling);
    }

    /// <summary>
    /// Determines whether the requested add-on(s) are allowed for the given business type.
    /// Returns true if every flag in <paramref name="addOn"/> is permitted.
    /// </summary>
    public static bool IsAllowed(BusinessType? businessType, AddOn addOn)
    {
        AddOn allowed = Allowed(businessType);
        return (allowed & addOn) == addOn;
    }

    /// <summary>
    /// Returns true if <paramref name="businessType"/> is a defined <see cref="BusinessType"/> value.
    /// </summary>
    public static bool IsDefined(BusinessType businessType) =>
        businessType is BusinessType.General or BusinessType.Pharma or BusinessType.Jewellery or BusinessType.School or BusinessType.Service or BusinessType.Restaurant;

    /// <summary>
    /// Parses a string into a <see cref="BusinessType"/>.
    /// An empty, null, or whitespace string evaluates to null (no business type).
    /// </summary>
    public static bool TryParseBusinessType(string? value, out BusinessType? businessType)
    {
        businessType = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (int.TryParse(value, out _))
        {
            return false;
        }

        if (Enum.TryParse<BusinessType>(value.Trim(), ignoreCase: true, out var parsed) && IsDefined(parsed))
        {
            businessType = parsed;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Parses a string into <see cref="AddOn"/> flags.
    /// Supports numeric bitmasks and comma-separated enum names (e.g. "Hrms, Payroll").
    /// </summary>
    public static bool TryParseAddOn(string? value, out AddOn addOn)
    {
        addOn = AddOn.None;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (int.TryParse(value, out int intVal))
        {
            var fromInt = (AddOn)intVal;
            if ((fromInt & ~AddOn.All) == 0)
            {
                addOn = fromInt;
                return true;
            }
            return false;
        }

        if (Enum.TryParse<AddOn>(value.Trim(), ignoreCase: true, out var parsed) &&
            (parsed & ~AddOn.All) == 0)
        {
            addOn = parsed;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Enumerates each individual non-zero <see cref="AddOn"/> flag present in the provided mask.
    /// </summary>
    public static IEnumerable<AddOn> Split(AddOn addOns)
    {
        if (addOns == AddOn.None)
        {
            yield break;
        }

        foreach (AddOn flag in Enum.GetValues<AddOn>())
        {
            if (flag != AddOn.None && flag != AddOn.All && (addOns & flag) == flag)
            {
                yield return flag;
            }
        }
    }
}

/// <summary>
/// Deprecated: Replaced by <see cref="BusinessType"/> and <see cref="AddOn"/> (D-36, TK-126).
/// Kept as a compatibility bridge until TK-127 (Master migration) and TK-128 (services) complete.
/// </summary>
[Flags]
public enum App
{
    None = 0,
    RetailErp = 1,
    School = 2,
    Hrms = 4,
    Payroll = 8,

    /// <summary>Every app: the shared master screens and their permissions.</summary>
    All = RetailErp | School | Hrms | Payroll,
}

/// <summary>Rules about <see cref="App"/> values that more than one service needs.</summary>
public static class AppRules
{
    /// <summary>True when <paramref name="app"/> names exactly one app.</summary>
    public static bool IsSingle(App app) =>
        app is App.RetailErp or App.School or App.Hrms or App.Payroll;

    /// <summary>
    /// The grant rule: a role of one app may hold a permission only when the
    /// permission's apps include that app.
    /// </summary>
    public static bool MayGrant(App roleApp, App permissionApps) =>
        IsSingle(roleApp) && (permissionApps & roleApp) == roleApp;

    /// <summary>
    /// Reads an app from a claim or a query string, by name, ignoring case.
    /// Only a single app is accepted.
    /// </summary>
    public static bool TryParseSingle(string? value, out App app)
    {
        app = App.None;
        if (string.IsNullOrWhiteSpace(value) || int.TryParse(value, out _))
        {
            return false;
        }

        return Enum.TryParse(value.Trim(), ignoreCase: true, out app) && IsSingle(app);
    }
}



