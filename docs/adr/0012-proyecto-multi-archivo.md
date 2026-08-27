# 0012 — Un archivo por ahora; concepto de proyecto multi-archivo pendiente

**Estado:** propuesto (no implementado) · post Dia 4

## Contexto

Foundry edita **un archivo JSON** a la vez = el contenido de un juego. Preguntas que aparecen al
usarlo con mas de un juego:

- ¿Como manejo 2, 5, 10 juegos? ¿Se mezcla todo?
- El contenido de un juego real no suele estar en un solo archivo (`troops.json`, `waves.json`,
  `liveops.json`, ...).

## Decision

**Por ahora se deja como esta: un archivo abierto.** Varios juegos = varios archivos, se cambia
con *Archivo → Abrir*. No se mezclan (el arbol muestra solo lo del archivo abierto).

Se documenta el camino para escalar, en dos pasos:

1. **Lista de recientes** (chico): menu *Archivo → Recientes*, persistido en
   `%APPDATA%\Foundry\recent.json`. Cubre bien 2-10 juegos de un archivo cada uno.

2. **Concepto de proyecto** (grande): un `game.foundryproj` con `{ name, contentFiles[],
   assistant }`. Abrir el proyecto carga todos sus archivos; el arbol los agrupa; guardar escribe
   cada entidad **al archivo del que vino** (o a uno por defecto para las nuevas). Un selector de
   proyectos recientes para saltar entre juegos (tipo Unity Hub).

## Por que no se hace ahora

- El costo real no es "soportar varios juegos" — es **multi-archivo por juego**. `MainViewModel`
  asume "un `_database`, un `CurrentFilePath`". Habria que introducir un `ContentDocument` que
  sepa el origen de cada entidad, y cambiar la logica de guardado.
- Para 2-3 juegos, la lista de recientes ya alcanza, y es 1/20 del trabajo.
- Es scope que no agrega a la tesis del proyecto (arquitectura + Inspector reflexivo).

## Consecuencias

- **+** El modelo actual es simple y no bloquea nada: cada juego es un archivo autonomo.
- **−** Cambiar de juego hoy es *Archivo → Abrir* y navegar el dialogo — sin lista de recientes
  todavia.
- **−** No hay forma de tener el contenido de un juego partido en varios archivos.

## Alternativas consideradas

- **Todo en un archivo gigante con un campo `game` por entidad + un filtro**: mezcla los juegos
  en el mismo archivo, complica el control de versiones y el pipeline. Descartado.
- **Una instancia de la app por juego**: funciona pero es incomodo y no resuelve el multi-archivo.
