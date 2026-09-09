using System.Net;
using Shared.Kernel.Errors;
using Xunit;

namespace Shared.Kernel.Tests;

/// <summary>
/// The SQLSTATE audit, asserted rather than described.
///
/// Two properties matter more than any individual mapping. Every catalogued
/// state must produce a status a client can act on — a duplicate is not a 500 —
/// and no curated message may carry anything about the schema, because that
/// message is the one thing a production caller is shown.
/// </summary>
public class SqlErrorCatalogTests
{
    [Theory]
    [InlineData("23505", ApiErrorCode.DuplicateRecord, 409)]
    [InlineData("23503", ApiErrorCode.ReferencedRecordMissing, 409)]
    [InlineData("23502", ApiErrorCode.RequiredValueMissing, 400)]
    [InlineData("23514", ApiErrorCode.ValueRejected, 400)]
    [InlineData("22001", ApiErrorCode.ValueTooLong, 400)]
    [InlineData("22003", ApiErrorCode.ValueOutOfRange, 400)]
    [InlineData("22P02", ApiErrorCode.InvalidFormat, 400)]
    [InlineData("40001", ApiErrorCode.ConcurrentUpdate, 409)]
    [InlineData("40P01", ApiErrorCode.ConcurrentUpdate, 409)]
    [InlineData("42501", ApiErrorCode.PermissionDenied, 403)]
    [InlineData("53300", ApiErrorCode.ServiceBusy, 503)]
    [InlineData("57014", ApiErrorCode.RequestCancelled, 504)]
    [InlineData("P0001", ApiErrorCode.RuleViolation, 409)]
    public void A_catalogued_state_maps_to_its_code_and_status(
        string sqlState, ApiErrorCode expected, int status)
    {
        SqlErrorRule rule = SqlErrorCatalog.ForSqlState(sqlState);

        Assert.Equal(expected, rule.Code);
        Assert.Equal(status, rule.StatusCode);
    }

    [Fact]
    public void An_uncatalogued_state_falls_back_to_its_class()
    {
        // 23001 is a real integrity violation that nothing in this product
        // raises today. It must still be reported as a conflict rather than as
        // a crash, which is the whole reason the class table exists.
        SqlErrorRule rule = SqlErrorCatalog.ForSqlState("23001");

        Assert.Equal(ApiErrorCode.ValueRejected, rule.Code);
        Assert.Equal((int)HttpStatusCode.Conflict, rule.StatusCode);
    }

    [Fact]
    public void A_state_in_no_known_class_is_unexpected()
    {
        SqlErrorRule rule = SqlErrorCatalog.ForSqlState("XX000");

        Assert.Equal(ApiErrorCode.Unexpected, rule.Code);
        Assert.Equal((int)HttpStatusCode.InternalServerError, rule.StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_missing_state_is_unexpected(string? sqlState)
    {
        Assert.Equal(ApiErrorCode.Unexpected, SqlErrorCatalog.ForSqlState(sqlState).Code);
    }

    [Fact]
    public void Only_the_states_that_can_be_retried_are_marked_transient()
    {
        // A duplicate key will never succeed on retry, and telling a client it
        // might is how a retry loop becomes an infinite one.
        Assert.False(SqlErrorCatalog.ForSqlState("23505").IsTransient);
        Assert.False(SqlErrorCatalog.ForSqlState("23502").IsTransient);

        Assert.True(SqlErrorCatalog.ForSqlState("40001").IsTransient);
        Assert.True(SqlErrorCatalog.ForSqlState("40P01").IsTransient);
        Assert.True(SqlErrorCatalog.ForSqlState("53300").IsTransient);
    }

    [Fact]
    public void No_curated_message_leaks_the_schema_or_the_database()
    {
        // Every one of these appears in a real Postgres error message for one of
        // the states catalogued below. None may reach a production caller: the
        // message is the only part of the response a user is shown, and the
        // exact text is available to whoever holds the error reference.
        string[] forbidden =
        [
            "acc.", "sal.", "pur.", "inv.", "con.", "cus.", "rpt.", "mst.",
            "constraint", "column", "relation", "duplicate key", "SQLSTATE",
            "pg_", "postgres", "IX_", "FK_", "PK_", "null value",
        ];

        var messages = SqlErrorCatalog.CataloguedStates
            .Select(SqlErrorCatalog.ForSqlState)
            .Concat(SqlErrorCatalog.CataloguedClasses.Select(c => SqlErrorCatalog.ForSqlState(c + "999")))
            .Append(SqlErrorCatalog.Unexpected)
            .Select(r => r.Message)
            .ToList();

        Assert.NotEmpty(messages);

        foreach (string message in messages)
        {
            foreach (string term in forbidden)
            {
                Assert.False(
                    message.Contains(term, StringComparison.OrdinalIgnoreCase),
                    $"The message \"{message}\" leaks \"{term}\".");
            }
        }
    }

    [Fact]
    public void Every_curated_message_is_a_sentence_somebody_could_read()
    {
        foreach (string state in SqlErrorCatalog.CataloguedStates)
        {
            string message = SqlErrorCatalog.ForSqlState(state).Message;

            Assert.False(string.IsNullOrWhiteSpace(message), $"{state} has no message.");
            Assert.EndsWith(".", message, StringComparison.Ordinal);
        }
    }
}
