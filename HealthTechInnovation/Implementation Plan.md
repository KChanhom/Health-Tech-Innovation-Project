# Health Tech Innovation Project – Items 1-3

Build the foundational .NET solution for a FHIR-native Health Tech platform with microservice architecture, FHIR client connectivity, and an ingestion service.

## Proposed Changes

### 1. Solution & Project Structure

Create a .NET 8 solution with 4 service projects + 1 shared library + 1 test project.

```
HealthTechInnovation/
├── HealthTechInnovation.sln
├── src/
│   ├── Shared/                        # Shared models & FHIR helpers
│   │   └── Shared.csproj
│   ├── IngestionService/              # Worker service for data ingestion
│   │   └── IngestionService.csproj
│   ├── ProcessingService/             # Worker service for validation & enrichment
│   │   └── ProcessingService.csproj
│   ├── ApiGateway/                    # ASP.NET Core Web API
│   │   └── ApiGateway.csproj
│   └── LLMService/                   # Web API for LLM integration
│       └── LLMService.csproj
└── tests/
    └── HealthTechInnovation.Tests/    # xUnit test project
        └── HealthTechInnovation.Tests.csproj
```

#### [NEW] [HealthTechInnovation.sln](file:///Users/Boyd/SourceCode/githubs/Health%20Tech%20Innovation%20Project/HealthTechInnovation/HealthTechInnovation.sln)

Created via `dotnet new sln` and `dotnet sln add` commands.

#### [NEW] [Shared.csproj](file:///Users/Boyd/SourceCode/githubs/Health%20Tech%20Innovation%20Project/HealthTechInnovation/src/Shared/Shared.csproj)

Class library with `Hl7.Fhir.R4` NuGet package. Houses:
- `FhirClientFactory` – Configurable `FhirClient` wrapper
- `FhirCrudService` – Basic CRUD helpers (Create, Read, Search)

#### [NEW] [IngestionService.csproj](file:///Users/Boyd/SourceCode/githubs/Health%20Tech%20Innovation%20Project/HealthTechInnovation/src/IngestionService/IngestionService.csproj)

Worker service (BackgroundService). Houses:
- `IDataSourceAdapter` interface + `EhrAdapter`, `IoTAdapter`, `ExternalSystemAdapter`
- `FhirSubscriptionManager` – Create/manage FHIR Subscriptions
- `BulkDataIngestionService` – $export + NDJSON download

---

### 2. FHIR Client & Basic CRUD (Shared Project)

#### [NEW] [FhirClientFactory.cs](file:///Users/Boyd/SourceCode/githubs/Health%20Tech%20Innovation%20Project/HealthTechInnovation/src/Shared/Fhir/FhirClientFactory.cs)

- Accept FHIR server URL via configuration (`appsettings.json` / env vars)
- Configure `FhirClientSettings` (timeout, preferred format, conformance validation)
- Return configured `FhirClient` instance

#### [NEW] [FhirCrudService.cs](file:///Users/Boyd/SourceCode/githubs/Health%20Tech%20Innovation%20Project/HealthTechInnovation/src/Shared/Fhir/FhirCrudService.cs)

- `CreatePatientAsync(Patient)` → `client.Create<Patient>()`
- `ReadPatientAsync(string id)` → `client.Read<Patient>()`
- `SearchPatientsAsync(params)` → `client.Search<Patient>()`

---

### 3. Ingestion Service

#### [NEW] [IDataSourceAdapter.cs](file:///Users/Boyd/SourceCode/githubs/Health%20Tech%20Innovation%20Project/HealthTechInnovation/src/IngestionService/Adapters/IDataSourceAdapter.cs)

Interface with `Task<IEnumerable<Resource>> FetchDataAsync()` and `string SourceName`.

#### [NEW] [EhrAdapter.cs](file:///Users/Boyd/SourceCode/githubs/Health%20Tech%20Innovation%20Project/HealthTechInnovation/src/IngestionService/Adapters/EhrAdapter.cs)

Simulates pulling Patient/Condition/Observation from an EHR system.

#### [NEW] [IoTAdapter.cs](file:///Users/Boyd/SourceCode/githubs/Health%20Tech%20Innovation%20Project/HealthTechInnovation/src/IngestionService/Adapters/IoTAdapter.cs)

Simulates pulling Observation resources from IoT medical devices.

#### [NEW] [ExternalSystemAdapter.cs](file:///Users/Boyd/SourceCode/githubs/Health%20Tech%20Innovation%20Project/HealthTechInnovation/src/IngestionService/Adapters/ExternalSystemAdapter.cs)

Simulates pulling Medication/AllergyIntolerance from external systems.

#### [NEW] [FhirSubscriptionManager.cs](file:///Users/Boyd/SourceCode/githubs/Health%20Tech%20Innovation%20Project/HealthTechInnovation/src/IngestionService/Subscriptions/FhirSubscriptionManager.cs)

