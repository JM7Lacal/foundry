# 0010 — Theming: pasada de diseño clara, sin tema oscuro

**Estado:** aceptado · Dia 4

## Contexto

El plan original mencionaba un tema oscuro. Un tema oscuro **completo** en WPF exige reescribir
el `ControlTemplate` de casi todos los controles (los templates por defecto tienen colores
horneados que un simple cambio de brush no alcanza a cubrir), o adoptar una libreria de UI.

## Decision

- **No** hacer tema oscuro. Hacer una **pasada de diseño sobre el tema claro**:
  `Themes/Foundry.xaml` (merged en `App.xaml`) con una paleta con nombre (`AccentBrush`,
  `SurfaceBrush`, `WindowBrush`, `BorderBrush`, `DangerBrush`, `MutedBrush`), tipografia
  (`Segoe UI`, `TextFormattingMode=Display`) y un `GroupBox` mas sobrio (header en gris medio,
  semibold).
- Los estilos son acotados y sin `ControlTemplate`: cambian fondo/tipografia, no la mecanica de
  los controles. Bajo riesgo de romper nada.

## Consecuencias

- **+** La app se ve intencional y coherente, no "WPF crudo".
- **+** La paleta con nombre deja el tema oscuro como trabajo futuro acotado (redefinir los
  brushes en un `Dark.xaml` y togglear el `MergedDictionary`).
- **−** No hay modo oscuro. Para tooling interno que se mira todo el dia, es una carencia real;
  se asume a cambio de no enviar un tema oscuro a medias.

## Alternativas consideradas

- **Libreria de UI** (`Wpf.Ui` / MahApps / Fluent): da tema oscuro y claro completos, pero
  agrega una dependencia grande que habria que justificar (ver
  [ADR 0002](0002-mvvm-community-toolkit.md), misma linea de "sin frameworks pesados"). Para
  una pieza de portfolio pesa mas mostrar dominio de `ResourceDictionary` y `Style`.
- **Tema oscuro a mano**: semanas de `ControlTemplate`. Desproporcionado.
