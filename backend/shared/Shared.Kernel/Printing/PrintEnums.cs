namespace Shared.Kernel.Printing;

/// <summary>
/// What a placeholder resolves to, which decides how its value is formatted on
/// the way out — never how it is stored.
/// </summary>
public enum PlaceholderType
{
    Text = 0,
    Number = 1,
    Amount = 2,
    Date = 3,
    Image = 4,
    RichText = 5,
}

/// <summary>
/// Whether a placeholder yields one value per document or one per row of a
/// collection.
///
/// <b>Declared data, never inferred from the tag.</b> Dot notation is not a
/// signal: <c>Item.ItemName</c> is a List because Item is a repeating
/// collection, while <c>Organization.Name</c> is Single. Guessing from the
/// presence of a dot was tried and was wrong.
/// </summary>
public enum PlaceholderKind
{
    Single = 0,
    List = 1,
}
