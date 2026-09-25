using Microsoft.EntityFrameworkCore;
using Sales.Entity.Enums;
using Sales.Entity.Models;
using Sales.Entity.TableEntities;
using Sales.Repository;
using Shared.Kernel.Contacts;
using Shared.Kernel.Documents;
using Shared.Kernel.Persistence;
using Shared.Kernel.Tenancy;

namespace Sales.Api.Services.EInvoicing;

/// <summary>
/// Where posting and voiding a sales document meet e-invoicing (TK-92). The
/// document services call this and know nothing else about the IRP.
/// </summary>
public interface IEInvoicePosting
{
    /// <summary>
    /// Inside the posting's transaction: writes a Pending e-invoice when the
    /// document needs an IRN, and asks for one registration attempt once the
    /// posting has committed. Null when the document needs none.
    /// </summary>
    Task<EInvoiceStateView?> OnPostedAsync(EInvoiceSource source, long sourceId, DocumentHeaderBase document, CancellationToken ct);

    /// <summary>
    /// Before anything else a void changes: cancels the document's IRN. Null
    /// when the void may go ahead, otherwise the reason it may not.
    /// </summary>
    Task<string?> BeforeVoidAsync(
        EInvoiceSource source, long sourceId, string reason, EInvoiceCancelReason? cancelReason, CancellationToken ct);
}

/// <inheritdoc />
public sealed class EInvoicePosting : IEInvoicePosting
{
    private readonly SalesDbContext _db;
    private readonly IBranchSettingsProvider _settings;
    private readonly IContactAddressBook _addresses;
    private readonly IOrgIdentityProvider _identity;
    private readonly IEInvoiceGateway _gateway;
    private readonly IAfterCommit _afterCommit;
    private readonly EInvoiceRegistrar _registrar;
    private readonly TimeProvider _clock;

    public EInvoicePosting(
        SalesDbContext db,
        IBranchSettingsProvider settings,
        IContactAddressBook addresses,
        IOrgIdentityProvider identity,
        IEInvoiceGateway gateway,
        IAfterCommit afterCommit,
        EInvoiceRegistrar registrar,
        TimeProvider clock)
    {
        _db = db;
        _settings = settings;
        _addresses = addresses;
        _identity = identity;
        _gateway = gateway;
        _afterCommit = afterCommit;
        _registrar = registrar;
        _clock = clock;
    }

    public async Task<EInvoiceStateView?> OnPostedAsync(
        EInvoiceSource source, long sourceId, DocumentHeaderBase document, CancellationToken ct)
    {
        // A till sale is a consumer sale: never registered.
        if (document.TransactionTypeCode == "POS")
        {
            return null;
        }

        BranchSettings? settings = await _settings.GetSettingsAsync(ct);
        if (settings is null || !EInvoiceApplicability.BranchApplies(settings.EInvoiceFrom, document.DocumentDate))
        {
            return null;
        }

        if (!await LikelyNeedsIrnAsync(document, ct))
        {
            return null;
        }

        EInvoice? row = await _db.EInvoices.FirstOrDefaultAsync(e => e.SourceType == source && e.SourceId == sourceId, ct);
        if (row is null)
        {
            row = new EInvoice { SourceType = source, SourceId = sourceId, Status = EInvoiceStatus.Pending };
            _db.EInvoices.Add(row);
            await _db.SaveChangesAsync(ct);
        }

        EInvoiceStateView view = EInvoiceRegistrar.Fill(null, row);
        long id = row.EInvoiceId;

        // After the commit, never inside it: the IRP can take seconds, and a
        // timeout must not roll back a posting it may already have registered.
        _afterCommit.Enqueue(token => _registrar.RegisterAsync(id, view, token));
        return view;
    }

    /// <summary>
    /// A buyer with a GSTIN, or one outside India or in an SEZ, needs an IRN.
    /// When the buyer cannot be looked up the row is written anyway; the
    /// registrar removes it if the document turns out to need none, which is
    /// better than an export silently going unregistered.
    /// </summary>
    private async Task<bool> LikelyNeedsIrnAsync(DocumentHeaderBase document, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(document.ContactGstin))
        {
            return true;
        }

        try
        {
            IReadOnlyDictionary<long, ContactPostalAddress> found = await _addresses.FindAsync([document.ContactId], ct);
            return found.TryGetValue(document.ContactId, out ContactPostalAddress? buyer)
                && EInvoiceApplicability.SupplyTypeFor(buyer.RegistrationType, buyer.Gstin, document.IgstAmount > 0) is not null;
        }
        catch (HttpRequestException)
        {
            return true;
        }
    }

    public async Task<string?> BeforeVoidAsync(
        EInvoiceSource source, long sourceId, string reason, EInvoiceCancelReason? cancelReason, CancellationToken ct)
    {
        EInvoice? row = await _db.EInvoices.FirstOrDefaultAsync(e => e.SourceType == source && e.SourceId == sourceId, ct);
        if (row is null || row.Status == EInvoiceStatus.Cancelled)
        {
            return null;
        }

        DateTimeOffset now = _clock.GetUtcNow();
        EInvoiceCancelReason code = cancelReason ?? EInvoiceCancelReason.Other;
        string remark = reason.Length <= 100 ? reason : reason[..100];

        if (row.Status == EInvoiceStatus.Registered)
        {
            if (row.AckDate is not DateTimeOffset ackDate || now - ackDate > EInvoiceRules.CancelWindow)
            {
                return "This document's IRN was issued more than 24 hours ago, so it can no longer be cancelled "
                    + "and the document cannot be voided. Raise a credit note instead.";
            }

            if ((await _identity.GetIdentityAsync(ct))?.Gstin is not { Length: > 0 } gstin)
            {
                return "The branch's GSTIN could not be read, so the IRN could not be cancelled. Try again in a moment.";
            }

            IrpResult<IrnCancellation> cancelled;
            try
            {
                cancelled = await _gateway.CancelIrnAsync(gstin, row.Irn!, code, remark, ct);
            }
            catch (HttpRequestException)
            {
                cancelled = IrpResult<IrnCancellation>.Unavailable("The IRP could not be reached.");
            }

            if (!cancelled.Ok && cancelled.ErrorCode != IrpErrorCodes.AlreadyCancelled)
            {
                return cancelled.ErrorCode == IrpErrorCodes.CancelWindowPassed
                    ? "The IRP says the 24 hours to cancel this IRN have passed. Raise a credit note instead."
                    : $"The IRP did not cancel the IRN, so the document was not voided: {cancelled.ErrorMessage}";
            }
        }

        // A Pending or Failed e-invoice was never registered, so there is
        // nothing at the IRP to cancel: it is simply marked with the void.
        row.Status = EInvoiceStatus.Cancelled;
        row.CancelReason = code;
        row.CancelRemark = remark;
        row.CancelledAt = now;
        row.NextAttemptAt = null;
        return null;
    }
}
