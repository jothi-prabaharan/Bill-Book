using System.Net.Http.Json;

namespace Shared.Kernel.Approvals;

/// <summary>
/// Asks Master, which configures approval workflows and resolves their chains
/// (D-26, TK-99), for a document's chain and whether one user stands in for
/// another. Used by every service that stores approval steps (TK-100 on).
/// </summary>
public interface IApprovalChainClient
{
    /// <summary>The chain for a request, or null when Master cannot be asked.</summary>
    Task<ResolveChainResponse?> ResolveAsync(ResolveChainRequest request, CancellationToken ct);

    /// <summary>Whether the actor is standing in for the approver today. False when Master cannot be asked.</summary>
    Task<bool> IsDelegateAsync(DelegateCheckRequest request, CancellationToken ct);
}

/// <summary>Master's <c>internal/approval-chains</c>, called with the shared internal key.</summary>
public sealed class HttpApprovalChainClient : IApprovalChainClient
{
    private readonly HttpClient _http;

    public HttpApprovalChainClient(HttpClient http) => _http = http;

    public async Task<ResolveChainResponse?> ResolveAsync(ResolveChainRequest request, CancellationToken ct)
    {
        try
        {
            HttpResponseMessage response = await _http.PostAsJsonAsync("internal/approval-chains/resolve", request, ct);
            return response.IsSuccessStatusCode
                ? await response.Content.ReadFromJsonAsync<ResolveChainResponse>(cancellationToken: ct)
                : null;
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    public async Task<bool> IsDelegateAsync(DelegateCheckRequest request, CancellationToken ct)
    {
        try
        {
            HttpResponseMessage response = await _http.PostAsJsonAsync("internal/approval-chains/delegate-check", request, ct);
            return response.IsSuccessStatusCode
                && (await response.Content.ReadFromJsonAsync<DelegateCheckResponse>(cancellationToken: ct))?.IsDelegate == true;
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }

    private sealed class DelegateCheckResponse
    {
        public bool IsDelegate { get; set; }
    }
}

/// <summary>
/// The document side of an approval (TK-100): building a round's steps from a
/// resolved chain, and keeping the header's summary in step with them. Pure,
/// so each service's approval service stays thin and the rules stay one.
/// </summary>
public static class DocumentApproval
{
    /// <summary>A round's steps from Master's answer: skipped levels marked, the rest waiting.</summary>
    public static List<T> Steps<T>(ResolveChainResponse resolved, long requestId, ApprovalRequestKind kind, Func<T> create)
        where T : ApprovalStepBase =>
        [.. resolved.Steps.OrderBy(s => s.Sequence).Select(s =>
        {
            T step = create();
            step.RequestId = requestId;
            step.RequestKind = kind;
            step.Sequence = s.Sequence;
            step.Label = s.Label.Length > 50 ? s.Label[..50] : s.Label;
            step.ApproverUserId = s.ApproverUserId;
            step.ApproverEmployeeId = s.ApproverEmployeeId;
            step.RoleId = s.RoleId;
            step.IsCommentRequired = s.IsCommentRequired;
            step.StepStatus = s.IsSkipped ? ApprovalStepStatus.Skipped : ApprovalStepStatus.Waiting;
            return step;
        })];

    /// <summary>Copies where the chain stands onto the document: its status and who it waits on.</summary>
    public static void Summarize<T>(Documents.DocumentHeaderBase document, ApprovalStatus? status, IEnumerable<T> steps)
        where T : ApprovalStepBase
    {
        T? current = status == ApprovalStatus.InApproval ? ApprovalChain.Current(steps) : null;

        document.ApprovalStatus = status;
        document.CurrentStepLabel = current?.Label;
        document.CurrentApproverUserId = current?.ApproverUserId;
        document.CurrentApproverRoleId = current?.RoleId;
    }

    /// <summary>The refusal a lifecycle action gives while a document is under approval, or null.</summary>
    public static string? Blocks(Documents.DocumentHeaderBase document) => document.ApprovalStatus switch
    {
        ApprovalStatus.InApproval => "This document is waiting for approval.",
        ApprovalStatus.Rejected => "This document was rejected. Edit it and submit it for approval again.",
        _ => null,
    };
}
