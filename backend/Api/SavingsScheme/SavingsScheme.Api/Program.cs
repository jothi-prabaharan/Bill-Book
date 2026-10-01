using Microsoft.EntityFrameworkCore;
using SavingsScheme.Repository;
using Shared.Kernel.Tenancy;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// Add DbContext
builder.Services.AddScoped<TenantContext>();
builder.Services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
builder.Services.AddDbContext<SavingsSchemeDbContext>((sp, options) =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("TenantDatabase") ?? "Host=localhost;Database=billbook_tenant;Username=postgres;Password=postgres");
});

var app = builder.Build();

app.UseRouting();
app.MapControllers();

app.Run();
