# คู่มือเชิงลึก: โครงสร้างและการทำงานของ Health Tech Innovation Project (FHIR-Native)

เอกสารนี้จะอธิบายรายละเอียดทางเทคนิคเชิงลึก โครงสร้างไฟล์ หน้าที่การทำงานแต่ละส่วน และเทคโนโลยีที่ใช้ในแต่ละ Service ของโปรเจค

## ภาพรวมสถาปัตยกรรม (Architecture Overview)

โปรเจคถูกออกแบบเป็น **Microservices Architecture** โดยใช้ **.NET 8** เป็นแกนหลัก และสื่อสารกันผ่าน FHIR Standard (R4) หัวใจสำคัญคือการแยกหน้าที่การทำงานอย่างชัดเจนระหว่างการนำเข้าข้อมูล (Ingestion), การประมวลผล (Processing) และการให้บริการข้อมูล (API Gateway) เพื่อความยืดหยุ่นและรองรับการขยายตัว (Scalability)

---

## เจาะลึกแต่ละ Service และ Component

### 1. Shared Project (Library กลาง)
**หน้าที่:** เป็นศูนย์รวม Logic พื้นฐาน, Model, และ Interface ที่ใช้ร่วมกันทุก Service เพื่อลดโค้ดซ้ำซ้อน
**เทคโนโลยี:** .NET 8 Class Library, Firely SDK (`Hl7.Fhir.R4`)

- **`FhirClientFactory.cs`**
  - **หน้าที่:** สร้างและตั้งค่า `FhirClient` (ตัวเชื่อมต่อ FHIR server) ตาม Configuration ที่กำหนดใน `appsettings.json` เช่น URL, Timeout, และ Format (JSON/XML)
  - **จุดเด่น:** ช่วยให้ทุก Service เชื่อมต่อ Server ในมาตรฐานเดียวกัน

- **`IFhirCrudService.cs` / `FhirCrudService.cs`**
  - **หน้าที่:** เป็น Wrapper Class ครอบ `FhirClient` อีกชั้นหนึ่ง เพื่อให้บริการ CRUD (Create, Read, Update, Delete) พื้นฐาน และคำสั่งพิเศษ เช่น Transaction และ $validate
  - **ความสำคัญ:** ช่วยแก้ปัญหาเรื่องการเขียน Unit Test (Mocking) ของ Firely SDK ซึ่งบาง Method ไม่สามารถ Mock ได้โดยตรง การมี Wrapper ทำให้เราสามารถ Mock `IFhirCrudService` ได้ง่ายและสะอาดกว่า

---

### 2. Ingestion Service (Worker Service)
**หน้าที่:** ดูดข้อมูลนำเข้าจากแหล่งต่างๆ (EHR, IoT, External Systems) เข้าสู่ระบบ
**เทคโนโลยี:** .NET 8 Worker Service, `Microsoft.Extensions.Hosting`

- **`IngestionWorker.cs`**
  - **หน้าที่:** เป็น Background Service หลักที่ทำงานวนลูป (Infinite Loop) ตามตารางเวลา (Interval) คอยสั่งให้ Adapter ต่างๆ ทำงานดึงข้อมูลและส่ง Process ต่อ

- **`Adapters/`** (ตัวแปลงข้อมูล)
  - **`IDataSourceAdapter.cs`:** Interface กลางที่กำหนดให้ทุก Adapter ต้องมี Method `FetchDataAsync`
  - **`EhrAdapter.cs`:** จำลองการดึงข้อมูลผู้ป่วย (Patient) และผลตรวจ (Observation) จากระบบเวชระเบียนเดิม (EHR)
  - **`IoTAdapter.cs`:** จำลองการดึงค่าสัญญาณชีพ (Heart Rate, BP) จากอุปกรณ์ IoT หรือ Wearable
  - **`ExternalSystemAdapter.cs`:** ดึงข้อมูลยา (Medication) หรือประวัติการแพ้จากภายนอก

- **`BulkData/BulkDataIngestionService.cs`**
  - **หน้าที่:** จัดการกับ FHIR Bulk Data API ($export) สำหรับข้อมูลปริมาณมหาศาล
  - **การทำงาน:** สั่ง Export -> รอสถานะ (Polling) -> ดาวน์โหลดไฟล์ NDJSON -> แตกไฟล์เป็น Resource

- **`Subscriptions/FhirSubscriptionManager.cs`**
  - **หน้าที่:** จัดการ FHIR Subscription (R4) เพื่อรอรับการแจ้งเตือนแบบ Real-time (Rest-Hook) เมื่อมีการเปลี่ยนแปลงข้อมูลบน Server

---

### 3. Processing Service (Worker Service)
**หน้าที่:** ตรวจสอบความถูกต้อง (Validation), แปลงรหัสมาตรฐาน (Terminology), และบันทึกข้อมูล (Persistence)
**เทคโนโลยี:** .NET 8 Worker Service, Firely SDK