- `CreateSubscriptionAsync()` – Create FHIR Subscription with rest-hook channel
- `ListSubscriptionsAsync()` – Search existing subscriptions
- `DeleteSubscriptionAsync()` – Clean up subscriptions

#### [NEW] [BulkDataIngestionService.cs](file:///Users/Boyd/SourceCode/githubs/Health%20Tech%20Innovation%20Project/HealthTechInnovation/src/IngestionService/BulkData/BulkDataIngestionService.cs)

- `StartExportAsync()` – Initiate $export operation
- `PollExportStatusAsync()` – Poll for completion
- `DownloadNdjsonFilesAsync()` – Download & parse NDJSON files
- `ParseNdjsonResourcesAsync()` – Deserialize FHIR resources

#### [NEW] [Hl7v2ToFhirTransformer.cs](file:///Users/Boyd/SourceCode/githubs/Health%20Tech%20Innovation%20Project/HealthTechInnovation/src/IngestionService/Hl7v2/Hl7v2ToFhirTransformer.cs)

- Parses HL7 v2 messages (PID/OBX) and converts them to FHIR `Patient` / `Observation`
- Handles edge cases (invalid dates, non-numeric values, cancellation)
- Intended to run before publishing resources to the message bus

#### [NEW] [KafkaFhirProducer.cs](file:///Users/Boyd/SourceCode/githubs/Health%20Tech%20Innovation%20Project/HealthTechInnovation/src/IngestionService/Messaging/KafkaFhirProducer.cs)

- Publishes FHIR resources as JSON messages to Kafka topic `fhir.resources`
- Uses `Confluent.Kafka` and Firely `FhirJsonSerializer`

#### [MODIFY] [IngestionWorker.cs](file:///Users/Boyd/SourceCode/githubs/Health%20Tech%20Innovation%20Project/HealthTechInnovation/src/IngestionService/IngestionWorker.cs)

- `BackgroundService` that orchestrates adapters on a configurable schedule
- Instead of writing directly to the FHIR server, publishes all ingested resources to Kafka via `IKafkaFhirProducer`

#### [MODIFY] [Program.cs – IngestionService](file:///Users/Boyd/SourceCode/githubs/Health%20Tech%20Innovation%20Project/HealthTechInnovation/src/IngestionService/Program.cs)

- Registers adapters, `Hl7v2ToFhirTransformer`, `KafkaFhirProducer`
- Configures Kafka `Producer<string,string>` from `Kafka:BootstrapServers`

---

### 4. Tests

#### [NEW] [FhirClientFactoryTests.cs](file:///Users/Boyd/SourceCode/githubs/Health%20Tech%20Innovation%20Project/HealthTechInnovation/tests/HealthTechInnovation.Tests/FhirClientFactoryTests.cs)

Tests for FhirClientFactory configuration and creation.

#### [NEW] [AdapterTests.cs](file:///Users/Boyd/SourceCode/githubs/Health%20Tech%20Innovation%20Project/HealthTechInnovation/tests/HealthTechInnovation.Tests/AdapterTests.cs)

Tests for each adapter implementation and cancellation behavior.

#### [NEW] [BulkDataIngestionServiceTests.cs](file:///Users/Boyd/SourceCode/githubs/Health%20Tech%20Innovation%20Project/HealthTechInnovation/tests/HealthTechInnovation.Tests/BulkDataIngestionServiceTests.cs)

Tests for bulk data export and NDJSON parsing (valid/empty/invalid lines).

#### [MODIFY] [IngestionWorkerTests.cs](file:///Users/Boyd/SourceCode/githubs/Health%20Tech%20Innovation%20Project/HealthTechInnovation/tests/HealthTechInnovation.Tests/IngestionWorkerTests.cs)

Verifies ingestion cycle publishes resources from all adapters to Kafka and handles failing adapters gracefully.

#### [NEW] [Hl7v2ToFhirTransformerTests.cs](file:///Users/Boyd/SourceCode/githubs/Health%20Tech%20Innovation%20Project/HealthTechInnovation/tests/HealthTechInnovation.Tests/Hl7v2ToFhirTransformerTests.cs)

Tests HL7 v2 PID/OBX → FHIR `Patient` / `Observation` mapping and edge cases (invalid dates, non-numeric values, cancellation).

#### [NEW] [KafkaMessagingTests.cs](file:///Users/Boyd/SourceCode/githubs/Health%20Tech%20Innovation%20Project/HealthTechInnovation/tests/HealthTechInnovation.Tests/KafkaMessagingTests.cs)

Tests `KafkaFhirProducer` / `KafkaFhirConsumer` serialization, topic usage, and error handling.

---

## Verification Plan

### Automated Tests

Run the full test suite:

```bash
cd /Users/Boyd/SourceCode/githubs/Health\ Tech\ Innovation\ Project/HealthTechInnovation
dotnet build
dotnet test --verbosity normal
```

All tests use mocked dependencies (no live FHIR server required).

### Manual Verification

1. Verify solution builds without errors via `dotnet build`
2. Verify all projects are correctly added to the solution via `dotnet sln list`
