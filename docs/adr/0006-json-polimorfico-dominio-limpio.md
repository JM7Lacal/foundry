# 0006 — JSON polimorfico sin ensuciar el dominio

**Estado:** aceptado · Dia 1

## Contexto

La base de contenido guarda una lista heterogenea de entidades (`Troop`, `Tower`, `Enemy`, ...)
en un unico archivo. Al deserializar hay que reconstruir el tipo concreto de cada objeto.

`System.Text.Json` soporta polimorfismo con atributos sobre el tipo base:

```csharp
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(Troop), "troop")]
...
public abstract class ContentEntity
```

Pero eso mete conocimiento de serializacion (y la lista de todos los subtipos) dentro de
`Foundry.Core`, que por diseño no debe saber que se persiste en JSON (ver
[ADR 0001](0001-clean-architecture-cuatro-proyectos.md)).

## Decision

Configurar el polimorfismo **en Infrastructure**, en runtime, con un modifier del
`DefaultJsonTypeInfoResolver` (`FoundryJsonOptions`):

- Cuando el resolver arma el contrato de `ContentEntity`, se le asigna un
  `JsonPolymorphismOptions` con discriminador `$type`.
- Los subtipos se **descubren por reflexion** sobre el ensamblado de `Foundry.Core`
  (`IsSubclassOf(typeof(ContentEntity))`).
- El discriminador es el nombre del tipo en minuscula (`Troop` → `"troop"`).
- `UnknownDerivedTypeHandling.FailSerialization` + `IgnoreUnrecognizedTypeDiscriminators = false`:
  un `$type` desconocido falla con error claro en vez de devolver null silenciosamente.
- `EntityId` se serializa como string via un `JsonConverter` propio, tambien registrado aca.

`Foundry.Core` queda sin una sola referencia a `System.Text.Json`.

## Consecuencias

- **+** El dominio queda limpio; la regla de dependencias se respeta de verdad.
- **+** Agregar una entidad nueva = agregar la clase. La serializacion la toma sola.
- **+** El formato del archivo (`$type`, camelCase, enums como texto) es legible para diseñadores.
- **−** Mas codigo que los atributos, y menos "descubrible" para quien lee `ContentEntity`.
- **−** La reflexion sobre el ensamblado corre una vez por proceso; se cachea en un campo estatico.
- **−** `.NET 8` exige que `$type` sea la **primera** propiedad de cada objeto en el JSON
  (`AllowOutOfOrderMetadataProperties` recien esta en .NET 9). El sample lo respeta.

## Alternativas consideradas

- **Atributos en `ContentEntity`**: lo mas simple, pero rompe la separacion de capas — el punto
  central del proyecto.
- **Capa de DTOs en Infrastructure** (un `TroopDto`, `TowerDto`, ... y mapeo manual): dominio
  limpio, pero mucho boilerplate y se duplica cada propiedad. Desproporcionado.
- **`JsonSerializerContext` con source generators**: mejor performance de arranque, pero tambien
  necesita atributos y no aporta a esta escala.
