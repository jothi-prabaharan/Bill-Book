namespace Reporting.Entity.Enums;

/// <summary>
/// What an invoice's age is counted from on the Aged Receivables reports (TK-222):
/// the day it falls due, or the day it was raised.
/// </summary>
public enum AgeBasis
{
    DueDate = 1,
    InvoiceDate = 2,
}
