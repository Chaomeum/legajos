# Legajos
## Auditoría Documental – Azure SaaS POC

Este proyecto implementa un **pipeline serverless en Azure** para la **revisión y validación automatizada de documentación regulatoria**
(SBS, SMV y políticas internas), orientado a procesos de auditoría.

El sistema permite:
- Ingestar documentos en Azure Blob Storage
- Clasificarlos y extraer información usando Azure AI Document Intelligence
- Validar la información extraída contra criterios regulatorios configurables
- Generar un **Excel final de auditoría** con resumen de cumplimiento y evidencia documental

---

## 1. Arquitectura general

El flujo lógico del sistema es el siguiente:

```
Blob Storage (input)
↓ Event Grid
Logic Apps (orquestación)
↓
IntakeFunction (clasificación + extracción)
↓
ValidationFunction (motor de validación regulatoria)
↓
ExportExcelFunction (generación de Excel final)
↓
Blob Storage (output)
```

Cada componente tiene **una sola responsabilidad**, lo que permite un desacoplamiento claro y escalabilidad futura.

---

## 2. Componentes principales

### 2.1 IntakeFunction

**Responsabilidad**
- Procesar **un documento individual**
- Clasificar el documento (classifier de Document Intelligence)
- Ejecutar el extractor correspondiente
- Normalizar el resultado en un `CanonicalExtractionResult`

**No hace**
- Validación regulatoria
- Identificación de muestras o casos
- Generación de Excel

**Salida**
- Resultado canónico del documento (estructura uniforme)
- Metadata del documento procesado

---

### 2.2 ValidationFunction

**Responsabilidad**
- Validar una **muestra** (conjunto de documentos) contra criterios regulatorios
- Consumir:
  - Resultados de extracción
  - Catálogo de criterios (Table Storage)

**Validaciones implementadas (Fase 1)**
- Existencia de documento (`DocumentExists`)
- Existencia de campos obligatorios (`RequiredFields`)
- No aplicable (`NA`)

**Salida**
- Lista de `ValidationResult`:
  - CriterionId
  - Description
  - Status (OK / NOK / NA)

---

### 2.3 ExportExcelFunction

**Responsabilidad**
- Generar el **Excel final de auditoría**
- Trabajar por **CaseId**, no por documento
- Soportar **múltiples muestras** (columnas dinámicas)

**Estructura del Excel**
- Hoja `Resumen`:
  - Filas = criterios verificables
  - Columnas = Muestras (Muestra 1, Muestra 2, …)
- Hojas adicionales por tipo documental (`DNI`, `CV`, etc.)
  - Campos como filas
  - Muestras como columnas

El Excel es **presentación pura**, no contiene lógica de validación.

---

## 3. Organización del proyecto

```
AF/
├─ Functions/
│  ├─ Intake/
│  │  └─ IntakeFunction.cs
│  ├─ Validation/
│  │  └─ ValidationFunction.cs
│  └─ Export/
│     └─ ExportExcelFunction.cs
│
├─ Domain/
│  ├─ Documents/        # Modelos documentales y adapters
│  ├─ Validation/       # Resultados de validación
│  └─ Export/           # Modelo de exportación (samples)
│
├─ Infrastructure/
│  ├─ Storage/          # Entidades Table Storage
│  └─ Requests/         # DTOs de entrada a funciones
│
├─ host.json
├─ AF.csproj
```

---

## 4. Catálogo de criterios regulatorios

Los criterios de validación se almacenan en **Azure Table Storage** (`ValidationCriteria`).

La clave de partición sigue el esquema:

{Regulation}|{PersonType}|{PersonSubType}|{Regime}

Ejemplos:
- `SBS|Clientes|PN|Estandar`
- `SMV|Clientes|PJ|Estandar`

Esto permite soportar múltiples regulaciones sin cambios de código.

---

## 5. Estado actual del producto

✅ Clasificación y extracción funcional  
✅ Motor de validación Fase 1  
✅ Excel final con:
- resumen de cumplimiento
- evidencia documental
- soporte multi‑muestra  

🚧 Pendiente para fases futuras:
- Validaciones complejas (PEP, OFAC, listas externas)
- Persistencia de muestras por CaseId
- Append incremental lógico (no edición de Excel)
- Power BI / reporting avanzado

---

## 6. Principios de diseño

- Event‑driven
- Pay‑As‑You‑Go
- Serverless first
- Desacoplamiento por responsabilidad
- Auditoría y trazabilidad sobre automatización ciega