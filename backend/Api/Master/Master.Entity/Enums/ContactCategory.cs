namespace Master.Entity.Enums;

/// <summary>
/// A business or a person. Drives which fields are required — a business needs
/// contact people and a legal name; a walk-in customer needs neither.
/// </summary>
public enum ContactCategory
{
    Business = 0,
    Individual = 1,
}
