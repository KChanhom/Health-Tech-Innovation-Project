using IngestionService;
using IngestionService.Adapters;
using IngestionService.BulkData;
using IngestionService.Subscriptions;
using Shared.Configuration;
using Shared.Fhir;

var builder = Host.CreateApplicationBuilder(args);

// ── Configuration ──
builder.Services.Configure<FhirServerSettings>(
    builder.Configuration.GetSection(FhirServerSettings.SectionName));

// ── FHIR services ──
builder.Services.AddSingleton<IFhirClientFactory, FhirClientFactory>();
builder.Services.AddSingleton<IFhirCrudService, FhirCrudService>();

// ── Adapters ──
builder.Services.AddSingleton<IDataSourceAdapter, EhrAdapter>();
builder.Services.AddSingleton<IDataSourceAdapter, IoTAdapter>();
builder.Services.AddSingleton<IDataSourceAdapter, ExternalSystemAdapter>();

// ── Subscription management ──
builder.Services.AddSingleton<FhirSubscriptionManager>();

// ── Bulk Data ──
builder.Services.AddHttpClient<BulkDataIngestionService>();

// ── Worker ──
builder.Services.AddHostedService<IngestionWorker>();

var host = builder.Build();
host.Run();
