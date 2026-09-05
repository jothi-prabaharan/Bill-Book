using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace BillBook.Tests.Shared;

/// <summary>
/// Asks PostgreSQL what row-level security it is actually enforcing on a schema.
///
/// <b>Three separate things have to be true, and the suites were checking one.</b>
/// Every per-customer schema had a test asserting <c>pg_tables.rowsecurity</c>,
/// which says a table has RLS turned on. It does not say a policy exists —
/// a table with RLS on and no policy denies everything, which fails loudly, but
/// a table with RLS on and a policy that was dropped by a squashed migration
/// would too, and neither shows up as "not rowsecurity". And it does not say
/// <c>FORCE</c> is set.
///
/// <b>FORCE is the one that matters most and was checked nowhere.</b> Without
/// it, RLS does not apply to the table's owner. The application connects as the
/// role that owns these tables, so every policy in the product would be
/// inert — the query filter would be the only thing left, and the whole point of
/// having both is that neither is trusted alone. It is set correctly today;
/// nothing was asserting that it stays set.
///
/// <b>Read from the catalog, not from a list.</b> The existing Reporting check
/// named three tables explicitly, which is a list with exceptions on it — the
/// shape that has hidden every gap this project has found. This asks about every
/// table in the schema and takes the exemptions as an argument, so an exemption
/// is a line somebody wrote rather than a table nobody added.
/// </summary>
public static class RlsAudit
{
    public sealed record TableSecurity(
        string Table, bool Enabled, bool Forced, int Policies);

    /// <summary>Every ordinary table in the schema, with what RLS it carries.</summary>
    public static async Task<IReadOnlyList<TableSecurity>> ReadAsync(
        DbContext db, string schema)
    {
        List<TableSecurity> rows = [];

        await using NpgsqlConnection connection =
            new(db.Database.GetConnectionString());

        await connection.OpenAsync();

        // Raw SQL, which is one of the exceptions the project's rules name:
        // there is no LINQ over pg_class, and this is asking the database what
        // it is enforcing rather than querying the application's own data.
        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT c.relname,
                   c.relrowsecurity,
                   c.relforcerowsecurity,
                   (SELECT count(*) FROM pg_policies p
                     WHERE p.schemaname = n.nspname AND p.tablename = c.relname)
              FROM pg_class c
              JOIN pg_namespace n ON n.oid = c.relnamespace
             WHERE n.nspname = @schema
               AND c.relkind = 'r'
             ORDER BY c.relname
            """;
        command.Parameters.AddWithValue("schema", schema);

        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            rows.Add(new TableSecurity(
                reader.GetString(0),
                reader.GetBoolean(1),
                reader.GetBoolean(2),
                (int)reader.GetInt64(3)));
        }

        return rows;
    }

    /// <summary>
    /// The tables that are not fully protected, described so a failure says
    /// which of the three things is missing rather than only that something is.
    ///
    /// <paramref name="exempt"/> names tables with no tenant column to scope —
    /// global reference data sharing the schema. Those must be listed, because
    /// "this table has nothing to leak" is a claim about the table's columns
    /// that only a person can make.
    /// </summary>
    public static async Task<IReadOnlyList<string>> UnprotectedAsync(
        DbContext db, string schema, params string[] exempt)
    {
        var exemptions = exempt.ToHashSet(StringComparer.Ordinal);

        return
        [
            .. (await ReadAsync(db, schema))
                .Where(t => !exemptions.Contains(t.Table))
                .Where(t => !t.Enabled || !t.Forced || t.Policies == 0)
                .Select(t => $"{t.Table} ("
                    + string.Join(", ", Faults(t))
                    + ")"),
        ];
    }

    private static IEnumerable<string> Faults(TableSecurity table)
    {
        if (!table.Enabled)
        {
            yield return "RLS off";
        }

        if (!table.Forced)
        {
            // The application owns these tables, so without FORCE the policies
            // below never run for it.
            yield return "not FORCEd, so the owner bypasses every policy";
        }

        if (table.Policies == 0)
        {
            yield return "no policy";
        }
    }
}
