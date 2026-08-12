using Shared.Kernel.Documents;
using Shared.Kernel.Internal;
using Xunit;

namespace Shared.Kernel.Tests;

/// <summary>
/// The lifecycle table, and the permission action it implies.
///
/// These are the cases nobody thinks about until they happen — whether a
/// <c>ReadyToPost</c> document is still editable, whether a void needs a reason,
/// whether an abandoned draft is deleted. Written down here so all nine
/// documents answer them the same way.
/// </summary>
public class DocumentLifecycleTests
{
    // ---- Editing -----------------------------------------------------------

    [Theory]
    [InlineData(DocumentStatus.Draft)]
    [InlineData(DocumentStatus.ReadyToPost)]
    public void Draft_and_ready_to_post_are_editable(DocumentStatus status)
    {
        // ReadyToPost is editable deliberately: the reviewer is usually the one
        // who spots the error, and sending it backwards to fix a typo is how a
        // review step gets skipped.
        Assert.True(DocumentLifecycle.CanEdit(status).IsAllowed);
    }

    [Fact]
    public void A_posted_document_refuses_an_edit()
    {
        DocumentTransition result = DocumentLifecycle.CanEdit(DocumentStatus.Posted);

        Assert.False(result.IsAllowed);
        Assert.Equal(DocumentTransitionOutcome.AlreadyPosted, result.Outcome);
        Assert.False(string.IsNullOrWhiteSpace(result.Detail));
    }

    [Fact]
    public void A_voided_document_refuses_an_edit()
    {
        Assert.Equal(
            DocumentTransitionOutcome.AlreadyVoid,
            DocumentLifecycle.CanEdit(DocumentStatus.Void).Outcome);
    }

    // ---- Approving ---------------------------------------------------------

    [Fact]
    public void A_draft_with_lines_can_be_approved()
    {
        Assert.True(DocumentLifecycle.CanApprove(DocumentStatus.Draft, lineCount: 2).IsAllowed);
    }

    [Fact]
    public void An_empty_draft_cannot_be_approved()
    {
        Assert.Equal(
            DocumentTransitionOutcome.NoLines,
            DocumentLifecycle.CanApprove(DocumentStatus.Draft, lineCount: 0).Outcome);
    }

    [Theory]
    [InlineData(DocumentStatus.ReadyToPost, DocumentTransitionOutcome.WrongStatus)]
    [InlineData(DocumentStatus.Posted, DocumentTransitionOutcome.AlreadyPosted)]
    [InlineData(DocumentStatus.Void, DocumentTransitionOutcome.AlreadyVoid)]
    public void Only_a_draft_can_be_approved(
        DocumentStatus status, DocumentTransitionOutcome expected)
    {
        Assert.Equal(expected, DocumentLifecycle.CanApprove(status, lineCount: 2).Outcome);
    }

    // ---- Posting -----------------------------------------------------------

    [Theory]
    [InlineData(DocumentStatus.Draft)]
    [InlineData(DocumentStatus.ReadyToPost)]
    public void A_document_posts_from_draft_or_from_ready(DocumentStatus status)
    {
        // A branch that does not want an approval step posts straight from
        // Draft. Nothing forces a document through ReadyToPost.
        Assert.True(DocumentLifecycle.CanPost(status, lineCount: 1).IsAllowed);
    }

    [Theory]
    [InlineData(DocumentStatus.Draft)]
    [InlineData(DocumentStatus.ReadyToPost)]
    public void A_document_with_no_lines_does_not_post(DocumentStatus status)
    {
        Assert.Equal(
            DocumentTransitionOutcome.NoLines,
            DocumentLifecycle.CanPost(status, lineCount: 0).Outcome);
    }

    [Fact]
    public void A_posted_document_does_not_post_twice()
    {
        Assert.Equal(
            DocumentTransitionOutcome.AlreadyPosted,
            DocumentLifecycle.CanPost(DocumentStatus.Posted, lineCount: 1).Outcome);
    }

    [Fact]
    public void A_voided_document_does_not_post()
    {
        Assert.Equal(
            DocumentTransitionOutcome.AlreadyVoid,
            DocumentLifecycle.CanPost(DocumentStatus.Void, lineCount: 1).Outcome);
    }

