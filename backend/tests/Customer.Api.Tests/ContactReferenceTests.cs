using System.Net;
using Customer.Api.Controllers;
using Customer.Api.Services;
using Customer.Entity.TableEntities;
using Customer.Repository;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Customer;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Customer.Api.Tests;

/// <summary>
/// Cross-organization <c>ContactId</c> references, which is the hole this suite
/// was written to close.
///
/// <b>The foreign key was never the guard.</b> <c>Lead.ConvertedContactId</c> and
/// <c>Ticket.ContactId</c> hold ids into <c>con.Contacts</c> — another service's
/// database, so there is no foreign key at all, and even where there is one it
/// proves the row exists rather than whose books it belongs to. Branch A could
/// raise a ticket against branch B's contact simply by naming the number, and
/// nothing in the request would look wrong.
///
/// The check is a question put to Contacts with the caller's own token forwarded,
/// so it is answered through that service's query filter and RLS policy. Here
/// that client is a stub: what these tests fix is the controller's behaviour when
/// the answer is no — a <c>Forbid()</c>, before anything is written — rather than
/// the HTTP call itself.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class ContactReferenceTests
{
    private readonly PostgresFixture _postgres;

    public ContactReferenceTests(PostgresFixture postgres) => _postgres = postgres;

    /// <summary>
    /// Contacts, as this service sees it: a set of ids that exist in the caller's
    /// branch, and nothing else.
    /// </summary>
    private sealed class StubContacts : IContactsClient
    {
        public HashSet<long> Mine { get; init; } = [];

        public CreatedContact? Created { get; init; }

        public HttpStatusCode? CreateRefusal { get; init; }

        public int CreateCalls { get; private set; }

        public Task<bool> ExistsInCallerOrgAsync(long contactId, CancellationToken ct) =>
            Task.FromResult(Mine.Contains(contactId));

        public Task<CreatedContact?> CreateAsync(NewContactRequest request, CancellationToken ct)
        {
            CreateCalls++;

            return CreateRefusal is HttpStatusCode status
                ? throw new ContactCreationFailedException(status, "refused")
                : Task.FromResult<CreatedContact?>(
                    Created ?? new CreatedContact(9001, "CON-9001", request.DisplayName));
        }
    }

    /// <summary>
    /// A real contact row in one branch.
    ///
    /// <b>These have to exist.</b> <c>cus.Leads</c> and <c>cus.Tickets</c> carry
    /// genuine foreign keys into <c>con.Contacts</c> — the two schemas share one
    /// physical database — so a test using an invented id would fail on the key
    /// rather than on the check it is about. The key is also exactly what these
    /// tests exist to show is not enough: it accepts any contact in the database,
    /// including one belonging to another branch.
    /// </summary>
    private async Task<long> SeedContactAsync(Guid customerId, Guid orgId, string name)
    {
        await using var contacts = new Master.Repository.ContactsDbContext(
            new DbContextOptionsBuilder<Master.Repository.ContactsDbContext>()
                .UseNpgsql(ConnectionString).Options,
            new TenantContext { CustomerId = customerId, OrgId = orgId });

        var contact = new Master.Entity.TableEntities.Contact
        {
            ContactCode = $"CON-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
            DisplayName = name,
            IsCustomer = true,
            CurrencyCode = "INR",
            IsActive = true,
        };

        contacts.Contacts.Add(contact);
        await contacts.SaveChangesAsync();

        return contact.ContactId;
    }

    private static string ConnectionString =>
        Environment.GetEnvironmentVariable("CUSTOMER_TEST_DB")
        ?? "Host=localhost;Port=5432;Database=customer_tests;Username=postgres;Password=123";

    private static LeadsController Leads(
        CustomerDbContext db, Guid orgId, IContactsClient contacts) =>
        new(db, new TenantContext { CustomerId = Guid.NewGuid(), OrgId = orgId }, contacts);

    private static TicketsController Tickets(
        CustomerDbContext db, Guid orgId, IContactsClient contacts) =>
        new(db, new TenantContext { CustomerId = Guid.NewGuid(), OrgId = orgId }, contacts);

    private async Task<(CustomerDbContext Db, Guid CustomerId, Guid OrgId, Lead Lead)> SeedLeadAsync()
    {
        Guid customerId = Guid.NewGuid();
        Guid orgId = Guid.NewGuid();

        CustomerDbContext db = _postgres.CreateContext(customerId, orgId);

        var lead = new Lead
        {
            Name = "Ravi Kumar",
            CompanyName = "Kumar Traders",
            Phone = "9876543210",
            Email = "ravi@example.com",
            Source = LeadSource.Other,
            Status = LeadStatus.New,
        };

        db.Leads.Add(lead);
        await db.SaveChangesAsync();

        return (db, customerId, orgId, lead);
    }

    // ---- Tickets ---------------------------------------------------------

    [SkippableFact]
    public async Task A_ticket_against_another_branchs_contact_is_forbidden()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid customerId = Guid.NewGuid();
        Guid orgId = Guid.NewGuid();
        await using CustomerDbContext db = _postgres.CreateContext(customerId, orgId);

        long mine = await SeedContactAsync(customerId, orgId, "My contact");

        // A real contact, in a real branch, that is not this caller's — the
        // foreign key would accept it without complaint.
        long theirs = await SeedContactAsync(customerId, Guid.NewGuid(), "Their contact");

        TicketsController controller = Tickets(db, orgId, new StubContacts { Mine = [mine] });

        IActionResult result = await controller.Create(
            new SaveTicketRequest { ContactId = theirs, Subject = "Cannot log in" },
            CancellationToken.None);

        Assert.IsType<ForbidResult>(result);

        // And nothing was written. A refusal that still left the row behind
        // would be a leak with a 403 on top of it.
        Assert.False(await db.Tickets.AnyAsync(t => t.ContactId == theirs));
    }

    [SkippableFact]
    public async Task A_ticket_against_the_callers_own_contact_is_created()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid customerId = Guid.NewGuid();
        Guid orgId = Guid.NewGuid();
        await using CustomerDbContext db = _postgres.CreateContext(customerId, orgId);

        long mine = await SeedContactAsync(customerId, orgId, "My contact");

        TicketsController controller = Tickets(db, orgId, new StubContacts { Mine = [mine] });

        IActionResult result = await controller.Create(
            new SaveTicketRequest { ContactId = mine, Subject = "Cannot log in" },
            CancellationToken.None);

        Assert.IsType<CreatedAtActionResult>(result);
        Assert.True(await db.Tickets.AnyAsync(t => t.ContactId == mine));
    }

    // ---- Lead conversion: existing contact --------------------------------

    [SkippableFact]
    public async Task Converting_to_another_branchs_contact_is_forbidden_and_leaves_the_lead_open()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        (CustomerDbContext db, Guid customerId, Guid orgId, Lead lead) = await SeedLeadAsync();
        await using CustomerDbContext _ = db;

        long mine = await SeedContactAsync(customerId, orgId, "My contact");
        long theirs = await SeedContactAsync(customerId, Guid.NewGuid(), "Their contact");

        LeadsController controller = Leads(db, orgId, new StubContacts { Mine = [mine] });

        IActionResult result = await controller.Convert(
            lead.LeadId, new ConvertLeadRequest { ContactId = theirs }, CancellationToken.None);

        Assert.IsType<ForbidResult>(result);

        Lead after = await db.Leads.AsNoTracking().FirstAsync(l => l.LeadId == lead.LeadId);
        Assert.Equal(LeadStatus.New, after.Status);
        Assert.Null(after.ConvertedContactId);
    }

    [SkippableFact]
    public async Task Converting_to_the_callers_own_contact_succeeds()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        (CustomerDbContext db, Guid customerId, Guid orgId, Lead lead) = await SeedLeadAsync();
        await using CustomerDbContext _ = db;

        long mine = await SeedContactAsync(customerId, orgId, "My contact");

        LeadsController controller = Leads(db, orgId, new StubContacts { Mine = [mine] });

        IActionResult result = await controller.Convert(
            lead.LeadId, new ConvertLeadRequest { ContactId = mine }, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);

        Lead after = await db.Leads.AsNoTracking().FirstAsync(l => l.LeadId == lead.LeadId);
        Assert.Equal(LeadStatus.Converted, after.Status);
        Assert.Equal(mine, after.ConvertedContactId);
        Assert.NotNull(after.ConvertedAt);
    }

    [SkippableFact]
    public async Task Another_branchs_lead_cannot_be_converted_at_all()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        (CustomerDbContext db, Guid customerId, Guid orgId, Lead lead) = await SeedLeadAsync();
        await using CustomerDbContext _ = db;

        // Same database, a different branch asking.
        await using CustomerDbContext theirs =
            _postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());

        LeadsController controller = Leads(
            theirs, Guid.NewGuid(), new StubContacts { Mine = [4242] });

        IActionResult result = await controller.Convert(
            lead.LeadId, new ConvertLeadRequest { ContactId = 4242 }, CancellationToken.None);

        // The query filter hides the row entirely, so this is the 404 that
        // "there is no such lead for you" earns — the Forbid() case is a lead
        // this caller can see but a contact they cannot.
        Assert.IsType<NotFoundResult>(result);
    }

    // ---- Lead conversion: a new contact -----------------------------------

    [SkippableFact]
    public async Task Converting_without_a_contact_id_creates_one_and_records_it()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        (CustomerDbContext db, Guid customerId, Guid orgId, Lead lead) = await SeedLeadAsync();
        await using CustomerDbContext _ = db;

        long created = await SeedContactAsync(customerId, orgId, "Kumar Traders");

        var contacts = new StubContacts
        {
            Created = new CreatedContact(created, "CON-7007", "Kumar Traders"),
        };

        LeadsController controller = Leads(db, orgId, contacts);

        IActionResult result = await controller.Convert(
            lead.LeadId, new ConvertLeadRequest { CreateContact = true }, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(1, contacts.CreateCalls);

        Lead after = await db.Leads.AsNoTracking().FirstAsync(l => l.LeadId == lead.LeadId);
        Assert.Equal(LeadStatus.Converted, after.Status);
        Assert.Equal(created, after.ConvertedContactId);

        // The created contact comes back, so the screen can go straight to it.
        Assert.Contains(
            created.ToString(System.Globalization.CultureInfo.InvariantCulture),
            System.Text.Json.JsonSerializer.Serialize(ok.Value),
            StringComparison.Ordinal);
    }

    [SkippableFact]
    public async Task A_lead_is_not_marked_converted_when_the_contact_could_not_be_created()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        (CustomerDbContext db, Guid customerId, Guid orgId, Lead lead) = await SeedLeadAsync();
        await using CustomerDbContext _ = db;

        LeadsController controller = Leads(
            db, orgId, new StubContacts { CreateRefusal = HttpStatusCode.BadRequest });

        IActionResult result = await controller.Convert(
            lead.LeadId, new ConvertLeadRequest { CreateContact = true }, CancellationToken.None);

        var status = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status502BadGateway, status.StatusCode);

        // The whole point: a lead marked converted against a contact that was
        // never created is a dangling reference nobody can see is dangling.
        Lead after = await db.Leads.AsNoTracking().FirstAsync(l => l.LeadId == lead.LeadId);
        Assert.Equal(LeadStatus.New, after.Status);
        Assert.Null(after.ConvertedContactId);
    }

    [SkippableFact]
    public async Task A_permission_refusal_from_contacts_comes_back_as_403_not_502()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        (CustomerDbContext db, Guid customerId, Guid orgId, Lead lead) = await SeedLeadAsync();
        await using CustomerDbContext _ = db;

        LeadsController controller = Leads(
            db, orgId, new StubContacts { CreateRefusal = HttpStatusCode.Forbidden });

        IActionResult result = await controller.Convert(
            lead.LeadId, new ConvertLeadRequest { CreateContact = true }, CancellationToken.None);

        var status = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, status.StatusCode);
    }

    [SkippableFact]
    public async Task A_lead_with_no_email_and_no_phone_cannot_become_a_new_contact()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid customerId = Guid.NewGuid();
        Guid orgId = Guid.NewGuid();
        await using CustomerDbContext db = _postgres.CreateContext(customerId, orgId);

        var lead = new Lead { Name = "Anonymous walk-in", Status = LeadStatus.New };
        db.Leads.Add(lead);
        await db.SaveChangesAsync();

        var contacts = new StubContacts();
        LeadsController controller = Leads(db, orgId, contacts);

        IActionResult result = await controller.Convert(
            lead.LeadId, new ConvertLeadRequest { CreateContact = true }, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);

        // Refused here rather than by Contacts, so no pointless round trip.
        Assert.Equal(0, contacts.CreateCalls);
    }

    // ---- The two paths are exclusive --------------------------------------

    [SkippableFact]
    public async Task Naming_a_contact_and_asking_for_a_new_one_is_refused()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        (CustomerDbContext db, Guid customerId, Guid orgId, Lead lead) = await SeedLeadAsync();
        await using CustomerDbContext _ = db;

        LeadsController controller = Leads(db, orgId, new StubContacts { Mine = [4242] });

        IActionResult result = await controller.Convert(
            lead.LeadId,
            new ConvertLeadRequest { ContactId = 4242, CreateContact = true },
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [SkippableFact]
    public async Task Naming_neither_is_refused_rather_than_defaulted()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        (CustomerDbContext db, Guid customerId, Guid orgId, Lead lead) = await SeedLeadAsync();
        await using CustomerDbContext _ = db;

        var contacts = new StubContacts();
        LeadsController controller = Leads(db, orgId, contacts);

        IActionResult result = await controller.Convert(
            lead.LeadId, new ConvertLeadRequest(), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(0, contacts.CreateCalls);
    }

    [SkippableFact]
    public async Task A_lead_cannot_be_converted_twice()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        (CustomerDbContext db, Guid customerId, Guid orgId, Lead lead) = await SeedLeadAsync();
        await using CustomerDbContext _ = db;

        long mine = await SeedContactAsync(customerId, orgId, "My contact");

        var contacts = new StubContacts { Mine = [mine] };
        LeadsController controller = Leads(db, orgId, contacts);

        Assert.IsType<OkObjectResult>(await controller.Convert(
            lead.LeadId, new ConvertLeadRequest { ContactId = mine }, CancellationToken.None));

        IActionResult again = await controller.Convert(
            lead.LeadId, new ConvertLeadRequest { CreateContact = true }, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(again);

        // Refused before Contacts is asked, so a double-click cannot leave a
        // second contact behind.
        Assert.Equal(0, contacts.CreateCalls);
    }
}
