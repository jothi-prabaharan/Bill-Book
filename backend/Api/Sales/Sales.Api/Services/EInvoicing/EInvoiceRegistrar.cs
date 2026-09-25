using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Sales.Entity.Enums;
using Sales.Entity.Models;
using Sales.Entity.TableEntities;
using Sales.Repository;
using Shared.Kernel.Errors;

namespace Sales.Api.Services.EInvoicing;

/// <summary>An e-invoice that could not be registered, for the error log's task list (TK-92).</summary>
public sealed class EInvoiceRefusedException(string code, string message)
    : Exception($"{code}: {message}")
{
    public string Code { get; } = code;
}

/// <summary>
/// Registers one e-invoice at the IRP (TK-92): the inline attempt right after
/// a posting commits, the retry worker's later ones, and a person's manual
/// retry all come here.
///
/// <list type="bullet">
/// <item><b>Claimed before it is sent.</b> A guarded update pushes
/// <c>NextAttemptAt</c> out while the attempt runs, and its row count is the
/// answer, so the inline attempt and the worker never send the same row at
/// once.</item>
/// <item><b>A duplicate is not a failure.</b> The IRP may have registered a
/// request whose answer was lost; "duplicate IRN" is answered by fetching the
/// IRN by document and storing it (design, decision 6).</item>
/// <item><b>Transient problems wait, permanent ones stop.</b> The portal being
/// unreachable backs off and tries again; a refusal a person has to fix goes to
/// Failed and to the error log with <c>FollowUpStatus = Open</c>.</item>
/// </list>
/// </summary>
public sealed class EInvoiceRegistrar
{
    /// <summary>How long a claimed row is held while its attempt runs.</summary>
    public static readonly TimeSpan ClaimLease = TimeSpan.FromMinutes(2);

    private static readonly TimeSpan[] Backoff =
    [
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(15),
        TimeSpan.FromHours(1),
        TimeSpan.FromHours(3),
        TimeSpan.FromHours(6),
        TimeSpan.FromHours(12),
    ];

    private readonly SalesDbContext _db;
    private readonly EInvoiceDocumentBuilder _builder;
    private readonly IEInvoiceGateway _gateway;
    private readonly IWorkerErrorAuditor _auditor;
    private readonly TimeProvider _clock;

    public EInvoiceRegistrar(
        SalesDbContext db,
        EInvoiceDocumentBuilder builder,
        IEInvoiceGateway gateway,
        IWorkerErrorAuditor auditor,
        TimeProvider clock)
    {
        _db = db;
        _builder = builder;
        _gateway = gateway;
        _auditor = auditor;
        _clock = clock;
    }

    /// <summary>The wait before the next attempt, after <paramref name="attempts"/> of them.</summary>
    public static TimeSpan BackoffAfter(int attempts) => Backoff[Math.Clamp(attempts - 1, 0, Backoff.Length - 1)];

