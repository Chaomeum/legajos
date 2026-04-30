# Legajos
## Auditoría Documental – Azure SaaS POC

**Legajos** implementa un **pipeline serverless en Azure** para la **revisión y validación automatizada de documentación regulatoria**
(SBS, SMV y políticas internas), orientado a procesos de auditoría y cumplimiento normativo.

El sistema permite:
- Ingestar documentos en Azure Blob Storage
- Clasificarlos y extraer información usando Azure AI Document Intelligence
- Validar la información extraída contra criterios regulatorios configurables
- Generar un **Excel final de auditoría** con:
  - resumen de cumplimiento
  - evidencia documental por muestra
  - soporte multi‑persona (multi‑muestra)

---

## 1. Arquitectura general

El sistema sigue un modelo **event‑driven e incremental**:  
cada documento genera un evento independiente y el estado se consolida progresivamente.

```

Blob Storage (input)
↓ Event Grid
Logic Apps (orquestación)
↓
IntakeFunction (clasificación + extracción)
↓
ValidationFunction (validación por muestra)
↓
ExportExcelFunction (consolidación por caso)
↓
Blob Storage (output)

```

### Principio clave
- **La validación ocurre por muestra**
- **El Excel se genera por caso**
- **Solo la última ejecución del caso genera el Excel**

---

## 2. Modelo conceptual

### 2.1 Case
Un **Case** representa una ejecución completa de auditoría.

- Identificado por `CaseId`
- Puede contener múltiples muestras
- Produce **un único Excel final**

---

### 2.2 Sample (Muestra)
Una **Sample** representa una persona o entidad auditada dentro del Case.

- Identificada por `SampleId` (GUID)
- Puede tener múltiples documentos
- Se valida de forma independiente
- Su estado evoluciona:

```
Partial → Ready → Validated
```

---

### 2.3 Documento
Cada documento:
- Se procesa de forma individual
- Produce un resultado canónico (`CanonicalExtractionResult`)
- Contribuye al estado de la muestra

---

## 3. Componentes principales

### 3.1 IntakeFunction

**Responsabilidad**
- Procesar **un documento individual**
- Clasificar el documento (classifier de Document Intelligence)
- Ejecutar el extractor correspondiente
- Normalizar el resultado en un `CanonicalExtractionResult`

**No hace**
- Validación regulatoria
- Coordinación de muestras o casos
- Generación de Excel

---

### 3.2 ValidationFunction

**Responsabilidad**
- Validar una **muestra completa** contra criterios regulatorios
- Consumir:
- resultados de extracción (`SampleDocuments`)
- catálogo de criterios (`ValidationCriteria`)

**Tipos de validación (Fase 1)**
- `DocumentExists`
- `RequiredFields`
- `NA`

**Notas de diseño**
- Los criterios pueden ser **específicos de documento** o **transversales**
- Los criterios transversales aun no se encuentran implementados pero se planea modelarlos segun `DocumentType = Default`y evalúan campos en múltiples documentos de la muestra

**Salida**
- Persistencia de resultados en `ValidationResults`
- Cada resultado contiene:
- CriterionId
- Status (OK / NOK / NA)
- Evidence / Observation

---

### 3.3 ExportExcelFunction

**Responsabilidad**
- Generar el **Excel final de auditoría**
- Trabajar por **CaseId**
- Consolidar múltiples muestras de forma coherente

**Características clave**
- Reconstruye los datos desde Table Storage:
- `SampleDocuments`
- `ValidationResults`
- `ValidationCriteria`
- **No depende del payload original**
- El Excel es **presentación pura**

---

## 4. Estructura del Excel

### 4.1 Hoja `Resumen`

- Filas: **criterios regulatorios** (`ValidationCriteria.Description`)
- Columnas: **Muestras** (`Muestra 1`, `Muestra 2`, …)
- Celdas:
- ✅ OK
- ❌ NOK
- NA No aplica

> La numeración de muestras es **determinística** y se basa en un orden canónico por `SampleId`
> para garantizar coherencia entre todas las hojas.

---

### 4.2 Hojas por tipo documental (`DNI`, `CUL`, etc.)

- Filas: campos extraídos
- Columnas: muestras
- Los valores complejos (arrays / JSON) que sean distintos a string se deben formatean para legibilidad

---

## 5. Persistencia (Azure Table Storage)

### 5.1 SampleDocuments
- Hechos documentales
- Resultado canónico de cada documento

### 5.2 ValidationCriteria
- Catálogo declarativo de reglas regulatorias
- PartitionKey:

```
{Regulation}|{PersonType}|{PersonSubType}|{Regime}
```

Ejemplo:

```
SBS|Clientes|PN|Estandar
```

### 5.3 ValidationResults
- Resultado de aplicar criterios a una muestra
- Fuente de verdad para el Excel

---

## 6. Estado actual del producto

✅ Pipeline completo funcional (end‑to‑end)  
✅ Validación por muestra  
✅ Excel final por caso  
✅ Soporte multi‑muestra  
✅ Diseño incremental y reproducible  

---

## 7. Mejoras planificadas (TODO)

### Prioridad alta
- Soporte robusto para criterios transversales (multi‑documento)
- Mejor formateo de campos complejos (ej. experiencia laboral)

### Prioridad media
- Validación de consistencia de identidad entre documentos
- Introducción de estado `WARN` (⚠️) para datos sospechosos pero no inválidos

### Prioridad baja
- Hoja técnica de trazabilidad (SampleId ↔ Muestra N)
- Versionado visible de criterios regulatorios en el Excel

---

## 8. Principios de diseño

- Event‑driven
- Serverless first
- Pay‑as‑you‑go
- Separación estricta de responsabilidades
- Auditoría y trazabilidad sobre automatización ciega
- Reproducibilidad antes que conveniencia
