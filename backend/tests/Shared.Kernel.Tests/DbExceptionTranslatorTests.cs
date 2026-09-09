using Microsoft.EntityFrameworkCore;
using Npgsql;
using Shared.Kernel.Errors;
using Xunit;

namespace Shared.Kernel.Tests;

/// <summary>
/// Unwrapping is the whole job here. A database failure almost never arrives as
/// a PostgresException — it arrives inside a DbUpdateException, sometimes inside
/// that inside an AggregateException — and a translator that only looks one
/// level down reports every one of them as an unexplained 500.
/// </summary>
public class DbExceptionTranslatorTests
{
    private static PostgresException Postgres(
        string sqlState,
        string message = "duplicate key value violates unique constraint",
        string? constraint = null,
        string? table = null,
        string? detail = null) =>
        new(
            messageText: message,
            severity: "ERROR",
            invariantSeverity: "ERROR",
            sqlState: sqlState,
            detail: detail,
            constraintName: constraint,
            tableName: table);

    [Fact]
    public void A_bare_postgres_exception_is_translated()
    {
        TranslatedError result = DbExceptionTranslator.Translate(Postgres("23505"));

        Assert.Equal(ApiErrorCode.DuplicateRecord, result.Code);
        Assert.Equal(409, result.StatusCode);
        Assert.NotNull(result.Postgres);
    }

    [Fact]
    public void A_postgres_exception_wrapped_by_save_changes_is_translated()
    {
        // The shape every SaveChanges failure actually has.
        var exception = new DbUpdateException("An error occurred while saving.", Postgres("23503"));

        TranslatedError result = DbExceptionTranslator.Translate(exception);

        Assert.Equal(ApiErrorCode.ReferencedRecordMissing, result.Code);
        Assert.Equal(409, result.StatusCode);
    }

    [Fact]
    public void A_postgres_exception_two_levels_down_is_still_found()
    {
        var exception = new InvalidOperationException(
            "Posting failed.",
            new DbUpdateException("An error occurred while saving.", Postgres("23502")));

        TranslatedError result = DbExceptionTranslator.Translate(exception);

        Assert.Equal(ApiErrorCode.RequiredValueMissing, result.Code);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public void A_postgres_exception_inside_an_aggregate_is_still_found()
    {
        var exception = new AggregateException(
            new InvalidOperationException("unrelated"),
            new DbUpdateException("An error occurred while saving.", Postgres("40001")));

        TranslatedError result = DbExceptionTranslator.Translate(exception);

        Assert.Equal(ApiErrorCode.ConcurrentUpdate, result.Code);
        Assert.True(result.IsTransient);
    }

    [Fact]
    public void A_concurrency_failure_has_no_sqlstate_and_is_still_a_conflict()
    {
        // xmin disagreed. Postgres never raised an error, so there is nothing to
        // look up — the answer has to come from the exception type alone.
        TranslatedError result = DbExceptionTranslator.Translate(
            new DbUpdateConcurrencyException("The database operation was expected to affect 1 row."));

        Assert.Equal(ApiErrorCode.ConcurrencyConflict, result.Code);
        Assert.Equal(409, result.StatusCode);
        Assert.False(result.IsTransient);
        Assert.Null(result.Postgres);
    }

    [Fact]
    public void A_failure_that_is_not_the_databases_is_unexpected()
    {
        TranslatedError result = DbExceptionTranslator.Translate(
            new InvalidOperationException("A service was resolved out of scope."));

        Assert.Equal(ApiErrorCode.Unexpected, result.Code);
        Assert.Equal(500, result.StatusCode);
    }

    [Fact]
    public void The_description_carries_everything_the_developer_needs()
    {
        var exception = new DbUpdateException(
            "An error occurred while saving the entity changes.",
            Postgres(
                "23505",
                "duplicate key value violates unique constraint \"IX_Contacts_OrgId_Gstin\"",
                constraint: "IX_Contacts_OrgId_Gstin",
                table: "Contacts",
                detail: "Key (\"OrgId\", \"Gstin\")=(...) already exists."));

        ApiErrorDiagnostics described = DbExceptionTranslator.Describe(exception);

        Assert.Equal("23505", described.SqlState);
        Assert.Equal("IX_Contacts_OrgId_Gstin", described.ConstraintName);
        Assert.Equal("Contacts", described.TableName);
        Assert.Contains("already exists", described.Detail);
        Assert.NotNull(described.InnerExceptions);
        Assert.Contains(described.InnerExceptions!, i => i.Contains("PostgresException"));
    }
}
