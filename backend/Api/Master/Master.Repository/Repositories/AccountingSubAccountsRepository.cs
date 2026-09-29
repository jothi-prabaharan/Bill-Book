using Master.Repository.Clients;
using System.Net.Http.Json;
using Shared.Kernel.Tenancy;

namespace Master.Repository.Repositories;

/// <summary>
/// Creates a contact's six sub-accounts in Accounting — the trade balance, a
/// prepayment advance and an overpayment advance, beneath each of Accounts
/// Receivable and Accounts Payable.
/// Only Accounting writes ledger rows, so this is a call rather than an insert.
/// </summary>
public interface IAccountingSubAccounts
{
    /// <summary>True when the sub-ledger was provisioned. False leaves the contact without one.</summary>
    Task<bool> ProvisionForContactAsync(long contactId, string displayName, CancellationToken ct);
}
