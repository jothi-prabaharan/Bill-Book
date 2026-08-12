using Microsoft.AspNetCore.Builder;

// Customer is scaffolded, not built: no entities, no controllers, no pages.
//
// It is one service over what used to be two — CRM and the support helpdesk.
// They share a subject (the person on the other side of the relationship) and
// a lifecycle (a lead becomes a customer, a customer raises a ticket), so a
// ticket wanting the campaign that won the account, or a lead wanting its open
// tickets, is a join rather than an HTTP call. Its schema is cus.
//
// The name overlaps plt.Customers, which means the head office that holds the
// account. This service is about the people that head office sells to and
// supports — read Customer.Api as the trading relationship, plt.Customer as
// the tenant.
//
// This file exists so the project compiles and the solution builds. It is a
// host that starts and reports what it is — deliberately not a stub of the real
// service, which would be a shape for someone to fill in without deciding
// whether it is the right shape.
//
// Replace it wholesale when the service is built; copy the Program.cs of an
// implemented service (Inventory is the fullest) rather than extending this.
WebApplication app = WebApplication.CreateBuilder(args).Build();

app.MapGet("/", () => Results.Ok(new
{
    service = "Customer",
    status = "not implemented",
}));

app.Run();
