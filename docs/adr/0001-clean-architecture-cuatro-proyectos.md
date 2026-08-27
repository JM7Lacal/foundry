# 0001 — Clean architecture con cuatro proyectos

**Estado:** aceptado · Dia 0

## Contexto

Foundry es una herramienta chica pero tiene que verse production-ready y ser facil de testear.
El riesgo tipico en un tool WPF es que la logica (parseo, validacion, reglas de balanceo) quede
enredada con el codigo de UI y termine no teniendo tests.

## Decision

Separar en cuatro proyectos con la regla de dependencias apuntando hacia adentro:

- **Foundry.Core** — dominio puro. Sin dependencias (ni WPF, ni serializacion, ni IO).
- **Foundry.Application** — casos de uso y puertos (`IContentRepository`, `IContentImporter`,
  `UndoStack`). Solo depende de Core.
- **Foundry.Infrastructure** — adaptadores: repositorio JSON, importadores, acceso a disco.
  Depende de Application.
- **Foundry.App** — WPF. Unico proyecto que referencia a todos; contiene el composition root.

El limite entre capas se apoya en el sistema de proyectos: MSBuild rechaza referencias
circulares, y como Core no referencia nada es *imposible* filtrar detalles de UI o de
persistencia dentro del dominio.

## Consecuencias

- **+** La logica de Core y Application se testea sin `Application` de WPF ni archivos.
- **+** Cambiar el formato de persistencia (JSON → SQLite) toca solo Infrastructure.
- **+** Es un punto de conversacion claro en la entrevista.
- **−** Mas proyectos y algo de ceremonia para un tool de este tamaño.
- **−** Hay que resistir la tentacion de referenciar Infrastructure desde Application "por
  comodidad".

## Alternativas consideradas

- **Tres proyectos** (Infrastructure fusionado en Application): menos archivos, pero se pierde
  la garantia de que la capa de casos de uso no conoce el formato de persistencia.
- **Un proyecto** (todo en Foundry.App): mas rapido de arrancar, pero la logica queda atada a
  WPF y los tests se vuelven dificiles. Descartado.
