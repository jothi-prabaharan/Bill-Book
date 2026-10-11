namespace Master.Entity.Enums;

/// <summary>
/// What a contact's address is (TK-188). Which address bills and which ships
/// is not this: it is the address's own Default billing and Default shipping
/// flags, and one address may be both. That still matters beyond display —
/// for goods the place of supply follows the shipping address, and that is
/// what decides CGST+SGST against IGST.
/// </summary>
public enum AddressKind
{
    RegisteredOffice = 0,
    Warehouse = 1,
    Branch = 2,
    Other = 3,
}
