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

# Items 4-5: Processing Service & API Gateway

Build the FHIR resource validation/enrichment pipeline (ProcessingService) and the ASP.NET Core API Gateway with domain-specific controllers.

## Proposed Changes

### ProcessingService (Worker Service)

Receives FHIR resources, validates, enriches with terminology codes, and persists to FHIR server.

#### [NEW] [FhirValidationService.cs](file:///Users/Boyd/SourceCode/githubs/Health%20Tech%20Innovation%20Project/HealthTechInnovation/src/ProcessingService/Validation/FhirValidationService.cs)
- `ValidateResourceAsync(Resource)` → uses Firely validation + `client.Validate()`
- Returns `ValidationResult` with errors/warnings

#### [NEW] [TerminologyService.cs](file:///Users/Boyd/SourceCode/githubs/Health%20Tech%20Innovation%20Project/HealthTechInnovation/src/ProcessingService/Terminology/TerminologyService.cs)
- `ValidateCodeAsync(system, code)` → `$validate-code`
- `LookupCodeAsync(system, code)` → `$lookup`
- `TranslateCodeAsync(source, target, code)` → concept mapping
- Supports ICD-10, SNOMED-CT, LOINC

#### [NEW] [ResourcePersistenceService.cs](file:///Users/Boyd/SourceCode/githubs/Health%20Tech%20Innovation%20Project/HealthTechInnovation/src/ProcessingService/Persistence/ResourcePersistenceService.cs)
- `SaveResourceAsync(Resource)` → Create or Update
- `SaveBatchAsync(IEnumerable<Resource>)` → `client.Transaction()` with Bundle
- Response handling (201/200/4xx/5xx)

#### [NEW] [ProcessingWorker.cs](file:///Users/Boyd/SourceCode/githubs/Health%20Tech%20Innovation%20Project/HealthTechInnovation/src/ProcessingService/ProcessingWorker.cs)
- BackgroundService pipeline: Validate → Enrich → Persist
- In-memory queue for demo (replaceable with message bus)

#### [MODIFY] [Program.cs](file:///Users/Boyd/SourceCode/githubs/Health%20Tech%20Innovation%20Project/HealthTechInnovation/src/ProcessingService/Program.cs)
- Wire DI for all services + FhirServerSettings

---

### ApiGateway (ASP.NET Core Web API)

RESTful gateway with JWT authentication and domain controllers.

#### [MODIFY] [Program.cs](file:///Users/Boyd/SourceCode/githubs/Health%20Tech%20Innovation%20Project/HealthTechInnovation/src/ApiGateway/Program.cs)
- Replace weather forecast placeholder
- Add JWT Bearer authentication
- Add Swagger/OpenAPI, CORS
- Register FHIR services + controllers

#### [NEW] [PatientController.cs](file:///Users/Boyd/SourceCode/githubs/Health%20Tech%20Innovation%20Project/HealthTechInnovation/src/ApiGateway/Controllers/PatientController.cs)
- `GET /api/patients` – Search patients
- `GET /api/patients/{id}` – Get by ID
- `POST /api/patients` – Create patient
- `PUT /api/patients/{id}` – Update patient
- `DELETE /api/patients/{id}` – Delete patient

#### [NEW] [ObservationController.cs](file:///Users/Boyd/SourceCode/githubs/Health%20Tech%20Innovation%20Project/HealthTechInnovation/src/ApiGateway/Controllers/ObservationController.cs)
- `GET /api/observations` – Search observations
- `GET /api/observations/{id}` – Get by ID
- `POST /api/observations` – Create observation

#### [NEW] [SchedulingController.cs](file:///Users/Boyd/SourceCode/githubs/Health%20Tech%20Innovation%20Project/HealthTechInnovation/src/ApiGateway/Controllers/SchedulingController.cs)
- `GET /api/appointments` – Search appointments
- `GET /api/appointments/{id}` – Get by ID
- `POST /api/appointments` – Create appointment
- `PUT /api/appointments/{id}` – Update appointment

---

### Tests

- `FhirValidationServiceTests` – Validation logic
- `TerminologyServiceTests` – Code lookup/validation
- `ResourcePersistenceServiceTests` – Batch/single persistence
- `PatientControllerTests` – API endpoint tests
- `ObservationControllerTests` – API endpoint tests

## Verification Plan

```bash
dotnet build
dotnet test --verbosity normal
```
