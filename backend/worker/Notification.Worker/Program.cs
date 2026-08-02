using Microsoft.Extensions.Hosting;

// Notification.Worker is scaffolded, not built: the Consumers folder is empty.
//
// This file exists so the project compiles and the solution builds. It starts a
// host that does nothing, rather than a stub consumer that would look like a
// starting point without anyone having decided the shape.
//
// CostingEngine.Worker is the one that is built; copy its Program.cs when this
// service is written.
await Host.CreateApplicationBuilder(args).Build().RunAsync();
