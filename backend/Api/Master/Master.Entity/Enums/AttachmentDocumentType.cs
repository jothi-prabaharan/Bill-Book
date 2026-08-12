namespace Master.Entity.Enums;

/// <summary>What an uploaded file is, so the list can be read without opening each one.</summary>
public enum AttachmentDocumentType
{
    GstCertificate = 0,
    PanCard = 1,
    Agreement = 2,
    PurchaseOrder = 3,
    CancelledCheque = 4,
    Msme = 5,
    Licence = 6,
    Other = 7,
}
