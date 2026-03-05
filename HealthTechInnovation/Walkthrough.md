# Health Tech Innovation Project – Walkthrough

## Summary

Implemented items 1–4 of the project plan: .NET solution setup, FHIR client with CRUD operations, ingestion service with adapters/HL7 v2/Bulk/Kafka, and processing pipeline (validation, terminology enrichment, persistence) driven by Kafka.

## Project Structure

```
HealthTechInnovation/
├── HealthTechInnovation.sln
├── src/
│   ├── Shared/                        # FHIR client factory & CRUD service
│   │   ├── Configuration/
│   │   │   └── FhirServerSettings.cs
│   │   └── Fhir/
│   │       ├── IFhirClientFactory.cs
│   │       ├── FhirClientFactory.cs
│   │       ├── IFhirCrudService.cs
│   │       └── FhirCrudService.cs
│   ├── IngestionService/              # Data ingestion → Kafka worker service
│   │   ├── Adapters/
│   │   │   ├── IDataSourceAdapter.cs
│   │   │   ├── EhrAdapter.cs
│   │   │   ├── IoTAdapter.cs
│   │   │   └── ExternalSystemAdapter.cs
│   │   ├── Hl7v2/
│   │   │   └── Hl7v2ToFhirTransformer.cs
│   │   ├── Subscriptions/
│   │   │   └── FhirSubscriptionManager.cs
│   │   ├── BulkData/
│   │   │   └── BulkDataIngestionService.cs
│   │   ├── Messaging/
│   │   │   └── KafkaFhirProducer.cs
│   │   ├── IngestionWorker.cs
│   │   └── Program.cs
│   ├── ProcessingService/             # Kafka consumer → validate/enrich/persist
│   │   ├── Messaging/
│   │   │   └── KafkaFhirConsumer.cs
│   │   ├── Validation/
│   │   │   ├── FhirValidationService.cs
│   │   │   └── ValidationResult.cs
│   │   ├── Terminology/
│   │   │   └── TerminologyService.cs
│   │   ├── Persistence/
│   │   │   └── ResourcePersistenceService.cs
│   │   ├── ProcessingWorker.cs
│   │   └── Program.cs
│   ├── ApiGateway/                    # Web API for FHIR CRUD
│   └── LLMService/                    # Web API for LLM integration
└── tests/
    └── HealthTechInnovation.Tests/
        ├── FhirClientFactoryTests.cs
        ├── AdapterTests.cs
        ├── BulkDataIngestionServiceTests.cs
        ├── IngestionWorkerTests.cs
        ├── Hl7v2ToFhirTransformerTests.cs
        ├── KafkaMessagingTests.cs
        ├── FhirValidationServiceTests.cs
        ├── TerminologyServiceTests.cs
        ├── ResourcePersistenceServiceTests.cs
        ├── PatientControllerTests.cs
        └── ObservationControllerTests.cs
```

## Key Components

| Component | Description |
|---|---|
| **FhirClientFactory** | Creates configured `FhirClient` instances from `appsettings.json` |
| **FhirCrudService** | Patient-specific and generic CRUD operations with error handling |
| **EhrAdapter** | Fetches Patient, Condition, Observation from EHR systems |
| **IoTAdapter** | Fetches vital signs (heart rate, BP, SpO2) from IoT devices |
| **ExternalSystemAdapter** | Fetches Medication, MedicationRequest, AllergyIntolerance |
| **Hl7v2ToFhirTransformer** | Converts HL7 v2 PID/OBX into FHIR Patient/Observation resources |
| **FhirSubscriptionManager** | Creates/lists/deletes FHIR Subscriptions with rest-hook channel |
| **BulkDataIngestionService** | $export initiation, status polling, NDJSON download/parsing |
| **KafkaFhirProducer** | Serializes FHIR resources to JSON and publishes to Kafka (`fhir.resources`) |
| **KafkaFhirConsumer** | Consumes FHIR JSON from Kafka and parses back to `Resource` |
| **IngestionWorker** | BackgroundService orchestrating adapters and publishing to Kafka |
| **ProcessingWorker** | BackgroundService consuming from Kafka → Validate → Enrich → Persist |

## How to Run (Local)

### 1. Prerequisites

- .NET 8 SDK ติดตั้งบนเครื่อง
- Kafka cluster สำหรับทดสอบ (เช่น `localhost:9092`) และมี topic `fhir.resources` (หรือใช้ค่า default)

### 2. Build ทั้ง Solution

```bash
cd HealthTechInnovation
dotnet build
```

### 3. รัน Service ต่างๆ

เปิด 3 terminal:

- **IngestionService** (ดึงข้อมูลจาก adapters → ส่งเข้า Kafka)

```bash
cd HealthTechInnovation/src/IngestionService
dotnet run
```

- **ProcessingService** (รับจาก Kafka → Validate + Enrich + Persist)

```bash
cd HealthTechInnovation/src/ProcessingService
dotnet run
```

- **ApiGateway** (ให้บริการ REST API)

```bash
cd HealthTechInnovation/src/ApiGateway
dotnet run
```

Swagger UI จะอยู่ที่ `http://localhost:<port>/swagger`.

### 4. การรัน Test Suite

```bash
cd HealthTechInnovation
dotnet test
```

Test จะครอบคลุม:

- Shared (FhirClientFactory, Validation, Terminology, Persistence)
- Ingestion (Adapters, BulkData, HL7 v2 transformer, IngestionWorker, Kafka messaging)
- ApiGateway (Patient & Observation controllers)
