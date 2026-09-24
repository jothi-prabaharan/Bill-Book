namespace Shared.Kernel.Apps;

/// <summary>
/// The four products one customer can buy (H0.1, TK-42): RetailErp, School,
/// HRMS and Payroll. Each is licensed on its own, and a token is minted for one.
///
/// <b>A flags enum because a permission or a menu can belong to several apps</b>,
/// while a role, a licence and a refresh token belong to exactly one. The users,
/// roles and branches screens have one permission code and one menu row, marked
/// with every app that shows them, rather than a copy per app.
///
/// <b>In <c>Shared.Kernel</c>, not <c>Master.Entity</c></b>, because every service
/// checks a token's <c>app</c> claim against its controllers (<c>[RequireApp]</c>,
/// TK-43), and no service may reference Master's entities.
///
/// Not <c>Vertical</c>, which is the trade inside RetailErp (General, Pharma,
/// Jewellery).
///
/// Stored as its integer value, so a set of flags is one column.
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