- **`ProcessingWorker.cs`**
  - **หน้าที่:** รับช่วงต่อข้อมูล (ในระบบจริงอาจผ่าน Message Queue เช่น RabbitMQ) และส่งเข้า Pipeline: Validate -> Enrich -> Persist

- **`Validation/FhirValidationService.cs`**
  - **หน้าที่:** ตรวจสอบความถูกต้องของ FHIR Resource
  - **การทำงาน:**
    1. **Structural Validation:** ตรวจ Field บังคับ, Data Type (ทำในโค้ด)
    2. **Server-side Validation:** เรียก Operation `$validate` ไปยัง FHIR Server เพื่อตรวจกฎที่ซับซ้อน (Profiles, Constraints)

- **`Terminology/TerminologyService.cs`**
  - **หน้าที่:** จัดการเรื่องมาตรฐานรหัสทางการแพทย์ (SNOMED-CT, LOINC, ICD-10)
  - **การทำงาน:** ใช้ Operation `$validate-code` เพื่อเช็ครหัส และ `$lookup` เพื่อค้นหาหรือเติมเต็มข้อมูล (Enrichment) เช่น หาชื่อโรคจากรหัส

- **`Persistence/ResourcePersistenceService.cs`**
  - **หน้าที่:** บันทึกข้อมูลลง FHIR Server
  - **จุดเด่น:** รองรับ **Transaction Bundle** (Atomic Update) คือการบันทึกข้อมูลหลายรายการพร้อมกัน ถ้าพังตัวเดียวให้ Rollback ทั้งหมด รับประกันความสมบูรณ์ของข้อมูล

---

### 4. Api Gateway (Web API)
**หน้าที่:** เป็นประตูหน้าบ้าน (Entry Point) ให้แอปพลิเคชันภายนอกเชื่อมต่ออย่างปลอดภัย
**เทคโนโลยี:** ASP.NET Core Web API (.NET 8), JWT Authentication, Swagger (OpenAPI)

- **ความปลอดภัย (Security):**
  - **JWT Authentication:** รองรับการยืนยันตัวตนผ่าน Bearer Token
  - **CORS:** เปิดให้ Web Client (เช่น Frontend) เรียกใช้งานได้
  - **HTTPS:** บังคับใช้การสื่อสารที่ปลอดภัย

- **Controllers:**
  - **`PatientController`:** ค้นหา, สร้าง, แก้ไข, ลบข้อมูลผู้ป่วย (Patient)
  - **`ObservationController`:** ดูประวัติการรักษาและสัญญาณชีพ (Observation) พร้อมรองรับการค้นหาตามผู้ป่วย
  - **`SchedulingController`:** จัดการการนัดหมาย (Appointment)

- **Documentation:**
  - มี **Swagger UI** ในตัว สำหรับทดลองยิง API และดูเอกสารประกอบ

---

### 5. Tests (Testing Project)
**หน้าที่:** ทดสอบการทำงานของ Logic ทั้งหมดโดยไม่พึ่งพา FHIR Server จริง
**เทคโนโลยี:** xUnit, Moq, FluentAssertions

- **ความครอบคลุม (Coverage):**
  - **Total Tests:** 32 Tests (Updated)
  - ครอบคลุมทั้ง Ingestion Adapters, Bulk Data Logic, Validation Rules, Persistence Logic และ **API Controllers**
- **เทคนิค:**
  - ใช้ **Moq** จำลองการทำงานของ `IFhirCrudService` ทำให้ทดสอบ Business Logic ใน Controller และ Service ได้อย่างอิสระ
  - จำลองการตอบกลับของ Server ทั้งแบบ Success และ Error Cases

---

## ขั้นตอนการรันโปรแกรม (How to Run)

### 1. การเตรียมความพร้อม (Prerequisites)
- ติดตั้ง **.NET 8 SDK**
- (Optional) มี FHIR Server ปลายทาง (เช่น HAPI FHIR หรือ Firely Server) ถ้าต้องการรันจริง แต่ถ้าจะรัน Test ไม่ต้องใช้

### 2. การ Build โปรเจค
รันคำสั่งที่ root folder ของโปรเจค:
```bash
dotnet build
```
โครงการทั้งหมด (รวมถึง ApiGateway) ถูกปรับให้เป็น **.NET 8** แล้ว ควร Build ผ่านโดยไม่มี Error

### 3. การรัน Unit Tests
เพื่อตรวจสอบความถูกต้องของระบบทั้งหมด รันคำสั่ง:
```bash
dotnet test
```
ผลลัพธ์ควรแสดงจำนวน Test ที่ผ่านทั้งหมด **32 Tests**

### 4. การรัน Service (ทีละ Service)
แยกหน้าต่าง Terminal สำหรับแต่ละ Service:

**รัน Ingestion Service:**
```bash
cd src/IngestionService
dotnet run
```

**รัน Processing Service:**
```bash
cd src/ProcessingService
dotnet run
```

**รัน Api Gateway:**
```bash
cd src/ApiGateway
dotnet run
```
*เข้าใช้งาน Swagger UI ได้ที่ `http://localhost:5xxx/swagger/index.html`*
