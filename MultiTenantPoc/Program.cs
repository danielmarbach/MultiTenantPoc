using Microsoft.Extensions.Hosting;
using MultiTenantPoc;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOpenApi();

builder.Services.AddMultiTenantOptions(builder.Configuration);

builder.Logging.ClearProviders();
builder.Logging.AddProvider(new EndpointColorConsoleLoggerProvider());
builder.AddServiceDefaults();

var pocOptions = builder.Configuration.GetSection(PocOptions.SectionName).Get<PocOptions>()
    ?? throw new InvalidOperationException($"Configuration section '{PocOptions.SectionName}' is missing.");

var endpointCatalog = new EndpointCatalog(pocOptions);
builder.Services.AddSingleton(endpointCatalog);
builder.Services.AddSingleton<TenantDatabaseInitializer>();

builder.Services.AddMultiTenantNServiceBusEndpoints(pocOptions, endpointCatalog);

var app = builder.Build();
app.UseOpenApi();

await app.EnsureTenantDatabasesCreatedAsync(pocOptions, endpointCatalog);

app.MapPocEndpoints();
app.MapDefaultEndpoints();

app.Run();
