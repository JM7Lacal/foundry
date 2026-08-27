# 0004 — FluentAssertions fijado en 7.2.0

**Estado:** aceptado · Dia 0

## Contexto

`FluentAssertions` da asserts legibles (`result.Should().Be(...)`). A partir de la **version 8**
cambio su licencia a una comercial paga (Xceed): el uso en un contexto comercial requiere
comprar licencia. `dotnet add package` trae la 8.x por defecto.

## Decision

Fijar `FluentAssertions` en **7.2.0** — la ultima version bajo licencia libre — via Central
Package Management (`Directory.Packages.props`).

## Consecuencias

- **+** Sintaxis de asserts que ya conocemos, sin costo de licencia ni riesgo legal.
- **+** Deja documentado que el tema de licencias se tuvo en cuenta (buena señal en review).
- **−** No recibimos features ni fixes de la linea 8.x.
- **−** Hay que mantener el pin y avisar al equipo por que no se actualiza.

## Alternativas consideradas

- **AwesomeAssertions**: fork libre y drop-in de FluentAssertions creado justamente por el
  cambio de licencia. Opcion valida; se prefirio quedarse en la 7.x oficial por ahora para no
  introducir un fork poco conocido.
- **Shouldly**: buena librería, pero API distinta y migrar no aporta.
- **Asserts nativos de xUnit**: cero dependencias; se descarto por legibilidad en asserts
  compuestos.
