using System.Globalization;
using System.Text.RegularExpressions;
using Sales.Entity.Enums;
using Sales.Entity.TableEntities;
using Shared.Kernel.Contacts;
using Shared.Kernel.Documents;
using Shared.Kernel.Tenancy;

namespace Sales.Api.Services.EInvoicing;

/// <summary>
/// The pure half of e-way bills (TK-93): the request a document makes, the
/// checks before it is sent, and what a generated bill writes back. No I/O.
/// </summary>
public static partial class EwayBillMapper
{
    [GeneratedRegex("^[0-9]{4,8}$")]
    private static partial Regex HsnCode();

    [GeneratedRegex("^[A-Z]{2}[0-9A-Z]{1,3}[0-9A-Z]{1,4}[0-9]{4}$|^TM[0-9A-Z]{4,10}$")]
    private static partial Regex VehicleNumber();

    /// <summary>The transport block INV-01 carries when the bill is asked for with the IRN.</summary>
    public static Inv01EwayBill Details(EwayTransport transport) => new()
    {
        TransId = transport.TransporterId,
        TransName = transport.TransporterName,
        Distance = transport.DistanceKm,
        TransMode = ((int)transport.Mode).ToString(CultureInfo.InvariantCulture),
        VehNo = transport.VehicleNo,
        VehType = transport.VehicleNo is null ? null : "R",
    };

    /// <summary>
    /// The portal's sub-supply type for a challan: why the goods move. Only a
    /// sale is a supply; job work is its own type; a branch transfer is for the
    /// business's own use; approval and samples are "others".
    /// </summary>
    public static int SubSupplyTypeFor(ChallanType type) => type switch
    {
        ChallanType.Sale => 1,
        ChallanType.JobWork => 4,
        ChallanType.BranchTransfer => 5,
        _ => 8,
    };

    /// <summary>A standalone request from a posted document, its lines, and the two parties.</summary>
    public static StandaloneEwayBillRequest Standalone(
        string documentType,
        int subSupplyType,
        DocumentHeaderBase header,
        IEnumerable<(DocumentLineBase Line, string? Uqc, IReadOnlyList<DocumentLineTaxBase> Taxes)> lines,
        IReadOnlyDictionary<long, string> itemNames,
        OrgIdentity seller,
        ContactPostalAddress buyer,
        EwayTransport transport) =>
        new(
            documentType,
            header.DocumentNo,
            header.DocumentDate,
            subSupplyType,
            new Inv01Party
            {
                Gstin = seller.Gstin ?? string.Empty,
                LglNm = seller.Name,
                Addr1 = seller.AddressLine1 ?? string.Empty,
                Addr2 = seller.AddressLine2,
                Loc = seller.City ?? string.Empty,
                Pin = Pin(seller.PostalCode),
                Stcd = seller.StateCode ?? string.Empty,
            },
            new Inv01Party
            {
                Gstin = string.IsNullOrWhiteSpace(header.ContactGstin) ? buyer.Gstin ?? Inv01Mapper.UnregisteredBuyer : header.ContactGstin,
                LglNm = buyer.LegalName,
                Addr1 = buyer.AddressLine1 ?? string.Empty,
                Addr2 = buyer.AddressLine2,
                Loc = buyer.City ?? string.Empty,
                Pin = Pin(buyer.PostalCode),
                Stcd = buyer.StateCode ?? Shared.Kernel.Tax.Gstin.StateCodeOf(buyer.Gstin) ?? string.Empty,
            },
            [.. lines.OrderBy(l => l.Line.LineNumber).Select(l => new EwayItem(
                (l.Line.HsnSacCode ?? string.Empty).Trim(),
                l.Line.Description ?? (l.Line.ItemId is long id && itemNames.TryGetValue(id, out string? name) ? name : null),
                l.Line.Quantity,
                l.Uqc,
                l.Line.TaxableAmount,
                Rate(l.Taxes, TaxComponent.Cgst),
                Rate(l.Taxes, TaxComponent.Sgst) + Rate(l.Taxes, TaxComponent.Utgst),
                Rate(l.Taxes, TaxComponent.Igst),
                Rate(l.Taxes, TaxComponent.Cess)))],
            header.TotalAmount,
            transport);

