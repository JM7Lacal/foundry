# 0009 — Importadores de contenido

**Estado:** aceptado · Dia 4

## Contexto

Los diseñadores de un F2P balancean en planillas. La herramienta tiene que poder **traer** ese
contenido (y mañana, otros formatos: el export del engine, un formato propio). No queremos un
importador por tipo de entidad ni tocar codigo cada vez que se agrega una entidad.

## Decision

- Puerto `IContentImporter` en Application: `FormatName`, `FileFilter`, `Import(path) -> lista de
  entidades`. El editor solo conoce esta abstraccion.
- `CsvContentImporter` en Infrastructure. CSV con cabecera; columnas obligatorias `type`
  (discriminador) e `id`. El resto de las columnas se mapean a propiedades editables **por nombre
  o por etiqueta** (sin distinguir mayusculas), reusando el `EditableSchema`. La conversion de
  texto a tipo (`int`, `double`, `enum`, `bool`, `EntityId`) usa `EditableField.ValueType`.
- Los tipos de entidad se resuelven con `ContentEntityCatalog` (Core), que descubre por reflexion
  los subtipos de `ContentEntity` y les da un discriminador. La misma pieza que usa la
  serializacion JSON — una sola fuente de verdad.
- La importacion **se combina** con la base actual (reemplaza por id) y marca "sin guardar".
  No es undoable: se trata como una carga masiva y limpia el historial (igual que abrir un
  archivo). Los errores (`ContentImportException`) traen numero de linea y columna.

## Consecuencias

- **+** Agregar un formato = una clase que implementa `IContentImporter`.
- **+** Agregar una entidad nueva no toca el importador CSV: sus columnas salen del schema.
- **+** El parser de CSV maneja comillas y comas dentro de campos (RFC 4180 basico).
- **−** No soporta saltos de linea dentro de un campo entrecomillado (poco comun en planillas de
  balanceo; documentado).
- **−** La importacion no es reversible con Ctrl+Z. Compromiso consciente por simplicidad;
  modelarla como `IUndoableAction` de lote es trabajo futuro.

## Alternativas consideradas

- **Un DTO/mapeo por entidad**: rigido y crece con cada tipo nuevo.
- **Una libreria de CSV** (CsvHelper): buena, pero agrega dependencia para un parser de ~60
  lineas cuyo comportamiento queremos controlar y explicar.
- **Importar via Google Sheets API**: mas cerca del flujo real, pero mete OAuth y red — fuera de
  alcance para una pieza de portfolio.
