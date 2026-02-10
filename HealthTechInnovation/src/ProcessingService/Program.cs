using ProcessingService;
using ProcessingService.Persistence;
using ProcessingService.Terminology;
using ProcessingService.Validation;
using Shared.Configuration;
using Shared.Fhir;

var builder = Host.CreateApplicationBuilder(args);

// ── Configuration ──
builder.Services.Configure<FhirServerSettings>(
    builder.Configuration.GetSection(FhirServerSettings.SectionName));

// ── FHIR services ──
builder.Services.AddSingleton<IFhirClientFactory, FhirClientFactory>();
builder.Services.AddSingleton<IFhirCrudService, FhirCrudService>();

// ── Processing services ──
builder.Services.AddSingleton<FhirValidationService>();
builder.Services.AddSingleton<TerminologyService>();
builder.Services.AddSingleton<ResourcePersistenceService>();

// ── Worker ──
builder.Services.AddHostedService<ProcessingWorker>();

var host = builder.Build();
host.Run();