    // ---- Voiding -----------------------------------------------------------

    [Theory]
    [InlineData(DocumentStatus.Draft)]
    [InlineData(DocumentStatus.ReadyToPost)]
    [InlineData(DocumentStatus.Posted)]
    public void Anything_not_already_void_can_be_voided_with_a_reason(DocumentStatus status)
    {
        Assert.True(
            DocumentLifecycle.CanVoid(status, hasDownstream: false, "Keyed twice").IsAllowed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_void_without_a_reason_is_refused(string? reason)
    {
        // The reason is the whole difference between a gap in the sequence that
        // is explained and a gap that is a hole.
        Assert.Equal(
            DocumentTransitionOutcome.ReasonRequired,
            DocumentLifecycle.CanVoid(DocumentStatus.Posted, hasDownstream: false, reason).Outcome);
    }

    [Fact]
    public void A_document_with_something_downstream_is_not_voided()
    {
        Assert.Equal(
            DocumentTransitionOutcome.HasDownstream,
            DocumentLifecycle.CanVoid(
                DocumentStatus.Posted, hasDownstream: true, "Customer cancelled").Outcome);
    }

    [Fact]
    public void The_missing_reason_is_reported_before_the_downstream_refusal()
    {
        // Being told to give a reason and then refused anyway is worse than
        // being refused first.
        Assert.Equal(
            DocumentTransitionOutcome.ReasonRequired,
            DocumentLifecycle.CanVoid(DocumentStatus.Posted, hasDownstream: true, null).Outcome);
    }

    [Fact]
    public void A_voided_document_is_terminal()
    {
        Assert.Equal(
            DocumentTransitionOutcome.AlreadyVoid,
            DocumentLifecycle.CanVoid(DocumentStatus.Void, hasDownstream: false, "Again").Outcome);
    }

    // ---- Deleting ----------------------------------------------------------

    [Fact]
    public void A_document_is_never_deleted()
    {
        DocumentTransition result = DocumentLifecycle.CanDelete();

        Assert.False(result.IsAllowed);
        Assert.Equal(DocumentTransitionOutcome.NeverDeleted, result.Outcome);

        // The refusal has to say why, because the caller reaching for a delete
        // is reaching for something reasonable-sounding.
        Assert.Contains("void", result.Detail!, StringComparison.OrdinalIgnoreCase);
    }

    // ---- The permission action ---------------------------------------------

    [Theory]
    [InlineData("GET", "view")]
    [InlineData("HEAD", "view")]
    [InlineData("OPTIONS", "view")]
    [InlineData("DELETE", "delete")]
    [InlineData("POST", "edit")]
    [InlineData("PUT", "edit")]
    [InlineData("PATCH", "edit")]
    public void The_method_decides_the_action_when_a_route_declares_none(
        string method, string expected)
    {
        Assert.Equal(expected, RequireModulePermissionAttribute.ActionFor(method, null));
    }

    [Theory]
    [InlineData("void")]
    [InlineData("approve")]
    [InlineData("print")]
    public void A_declared_action_replaces_the_one_the_method_implies(string declared)
    {
        // The point of T0.4: a POST to /void needs sales.void and NOT sales.edit.
        // Anyone holding edit could otherwise withdraw an invoice, which is the
        // separation the seeded permission exists to express.
        Assert.Equal("void", RequireModulePermissionAttribute.ActionFor("POST", "void"));
        Assert.Equal(declared, RequireModulePermissionAttribute.ActionFor("POST", declared));
    }

    [Theory]
    [InlineData("VOID", "void")]
    [InlineData("  Approve  ", "approve")]
    public void A_declared_action_is_normalised(string declared, string expected)
    {
        // Permission claims are compared case-insensitively anyway, but the
        // string is also what a log line shows, and two spellings of one
        // authority read as two authorities.
        Assert.Equal(expected, RequireModulePermissionAttribute.ActionFor("POST", declared));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_blank_declared_action_falls_back_to_the_method(string declared)
    {
        Assert.Equal("edit", RequireModulePermissionAttribute.ActionFor("POST", declared));
    }
}
