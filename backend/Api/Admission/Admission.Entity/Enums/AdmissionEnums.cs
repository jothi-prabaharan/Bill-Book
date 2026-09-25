namespace Admission.Entity.Enums;

// The adm schema's fixed sets (S2, TK-62), stored by name.

public enum EnquirySource
{
    WalkIn = 1,
    Website = 2,
    Referral = 3,
    Advertisement = 4,
    Other = 5,
}

public enum EnquiryStatus
{
    Open = 1,
    FollowUp = 2,
    Converted = 3,
    Lost = 4,
}

/// <summary>
/// Submitted → DocumentsVerified → Assessed → Offered → Admitted, or Rejected or
/// Withdrawn from any stage before Admitted.
/// </summary>
public enum ApplicationStage
{
    Submitted = 1,
    DocumentsVerified = 2,
    Assessed = 3,
    Offered = 4,
    Admitted = 5,
    Rejected = 6,
    Withdrawn = 7,
}

public enum DocumentKind
{
    BirthCertificate = 1,
    TransferCertificate = 2,
    ReportCard = 3,
    Photo = 4,
    AddressProof = 5,
    Other = 6,
}

public enum ChildGender
{
    Male = 1,
    Female = 2,
    Other = 3,
    NotStated = 4,
}

public enum ParentRelationship
{
    Father = 1,
    Mother = 2,
    Guardian = 3,
    Other = 4,
}
