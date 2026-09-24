namespace RateSync.Worker.Ibja;

public interface IIbjaClient
{
    Task<IbjaRates> FetchRatesAsync(string apiKey, CancellationToken ct);
}

public class IbjaRates
{
    public DateOnly Date { get; set; }
    public Dictionary<string, decimal> Rates { get; set; } = new();
}
