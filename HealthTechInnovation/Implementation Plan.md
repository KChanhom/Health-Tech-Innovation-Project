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

#### [NEW] [IngestionWorker.cs](file:///Users/Boyd/SourceCode/githubs/Health%20Tech%20Innovation%20Project/HealthTechInnovation/src/IngestionService/IngestionWorker.cs)

`BackgroundService` that orchestrates adapters on a configurable schedule.

---

### 4. Tests

#### [NEW] [FhirClientFactoryTests.cs](file:///Users/Boyd/SourceCode/githubs/Health%20Tech%20Innovation%20Project/HealthTechInnovation/tests/HealthTechInnovation.Tests/FhirClientFactoryTests.cs)

Tests for FhirClientFactory configuration and creation.

#### [NEW] [FhirCrudServiceTests.cs](file:///Users/Boyd/SourceCode/githubs/Health%20Tech%20Innovation%20Project/HealthTechInnovation/tests/HealthTechInnovation.Tests/FhirCrudServiceTests.cs)

Tests for CRUD operations (mocked FhirClient).

#### [NEW] [AdapterTests.cs](file:///Users/Boyd/SourceCode/githubs/Health%20Tech%20Innovation%20Project/HealthTechInnovation/tests/HealthTechInnovation.Tests/AdapterTests.cs)

Tests for each adapter implementation.

#### [NEW] [BulkDataIngestionServiceTests.cs](file:///Users/Boyd/SourceCode/githubs/Health%20Tech%20Innovation%20Project/HealthTechInnovation/tests/HealthTechInnovation.Tests/BulkDataIngestionServiceTests.cs)

Tests for bulk data export and NDJSON parsing.

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
