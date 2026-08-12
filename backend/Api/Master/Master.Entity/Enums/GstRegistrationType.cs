namespace Master.Entity.Enums;

/// <summary>
/// How the contact is registered under GST. Decides whether a GSTIN is required,
/// and buckets the contact for GSTR-1 — B2B, B2C and exports are reported apart.
/// </summary>
public enum GstRegistrationType
{
    /// <summary>Ordinary registered dealer. GSTIN required.</summary>
    Regular = 0,

    /// <summary>Composition scheme — cannot charge GST on its own sales. GSTIN required.</summary>
    Composition = 1,

    /// <summary>Registered but no GSTIN captured yet, or below threshold.</summary>
    Unregistered = 2,

    /// <summary>Special Economic Zone. GSTIN required; supplies are zero-rated.</summary>
    Sez = 3,

    /// <summary>Outside India. No GSTIN; supplies are exports.</summary>
    Overseas = 4,

    /// <summary>Walk-in retail customer. No GSTIN, reported as B2C.</summary>
    Consumer = 5,
}
