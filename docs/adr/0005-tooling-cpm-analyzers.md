# 0005 — Central Package Management, analyzers y warnings como errores

**Estado:** aceptado · Dia 0

## Contexto

En una solucion multi-proyecto es facil que las versiones de NuGet se desincronicen entre
proyectos y que el estilo/calidad del codigo derive con el tiempo.

## Decision

- **Central Package Management**: todas las versiones en `Directory.Packages.props`; los
  `.csproj` referencian paquetes sin `Version=`. Las versiones de `Microsoft.Extensions.*` se
  alinean con el runtime (.NET 8).
- **`Directory.Build.props`**: ajustes compartidos — `Nullable`, `ImplicitUsings`,
  `LangVersion=latest`, `GenerateDocumentationFile`, `TreatWarningsAsErrors=true`,
  `EnableNETAnalyzers` con `AnalysisLevel=latest-recommended`.
- **`.editorconfig`**: estilo (usings fuera del namespace, `namespace` file-scoped, campos
  privados con `_`) y ajuste puntual de reglas:
  - `CA1711` desactivada — nombres como `UndoStack` son mas claros que la alternativa.
  - `CA1707` desactivada solo en `tests/` — nombres de test con guiones bajos son idiomaticos.
- El TargetFramework NO va en `Directory.Build.props`: `Foundry.App` necesita `net8.0-windows`
  y el resto `net8.0`.

## Consecuencias

- **+** Una sola fuente de verdad para versiones; upgrades en un solo lugar.
- **+** El compilador frena estilo inconsistente y bugs sutiles (nullability) antes del review.
- **+** Onboarding: el `.editorconfig` documenta la convencion, no hace falta un wiki.
- **−** `TreatWarningsAsErrors` puede frenar el build por un analyzer nuevo tras un upgrade de
  SDK; se asume el costo a cambio de la disciplina.
- **−** Los proyectos de test relajan `TreatWarningsAsErrors` para no pelear con patrones
  propios de testing.

## Alternativas consideradas

- **Versiones por `.csproj`**: lo default; se descarta por la desincronizacion inevitable.
- **`Directory.Build.props` sin analyzers**: se pierde la mitad del valor (la red de seguridad
  de calidad).
