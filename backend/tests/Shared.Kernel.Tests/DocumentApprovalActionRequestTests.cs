using System.Text.Json;
using Shared.Kernel.Approvals;
using Xunit;

namespace Shared.Kernel.Tests;

/// <summary>
/// The approval panel and the inbox send the action by name (TK-103). A
/// service with no global string-enum converter must still read it.
/// </summary>
public sealed class DocumentApprovalActionRequestTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData("\"Approve\"", ApprovalAction.Approve)]
    [InlineData("\"Reject\"", ApprovalAction.Reject)]
    [InlineData("\"SendBack\"", ApprovalAction.SendBack)]
    public void The_action_is_read_by_name(string action, ApprovalAction expected)
    {
        DocumentApprovalActionRequest? request = JsonSerializer.Deserialize<DocumentApprovalActionRequest>(
            $$"""{ "action": {{action}}, "comments": "ok" }""", Web);

        Assert.Equal(expected, request!.Action);
        Assert.Equal("ok", request.Comments);
    }

    [Fact]
    public void The_action_is_still_read_by_number() =>
        Assert.Equal(
            ApprovalAction.Reject,
            JsonSerializer.Deserialize<DocumentApprovalActionRequest>(
                $$"""{ "action": {{(int)ApprovalAction.Reject}} }""", Web)!.Action);
}