    /// <summary>
    /// One attempt at one row. Returns its state, filled into
    /// <paramref name="view"/> when one is given, or null when the row is gone
    /// (the document turned out to need no IRN).
    /// </summary>
    public async Task<EInvoiceStateView?> RegisterAsync(long eInvoiceId, EInvoiceStateView? view, CancellationToken ct)
    {
        DateTimeOffset now = _clock.GetUtcNow();
        DateTimeOffset lease = now + ClaimLease;

        int claimed = await _db.EInvoices
            .Where(e => e.EInvoiceId == eInvoiceId
                && e.Status == EInvoiceStatus.Pending
                && (e.NextAttemptAt == null || e.NextAttemptAt <= now))
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.NextAttemptAt, lease), ct);

        EInvoice? row = await LoadAsync(eInvoiceId, ct);
        if (row is null)
        {
            return null;
        }

        if (claimed == 0)
        {
            // Already registered, failed, cancelled, or being tried by someone else.
            return Fill(view, row);
        }

        EInvoiceBuild build = row.SourceType switch
        {
            EInvoiceSource.Invoice => await _builder.BuildForInvoiceAsync(row.SourceId, ct),
            EInvoiceSource.CreditNote => await _builder.BuildForCreditNoteAsync(row.SourceId, ct),
            _ => EInvoiceBuild.NotApplicable,
        };

        if (!build.Applies)
        {
            // Written at posting when the buyer could not be looked up; the
            // document needs no IRN after all.
            _db.EInvoices.Remove(row);
            await _db.SaveChangesAsync(ct);
            return null;
        }

        if (build.Transient)
        {
            Wait(row, build.Problems[0].Code, build.Problems[0].Message, now);
        }
        else if (build.Problems.Count > 0)
        {
            await FailAsync(row, "VALIDATION", string.Join(" ", build.Problems.Select(p => p.Message)), ct);
        }
        else
        {
            await SendAsync(row, build.Document!, now, ct);
        }

        await _db.SaveChangesAsync(ct);
        return Fill(view, row);
    }

    private async Task SendAsync(EInvoice row, Inv01Document document, DateTimeOffset now, CancellationToken ct)
    {
        string gstin = document.SellerDtls.Gstin;
        row.Attempts++;

        IrpResult<IrnDetails> result = await CallAsync(() => _gateway.GenerateIrnAsync(gstin, document, ct));

        if (!result.Ok && result.ErrorCode == IrpErrorCodes.DuplicateIrn)
        {
            DateOnly date = DateOnly.ParseExact(document.DocDtls.Dt, "dd/MM/yyyy", CultureInfo.InvariantCulture);
            IrpResult<IrnDetails> existing = await CallAsync(() =>
                _gateway.GetIrnByDocumentAsync(gstin, document.DocDtls.Typ, document.DocDtls.No, date, ct));
            result = existing.Ok ? existing : result;
        }

        if (result.Value is IrnDetails irn)
        {
            row.Status = EInvoiceStatus.Registered;
            row.Irn = irn.Irn;
            row.AckNo = irn.AckNo;
            row.AckDate = irn.AckDate;
            row.SignedInvoice = irn.SignedInvoice;
            row.SignedQrCode = irn.SignedQrCode;
            row.LastErrorCode = null;
            row.LastErrorMessage = null;
            row.NextAttemptAt = null;
        }
        else if (result.Transient)
        {
            Wait(row, result.ErrorCode ?? IrpErrorCodes.Unavailable, result.ErrorMessage ?? "The IRP could not be reached.", now);
        }
        else
        {
            await FailAsync(row, result.ErrorCode ?? "IRP", result.ErrorMessage ?? "The IRP refused the document.", ct);
        }
    }

    /// <summary>A portal that cannot be reached is transient, whichever way it failed to answer.</summary>
    private static async Task<IrpResult<T>> CallAsync<T>(Func<Task<IrpResult<T>>> call)
        where T : class
    {
        try
        {
            return await call();
        }
        catch (HttpRequestException ex)
        {
            return IrpResult<T>.Unavailable(ex.Message);
        }
        catch (TaskCanceledException ex) when (!ex.CancellationToken.IsCancellationRequested)
        {
            return IrpResult<T>.Unavailable("The IRP did not answer in time.");
        }
    }

    private void Wait(EInvoice row, string code, string message, DateTimeOffset now)
    {
        row.Status = EInvoiceStatus.Pending;
        row.LastErrorCode = Truncate(code, 20);
        row.LastErrorMessage = Truncate(message, 1000);
        row.NextAttemptAt = now + BackoffAfter(Math.Max(row.Attempts, 1));
    }

    private async Task FailAsync(EInvoice row, string code, string message, CancellationToken ct)
    {
        row.Status = EInvoiceStatus.Failed;
        row.LastErrorCode = Truncate(code, 20);
        row.LastErrorMessage = Truncate(message, 1000);
        row.NextAttemptAt = null;

        // The task list: a refused e-invoice is a tax invoice that is not yet
        // valid, and somebody has to fix and retry it.
        await _auditor.AuditAsync(
            "EInvoice",
            $"EInvoice:{row.EInvoiceId} {row.SourceType}:{row.SourceId}",
            new EInvoiceRefusedException(row.LastErrorCode, row.LastErrorMessage),
            ct);
    }

    private async Task<EInvoice?> LoadAsync(long eInvoiceId, CancellationToken ct)
    {
        // The row may already be tracked, from the posting that wrote it, and a
        // query would hand back that instance unchanged, missing the claim.
        EInvoice? tracked = _db.EInvoices.Local.FirstOrDefault(e => e.EInvoiceId == eInvoiceId);
        if (tracked is not null)
        {
            await _db.Entry(tracked).ReloadAsync(ct);
            return _db.Entry(tracked).State == EntityState.Detached ? null : tracked;
        }

        return await _db.EInvoices.FirstOrDefaultAsync(e => e.EInvoiceId == eInvoiceId, ct);
    }

    /// <summary>The row as the screens see it.</summary>
    public static EInvoiceStateView Fill(EInvoiceStateView? view, EInvoice row)
    {
        view ??= new EInvoiceStateView();
        view.EInvoiceId = row.EInvoiceId;
        view.Status = row.Status;
        view.Irn = row.Irn;
        view.AckNo = row.AckNo;
        view.AckDate = row.AckDate;
        view.Attempts = row.Attempts;
        view.Message = row.Status is EInvoiceStatus.Registered or EInvoiceStatus.Cancelled ? null : row.LastErrorMessage;
        view.NextAttemptAt = row.Status == EInvoiceStatus.Pending ? row.NextAttemptAt : null;
        return view;
    }

    private static string Truncate(string value, int length) => value.Length <= length ? value : value[..length];
}
