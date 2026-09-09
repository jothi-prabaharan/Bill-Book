using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Shared.Kernel.Errors;
using Xunit;

namespace Shared.Kernel.Tests;

/// <summary>
/// The environment split, asserted from the wire rather than from the code.
///
/// This is the pair of tests the requirement reduces to: Development answers
/// with the exact database error, and every other environment answers with the
/// curated sentence and nothing that could be used to learn the schema. Asserted
/// against the serialized body, because "the property is null" and "the property
/// is absent from the JSON" are different promises and only the second one is
/// what a caller sees.
/// </summary>
public class GlobalExceptionHandlerTests
{
    private const string ConstraintName = "IX_Contacts_OrgId_Gstin";

    private static Exception RealisticFailure() =>
        new DbUpdateException(
            "An error occurred while saving the entity changes.",
            new PostgresException(
                messageText: $"duplicate key value violates unique constraint \"{ConstraintName}\"",
                severity: "ERROR",
                invariantSeverity: "ERROR",
                sqlState: "23505",
                detail: "Key (\"OrgId\", \"Gstin\")=(3f2b, 33AAAAA0000A1Z5) already exists.",
                constraintName: ConstraintName,
                tableName: "Contacts",
                schemaName: "con"));

    private static async Task<(int Status, JsonElement Body)> HandleAsync(string environmentName)
    {
        var handler = new GlobalExceptionHandler(
            new StubEnvironment(environmentName),
            NullLogger<GlobalExceptionHandler>.Instance,
            // No IErrorLogStore: this is the Gateway-shaped case, and it keeps
            // the test to the one thing it is about.
            new EmptyServiceProvider());

        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = "/api/contacts";
        var body = new MemoryStream();
        context.Response.Body = body;

        bool handled = await handler.TryHandleAsync(context, RealisticFailure(), CancellationToken.None);
        Assert.True(handled);

        body.Position = 0;
        using JsonDocument document = await JsonDocument.ParseAsync(body);

        return (context.Response.StatusCode, document.RootElement.Clone());
    }

    [Fact]
    public async Task Development_returns_the_exact_database_error()
    {
        (int status, JsonElement body) = await HandleAsync(Environments.Development);

        Assert.Equal(409, status);

        JsonElement diagnostics = body.GetProperty("diagnostics");

        Assert.Equal("23505", diagnostics.GetProperty("sqlState").GetString());
        Assert.Equal(ConstraintName, diagnostics.GetProperty("constraintName").GetString());
        Assert.Equal("Contacts", diagnostics.GetProperty("tableName").GetString());
        Assert.Equal("con", diagnostics.GetProperty("schemaName").GetString());
        Assert.Contains("already exists", diagnostics.GetProperty("detail").GetString());
        Assert.Contains(
            "duplicate key value violates unique constraint",
            diagnostics.GetProperty("message").GetString());
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    [InlineData("QA")]
    public async Task Every_other_environment_returns_the_curated_message_only(string environmentName)
    {
        (int status, JsonElement body) = await HandleAsync(environmentName);

        Assert.Equal(409, status);

        // The whole response, as a caller would see it. Nothing in it may name
        // the constraint, the table, the schema, or the database's own words.
        string wire = body.GetRawText();

        Assert.DoesNotContain(ConstraintName, wire, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Contacts", wire, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("duplicate key", wire, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("23505", wire, StringComparison.Ordinal);
        Assert.DoesNotContain("33AAAAA0000A1Z5", wire, StringComparison.Ordinal);

        Assert.Equal(JsonValueKind.Null, body.GetProperty("diagnostics").ValueKind);
        Assert.Equal(
            "A record with these details already exists.",
            body.GetProperty("message").GetString());
    }

    [Fact]
    public async Task The_code_is_the_same_in_both_environments_so_a_client_can_branch_on_it()
    {
        (_, JsonElement development) = await HandleAsync(Environments.Development);
        (_, JsonElement production) = await HandleAsync(Environments.Production);

        Assert.Equal(
            nameof(ApiErrorCode.DuplicateRecord),
            development.GetProperty("code").GetString());

        Assert.Equal(
            development.GetProperty("code").GetString(),
            production.GetProperty("code").GetString());

        // And so is the message. A user-facing sentence that changed between
        // environments would be a sentence nobody had read before release.
        Assert.Equal(
            development.GetProperty("message").GetString(),
            production.GetProperty("message").GetString());
    }

    [Fact]
    public async Task A_caller_that_hung_up_is_not_answered_and_not_recorded()
    {
        var handler = new GlobalExceptionHandler(
            new StubEnvironment(Environments.Production),
            NullLogger<GlobalExceptionHandler>.Instance,
            new EmptyServiceProvider());

        var context = new DefaultHttpContext();
        var aborted = new CancellationTokenSource();
        await aborted.CancelAsync();
        context.RequestAborted = aborted.Token;

        var body = new MemoryStream();
        context.Response.Body = body;

        bool handled = await handler.TryHandleAsync(
            context, new OperationCanceledException(), CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(0, body.Length);
    }

    private sealed class StubEnvironment : IHostEnvironment
    {
        public StubEnvironment(string environmentName) => EnvironmentName = environmentName;

        public string EnvironmentName { get; set; }

        public string ApplicationName { get; set; } = "Test.Api";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } =
            new NullFileProvider();
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
