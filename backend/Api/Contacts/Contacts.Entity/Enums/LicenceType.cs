namespace Contacts.Entity.Enums;

/// <summary>
/// A registration a contact holds. Each carries an expiry, and expiry is the
/// point: trading with a chemist whose drug licence has lapsed is an offence,
/// not an inconvenience.
/// </summary>
public enum LicenceType
{
    /// <summary>Drugs and Cosmetics Act — Form 20/21 retail, 20B/21B wholesale.</summary>
    DrugLicence = 0,

    /// <summary>Food safety, for nutraceuticals and food lines.</summary>
    Fssai = 1,

    /// <summary>BIS registration, for hallmarked jewellery.</summary>
    Bis = 2,

    /// <summary>A prescriber's medical council registration.</summary>
    MedicalRegistration = 3,

    Other = 4,
}