    /// <summary>What the portal would refuse, checked first, in plain words.</summary>
    public static IReadOnlyList<EInvoiceProblem> Validate(StandaloneEwayBillRequest request)
    {
        var problems = new List<EInvoiceProblem>();

        if (!Shared.Kernel.Tax.Gstin.IsValid(request.From.Gstin))
        {
            problems.Add(new("SELLER_GSTIN", "The branch's GSTIN is missing or is not a valid GSTIN."));
        }

        if (request.To.Gstin != Inv01Mapper.UnregisteredBuyer && !Shared.Kernel.Tax.Gstin.IsValid(request.To.Gstin))
        {
            problems.Add(new("BUYER_GSTIN", "The customer's GSTIN is not a valid GSTIN."));
        }

        if (request.From.Pin is < 100000 or > 999999)
        {
            problems.Add(new("SELLER_PIN", "The PIN code of the branch must be six digits."));
        }

        if (request.To.Pin is < 100000 or > 999999)
        {
            problems.Add(new("BUYER_PIN", "The PIN code of the customer must be six digits."));
        }

        if (request.Items.Count == 0)
        {
            problems.Add(new("ITEMS", "The document has no lines."));
        }

        foreach ((EwayItem item, int index) in request.Items.Select((item, i) => (item, i + 1)))
        {
            if (!HsnCode().IsMatch(item.HsnCode))
            {
                problems.Add(new("HSN", $"Line {index} needs an HSN code of at least 4 digits."));
            }
        }

        problems.AddRange(ValidateTransport(request.Transport));
        return problems;
    }

    /// <summary>Part B: a road bill needs a vehicle, or a transporter who will enter it later.</summary>
    public static IReadOnlyList<EInvoiceProblem> ValidateTransport(EwayTransport transport)
    {
        var problems = new List<EInvoiceProblem>();

        if (transport.DistanceKm is < 0 or > 4000)
        {
            problems.Add(new("DISTANCE", "The distance must be between 0 and 4000 km."));
        }

        if (transport.VehicleNo is null && transport.TransporterId is null)
        {
            problems.Add(new("PART_B", "Give the vehicle number, or the transporter's id so they can enter it later."));
        }

        if (transport.VehicleNo is string vehicle && transport.Mode == TransportMode.Road && !VehicleNumber().IsMatch(vehicle))
        {
            problems.Add(new("VEHICLE", "The vehicle number is not in the form the portal takes, such as TN01AB1234."));
        }

        if (transport.TransporterId is string id && !Shared.Kernel.Tax.Gstin.IsValid(id) && !(id.Length == 15 && id.All(char.IsAsciiLetterOrDigit)))
        {
            problems.Add(new("TRANSPORTER", "The transporter id must be their 15-character GSTIN or enrolment id."));
        }

        return problems;
    }

    /// <summary>A generated bill written onto its row.</summary>
    public static EwayBill Generated(EwayBill row, EwayTransport transport, EwayBillDetails details)
    {
        row.Status = EwayBillStatus.Generated;
        row.EwbNo = details.EwbNo;
        row.EwbDate = details.EwbDate;
        row.ValidUntil = details.ValidUntil;
        Apply(row, transport);
        row.LastErrorCode = null;
        row.LastErrorMessage = null;
        return row;
    }

    public static void Apply(EwayBill row, EwayTransport transport)
    {
        row.TransportMode = transport.Mode;
        row.DistanceKm = transport.DistanceKm;
        row.VehicleNo = transport.VehicleNo;
        row.TransporterId = transport.TransporterId;
        row.TransporterName = transport.TransporterName;
    }

    public static EwayTransport TransportOf(EwayBill row) =>
        new(row.TransportMode, row.DistanceKm, row.VehicleNo, row.TransporterId, row.TransporterName);

    private static decimal Rate(IReadOnlyList<DocumentLineTaxBase> taxes, TaxComponent component) =>
        taxes.Where(t => t.TaxComponent == component).Select(t => t.Rate).FirstOrDefault();

    private static int Pin(string? postalCode) =>
        postalCode?.Trim() is { Length: 6 } pin && int.TryParse(pin, NumberStyles.None, CultureInfo.InvariantCulture, out int value)
            ? value
            : 0;
}
