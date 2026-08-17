using Microsoft.AspNetCore.Builder;

// Reporting is scaffolded, not built: no entities, no controllers, no pages.
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
    service = "Reporting",
    status = "not implemented",
}));

app.Run();

