using Confluent.Kafka;
using ProcessingService;
using ProcessingService.Messaging;
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

// ── Kafka consumer for FHIR resources ──
builder.Services.AddSingleton<IConsumer<string, string>>(_ =>
{
    var bootstrapServers = builder.Configuration["Kafka:BootstrapServers"] ?? "localhost:9092";
    var groupId = builder.Configuration["Kafka:GroupId"] ?? "healthtech-processing";

    var config = new ConsumerConfig
    {
        BootstrapServers = bootstrapServers,
        GroupId = groupId,
        AutoOffsetReset = AutoOffsetReset.Earliest,
        EnableAutoCommit = true
    };

    return new ConsumerBuilder<string, string>(config).Build();
});

builder.Services.AddSingleton<IKafkaFhirConsumer, KafkaFhirConsumer>();

// ── Worker ──
builder.Services.AddHostedService<ProcessingWorker>();

var host = builder.Build();
host.Run();
