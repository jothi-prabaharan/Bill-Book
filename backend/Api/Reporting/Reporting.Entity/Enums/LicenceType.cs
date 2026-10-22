namespace Reporting.Entity.Enums;

/// <summary>
/// Master's <c>LicenceType</c> on a contact's licence. Master stores it by name, so these names must stay Master's;
/// the numbers are only this copy's (TK-213).
/// </summary>
public enum LicenceType
{
    DrugLicence = 0,
    Fssai = 1,
    Bis = 2,
    MedicalRegistration = 3,
    Other = 4,
}
