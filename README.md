# Foundry

Herramienta interna de escritorio (WPF / .NET 8) para editar y validar el **contenido de un
juego tower-defense / estrategia** — tropas, torres, oleadas de enemigos, arboles de mejora,
curvas de economia, eventos de live-ops — antes de exportarlo al formato que consume el juego.

Pensada como el tipo de tool que un equipo de diseño usa a diario para iterar balanceo sin
tocar el engine.

> Proyecto de portfolio para una entrevista de **Tools Engineer (C# / WPF)**. El objetivo no es
> la cantidad de features sino mostrar como un desarrollador senior estructura una herramienta
> interna: arquitectura, testabilidad y decisiones justificadas (ver [`docs/adr`](docs/adr)).

## Estado

| | |
|---|---|
| Build | `dotnet build` — OK |
| Tests | `dotnet test` — 18/18 |
| Fase | Dia 1 completo (dominio + persistencia + arbol). Ver [Roadmap](#roadmap). |

## Correr

```bash
dotnet build
dotnet test
dotnet run --project src/Foundry.App
```

Al arrancar carga un archivo de contenido de ejemplo
([`Samples/tower-defense.json`](src/Foundry.App/Samples/tower-defense.json)) para tener algo que
mostrar sin abrir un archivo a mano.

Requiere el **.NET 8 SDK**. Para desarrollo se recomienda Visual Studio 2026 Community con el
workload ".NET desktop development" (diseñador XAML, Live Visual Tree).

## Estructura de la solucion

```
Foundry.sln
├── src/
│   ├── Foundry.Core            Dominio puro: entidades de contenido, value objects, reglas.
│   │                           Sin WPF, sin JSON, sin file system. No referencia a nadie.
│   ├── Foundry.Application     Casos de uso + abstracciones (puertos): IContentRepository,
│   │                           UndoStack. Define QUE, no COMO.
│   ├── Foundry.Infrastructure  Implementaciones: repositorio JSON (polimorfico), disco.
│   └── Foundry.App             WPF: Views (XAML), ViewModels, composition root (App.xaml.cs).
└── tests/
    ├── Foundry.Core.Tests
    ├── Foundry.Application.Tests
    └── Foundry.Infrastructure.Tests
```

## Arquitectura

**Regla de dependencias — siempre hacia adentro:**

```
App  ─►  Infrastructure  ─►  Application  ─►  Core
                                              ▲
        Infrastructure ───────────────────────┘
```

`Core` no conoce a nadie, asi que la logica de dominio se testea sin instanciar una ventana ni
tocar el disco. `Application` define interfaces (`IContentRepository`) que `Infrastructure`
implementa: la capa de casos de uso no sabe que la persistencia es JSON. El limite se **fuerza
con los proyectos**: MSBuild no permite referencias circulares ni saltos de capa.

Decisiones clave, cada una con su ADR:

| # | Decision |
|---|---|
| [0001](docs/adr/0001-clean-architecture-cuatro-proyectos.md) | Clean architecture con 4 proyectos (Infrastructure separado) |
| [0002](docs/adr/0002-mvvm-community-toolkit.md) | MVVM con `CommunityToolkit.Mvvm` (source generators) |
| [0003](docs/adr/0003-composicion-generic-host-di.md) | Composicion con Generic Host + `Microsoft.Extensions.DependencyInjection` |
| [0004](docs/adr/0004-fluentassertions-7.md) | `FluentAssertions` fijado en 7.2.0 (8.x pasa a licencia paga) |
| [0005](docs/adr/0005-tooling-cpm-analyzers.md) | Central Package Management + analyzers + warnings como errores |
| [0006](docs/adr/0006-json-polimorfico-dominio-limpio.md) | JSON polimorfico sin ensuciar el dominio con atributos de serializacion |

## El nucleo: el Inspector (Dia 2)

El diferenciador tecnico del proyecto. En lugar de escribir un formulario por tipo de entidad,
las entidades se decoran con atributos:

```csharp
[EditableProperty(Label = "Daño", Group = "Combate", Order = 0)]
[Range(0, 9999)]
public int Damage { get; set; }

[EditableProperty(Label = "Mejora a", Group = "Progresion")]
[AssetReference(typeof(Troop))]
public EntityId? UpgradesInto { get; set; }
```

`InspectorViewModel` reflexiona sobre la entidad seleccionada y construye una lista de
`PropertyFieldViewModel` (uno por propiedad editable). Un `DataTemplateSelector` elige el control
segun el tipo: `TextBox`, `Slider`, `ComboBox` para enums, picker para referencias. Cada edicion
se encola como un `IUndoableAction` en el `UndoStack`.

**Agregar un tipo de entidad nuevo = escribir la clase y anotarla. Cero UI nueva.** Es el patron
del Inspector de Unity / el Details de Unreal.

## Roadmap

| Dia | Entregable |
|---|---|
| **0** ✅ | Solucion, 4 proyectos + tests, DI/host, ventana shell, build+test verde, tooling |
| **1** ✅ | Entidades (`Troop`/`Tower`/`Enemy`) + atributos de edicion · `ContentDatabase` · repositorio JSON async y polimorfico · `TreeView` de contenido con seleccion → Inspector |
| **2** | Inspector por reflexion · `DataTemplateSelector` por tipo de campo · preview JSON en vivo |
| **3** | Undo/redo sobre todas las ediciones · validacion `INotifyDataErrorInfo` · dirty tracking · atajos |
| **4** | Tema oscuro · busqueda en el arbol · `IContentImporter` (CSV) · "find usages" de referencias · ADRs finales |

## Fuera de alcance (a proposito)

- **Sin editor de grafo** (dialogos / skill trees con nodos): mucho tiempo en rendering custom,
  poco en la historia de ingenieria.
- **Sin librería de UI de terceros**: el tema se hace con `ResourceDictionary` para mostrar
  dominio de recursos y estilos.
- **Sin integracion con control de versiones ni pipeline de build real**: se menciona como
  extension, no se implementa.

## Testing

`xUnit` + `FluentAssertions`. Se testea la logica que vive en `Core` y `Application`
(value objects, `UndoStack`, mas adelante reglas de validacion y el armado del inspector).
Los ViewModels son testeables por construccion — es media de la razon de ser de MVVM.
