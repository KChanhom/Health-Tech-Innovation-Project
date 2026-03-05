using Confluent.Kafka;
using IngestionService;
using IngestionService.Adapters;
using IngestionService.BulkData;
using IngestionService.Hl7v2;
using IngestionService.Messaging;
using IngestionService.Subscriptions;
using Shared.Configuration;
using Shared.Fhir;

var builder = Host.CreateApplicationBuilder(args);

// ── Configuration ──
builder.Services.Configure<FhirServerSettings>(
    builder.Configuration.GetSection(FhirServerSettings.SectionName));

// ── Adapters ──
builder.Services.AddSingleton<IDataSourceAdapter, EhrAdapter>();
builder.Services.AddSingleton<IDataSourceAdapter, IoTAdapter>();
builder.Services.AddSingleton<IDataSourceAdapter, ExternalSystemAdapter>();

// ── Subscription management ──
builder.Services.AddSingleton<FhirSubscriptionManager>();

// ── Bulk Data ──
builder.Services.AddHttpClient<BulkDataIngestionService>();

// ── HL7 v2 → FHIR transformer ──
builder.Services.AddSingleton<IHL7v2ToFhirTransformer, Hl7v2ToFhirTransformer>();

// ── Kafka producer for FHIR resources ──
builder.Services.AddSingleton<IProducer<string, string>>(_ =>
{
    var bootstrapServers = builder.Configuration["Kafka:BootstrapServers"] ?? "localhost:9092";
    var config = new ProducerConfig
    {
        BootstrapServers = bootstrapServers
    };
    return new ProducerBuilder<string, string>(config).Build();
});

builder.Services.AddSingleton<IKafkaFhirProducer, KafkaFhirProducer>();

// ── Worker ──
builder.Services.AddHostedService<IngestionWorker>();

var host = builder.Build();
host.Run();
