namespace Banking.Api.Services;

/// <summary>
/// A statement file reduced to rows of strings, before anything knows what the
/// columns mean.
///
/// <b>Parsing the file and understanding it are two jobs, and keeping them apart
/// is what makes this tractable.</b> CSV and Excel differ entirely in how bytes
/// become cells and not at all in what the cells then mean, so the format
/// readers stop here and one mapper takes over. It also makes the mapper
/// testable without a file at all.
/// </summary>
/// <param name="Header">
/// The header row's cells, when the profile says there is one. Empty otherwise,
/// and the mapping is then by position.
/// </param>
public sealed record StatementGrid(IReadOnlyList<string> Header, IReadOnlyList<string[]> Rows);

/// <summary>What went wrong reading a file, in words a person can act on.</summary>
public sealed class StatementReadException : Exception
{
    public StatementReadException(string message)
        : base(message)
    {
    }

    public StatementReadException(string message, Exception inner)
        : base(message, inner)
    {
    }
}
