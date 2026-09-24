namespace Master.Entity.Enums;

/// <summary>
/// Where a rate in <c>rat</c> came from (TK-24). Exchange rates are entered by
/// hand or scraped from RBI's reference-rate page (D-03); metal rates are
/// entered by hand or fetched from IBJA's paid API (D-14).
/// </summary>
public enum RateSource
{
    Manual = 1,
    Rbi = 2,
    Ibja = 3,
}
