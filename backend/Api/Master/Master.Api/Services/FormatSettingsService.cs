using Master.Entity.Enums;
using Master.Entity.Models;
using Master.Entity.TableEntities;
using Master.Repository;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Tenancy;

namespace Master.Api.Services;

/// <summary>
/// Assembles the branch's display formats from the two places that already own
/// them: <c>mst.Currency</c> for the money half and <c>mst.Configuration</c>
/// for the rest.
///
/// <b>Why this is a service and not a fifth config category.</b> Every field
/// here except the date pattern was already modelled somewhere, and the
/// tempting shortcut — copy them all into <c>Configuration</c> so one query
/// answers — would make a currency's symbol editable in two places that
/// disagree. Reading from the owners costs one extra query and keeps one
/// answer.
/// </summary>
public sealed class FormatSettingsService
{
    private readonly AdminDbContext _db;
    private readonly ITenantContext _tenant;

    public FormatSettingsService(AdminDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    /// <summary>
    /// The formats for the caller's own branch. The org comes from the tenant
    /// context rather than a parameter, so there is no id for a caller to
    /// substitute and no cross-org check to forget.
    /// </summary>
    public async Task<FormatSettingsDto> GetAsync(CancellationToken ct)
    {
        (_, Guid orgId) = _tenant.Require();

        // The effective value of every Formatting-ish key in one pass: the
        // org's override where it exists, the shipped default otherwise. Two
        // rows can share a Code — that is what an override is — so this cannot
        // be a ToDictionary on Code alone.
        List<Configuration> configs = await _db.Configurations
            .Where(c => c.OrgId == null || c.OrgId == orgId)
            .AsNoTracking()
            .ToListAsync(ct);

        string Effective(string code, string fallback) =>
            configs.FirstOrDefault(c => c.Code == code && c.OrgId == orgId)?.Value
            ?? configs.FirstOrDefault(c => c.Code == code && c.OrgId == null)?.Value
            ?? fallback;

        int EffectiveInt(string code, int fallback) =>
            int.TryParse(Effective(code, string.Empty), out int parsed) ? parsed : fallback;

        var settings = new FormatSettingsDto
        {
            DatePattern = Effective("format.date", "dd/MM/yyyy"),
            UnitPriceDecimals = EffectiveInt("unitPrice.decimals", 2),
            QuantityDecimals = EffectiveInt("quantity.decimals", 2),
        };

        // The branch's base currency, if it has declared one. A branch mid-setup
        // may not have, and a screen that cannot draw an amount is worse than
        // one drawing it in the shipped default — so the DTO's own defaults
        // stand rather than throwing.
        Currency? baseCurrency = await _db.OrgCurrencies
            .Where(oc => oc.OrgId == orgId && oc.IsBaseCurrency && oc.IsActive)
            .Join(
                _db.Currencies,
                oc => oc.CurrencyId,
                c => c.CurrencyId,
                (oc, c) => c)
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);

        if (baseCurrency is not null)
        {
            settings.CurrencyCode = baseCurrency.Code;
            settings.CurrencySymbol = baseCurrency.Symbol;
            settings.CurrencyMask = baseCurrency.Format;
            settings.CurrencyDecimals = baseCurrency.DecimalPlaces;
            settings.SymbolPosition = baseCurrency.SymbolPosition == SymbolPosition.Suffix
                ? nameof(SymbolPosition.Suffix)
                : nameof(SymbolPosition.Prefix);
        }

        return settings;
    }
}
