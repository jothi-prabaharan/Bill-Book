using Sales.Entity.TableEntities;

namespace Sales.Api.Services.Pdf;

public class PdfInvoiceModel
{
    public Invoice Invoice { get; set; } = null!;
    public string OrgName { get; set; } = null!;
    public string? OrgGstin { get; set; }
    public string? OrgAddress { get; set; }
    public string CustomerName { get; set; } = null!;
    public System.Collections.Generic.Dictionary<long, string> ItemNames { get; set; } = new();
}

public interface IInvoicePdfRenderer
{
    byte[] Render(PdfInvoiceModel model);
}
