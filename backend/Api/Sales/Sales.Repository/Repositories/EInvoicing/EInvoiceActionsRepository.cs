using Sales.Repository.Clients;
using Microsoft.EntityFrameworkCore;
using Sales.Entity.Enums;
using Sales.Entity.Models;
using Sales.Entity.TableEntities;
using Sales.Repository;
using Shared.Kernel.Persistence;

namespace Sales.Repository.Repositories;

/// <summary>What a person can see and do about one document's e-invoice (TK-92).</summary>
public sealed class EInvoiceActionsRepository
{
    private readonly SalesDbContext _db;
    private readonly IAfterCommit _afterCommit;
    private readonly EInvoiceRegistrarRepository _registrar;

    public EInvoiceActionsRepository(SalesDbContext db, IAfterCommit afterCommit, EInvoiceRegistrarRepository registrar)
    {
        _db = db;
        _afterCommit = afterCommit;
        _registrar = registrar;
    }

    /// <summary>The document's e-invoice, or null when it has none (it needs no IRN, or is in another branch).</summary>
    public async Task<EInvoiceStateView?> GetAsync(EInvoiceSource source, long sourceId, CancellationToken ct)
    {
        EInvoice? row = await _db.EInvoices.AsNoTracking()
            .FirstOrDefaultAsync(e => e.SourceType == source && e.SourceId == sourceId, ct);
        return row is null ? null : EInvoiceRegistrarRepository.Fill(null, row);
    }

    /// <summary>
    /// Tries again now: a Failed e-invoice, after the document or the branch was
    /// fixed, or a Pending one waiting on its backoff. The attempt runs once the
    /// request commits, and the state returned is filled in with its answer.
    /// Null when the document has no e-invoice. A registered or cancelled one is
    /// returned as it is.
    /// </summary>
    public async Task<EInvoiceStateView?> RetryAsync(EInvoiceSource source, long sourceId, CancellationToken ct)
    {
        EInvoice? row = await _db.EInvoices.FirstOrDefaultAsync(e => e.SourceType == source && e.SourceId == sourceId, ct);
        if (row is null)
        {
            return null;
        }

        if (row.Status is EInvoiceStatus.Failed or EInvoiceStatus.Pending)
        {
            row.Status = EInvoiceStatus.Pending;
            row.NextAttemptAt = null;
            await _db.SaveChangesAsync(ct);

            EInvoiceStateView view = EInvoiceRegistrarRepository.Fill(null, row);
            long id = row.EInvoiceId;
            _afterCommit.Enqueue(token => _registrar.RegisterAsync(id, view, token));
            return view;
        }

        return EInvoiceRegistrarRepository.Fill(null, row);
    }
}
