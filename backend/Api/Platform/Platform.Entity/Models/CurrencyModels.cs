using System.ComponentModel.DataAnnotations;

namespace Platform.Entity.Models;

/// <summary>A currency enabled for an organization, joined to its master row for display.</summary>
public class OrgCurrencyDto
{
    public Guid OrgCurrencyId { get; set; }

    public int CurrencyId { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string Symbol { get; set; } = null!;

    public string Format { get; set; } = null!;

    public int DecimalPlaces { get; set; }

    public bool IsBaseCurrency { get; set; }

    public bool IsActive { get; set; }
}

public class AddOrgCurrencyRequest
{
    [Required(ErrorMessage = "Currency is required.")]
    [Range(1, int.MaxValue, ErrorMessage = "Currency is required.")]
    public int CurrencyId { get; set; }
}

public class SetOrgCurrencyActiveRequest
{
    public bool IsActive { get; set; }
}
