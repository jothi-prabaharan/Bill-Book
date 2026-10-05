using Shared.Kernel.Apps;
using Shared.Kernel.Internal;
using Shared.Kernel.Restaurant;

namespace Reporting.Repository.Repositories;

/// <summary>
/// What the caller's branch is, as far as which reports it sees: its add-ons,
/// its trade and, on a Restaurant branch, its switches (TK-150).
///
/// <see cref="Unrestricted"/> is the audience of a caller with no branch scope
/// to apply — a scheduled run, which was authorised when it was saved.
/// </summary>
public sealed record ReportAudience(
    AddOn AddOns,
    BusinessType? BusinessType,
    RestaurantSettingsView? Restaurant,
    bool Everything = false)
{
    public static ReportAudience Unrestricted(AddOn addOns = AddOn.All) => new(addOns, null, null, true);

    /// <summary>
    /// Whether <paramref name="source"/> is for this audience. Pure. A source
    /// naming a restaurant switch is hidden when the settings could not be read,
    /// so an outage at Master fails closed rather than showing a report the
    /// branch has turned off.
    /// </summary>
    public bool Allows(IReportSourceRepository source) =>
        (source.RequiredAddOn == AddOn.None || (AddOns & source.RequiredAddOn) != 0)
        && (Everything || source.RequiredBusinessType is null || BusinessType == source.RequiredBusinessType)
        && (Everything
            || source.RequiredRestaurantFeature is not RestaurantFeature feature
            || (Restaurant is not null && RequireRestaurantFeatureAttribute.Refusal(feature, Restaurant) is null));
}

/// <summary>
/// Builds a caller's <see cref="ReportAudience"/>. The restaurant settings are
/// read only for a Restaurant branch, through the provider's one-minute cache,
/// so every other branch's catalog costs nothing extra.
/// </summary>
public sealed class ReportAudienceRepository
{
    private readonly IRestaurantSettingsProvider _restaurant;

    public ReportAudienceRepository(IRestaurantSettingsProvider restaurant) => _restaurant = restaurant;

    public async Task<ReportAudience> ForAsync(AddOn addOns, BusinessType? businessType, CancellationToken ct) =>
        new(
            addOns,
            businessType,
            businessType == BusinessType.Restaurant ? await _restaurant.GetSettingsAsync(ct) : null);
}
