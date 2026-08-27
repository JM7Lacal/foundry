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
| Tests | `dotnet test` — 46/46 |
| Fase | Dia 3 completo (undo/redo + validacion + dirty tracking). Ver [Roadmap](#roadmap). |

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
│   ├── Foundry.Core            Dominio puro: entidades, value objects, EditableSchema (reflexion).
│   │                           Sin WPF, sin JSON, sin file system. No referencia a nadie.
│   ├── Foundry.Application     Casos de uso + abstracciones (puertos): IContentRepository,
│   │                           IContentSerializer, UndoStack. Define QUE, no COMO.
│   ├── Foundry.Infrastructure  Implementaciones: repositorio y serializador JSON (polimorficos).
│   ├── Foundry.Presentation    ViewModels + IFilePicker. SIN dependencia de WPF.
│   └── Foundry.App             WPF: Views (XAML), converters, behaviors, WpfFilePicker,
│                               composition root (App.xaml.cs).
└── tests/
    ├── Foundry.Core.Tests
    ├── Foundry.Application.Tests
    ├── Foundry.Infrastructure.Tests
    └── Foundry.Presentation.Tests   ViewModels, sin runner de WPF
```

## Arquitectura

**Regla de dependencias — siempre hacia adentro:**

```
App  ─►  Presentation  ─►  Application  ─►  Core
  │            │                            ▲
  └─► Infrastructure ──────────────────────┘
```

`Core` no conoce a nadie, asi que la logica de dominio se testea sin instanciar una ventana ni
tocar el disco. `Application` define interfaces (`IContentRepository`) que `Infrastructure`
implementa: la capa de casos de uso no sabe que la persistencia es JSON. Los **ViewModels viven
en `Foundry.Presentation`, sin referencia a WPF** — se testean con un runner de consola normal
(ver [ADR 0007](docs/adr/0007-viewmodels-sin-wpf.md)). El limite se **fuerza con los proyectos**:
MSBuild no permite referencias circulares ni saltos de capa.

Decisiones clave, cada una con su ADR:

| # | Decision |
|---|---|
| [0001](docs/adr/0001-clean-architecture-cuatro-proyectos.md) | Clean architecture con 4 proyectos (Infrastructure separado) |
| [0002](docs/adr/0002-mvvm-community-toolkit.md) | MVVM con `CommunityToolkit.Mvvm` (source generators) |
| [0003](docs/adr/0003-composicion-generic-host-di.md) | Composicion con Generic Host + `Microsoft.Extensions.DependencyInjection` |
| [0004](docs/adr/0004-fluentassertions-7.md) | `FluentAssertions` fijado en 7.2.0 (8.x pasa a licencia paga) |
| [0005](docs/adr/0005-tooling-cpm-analyzers.md) | Central Package Management + analyzers + warnings como errores |
| [0006](docs/adr/0006-json-polimorfico-dominio-limpio.md) | JSON polimorfico sin ensuciar el dominio con atributos de serializacion |
| [0007](docs/adr/0007-viewmodels-sin-wpf.md) | ViewModels en un assembly sin WPF; templates implicitos vs `DataTemplateSelector` |
| [0008](docs/adr/0008-undo-redo-y-validacion.md) | Undo/redo (command pattern) y validacion en dos capas (`INotifyDataErrorInfo` + `ContentValidator`) |

## El nucleo: el Inspector

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

`EditableSchema.For(type)` (en `Core`) reflexiona una vez por tipo y produce una lista de
`EditableField` con etiqueta, grupo, orden, rango y `FieldKind`. `InspectorViewModel` la recorre
y crea un `PropertyFieldViewModel` por campo (`TextField`, `WholeNumberField`, `ChoiceField`,
`ReferenceField`, ...). Cada subtipo tiene su `DataTemplate` implicito en `InspectorView.xaml`:
`TextBox`, `Slider` + `TextBox`, `ComboBox` de enum, selector de referencias. El getter lee
siempre de la entidad; el setter aplica el cambio por un callback (Dia 3: pasa por el `UndoStack`).

**Agregar un tipo de entidad nuevo = escribir la clase y anotarla. Cero UI nueva.** Es el patron
del Inspector de Unity / el Details de Unreal.

## Roadmap

| Dia | Entregable |
|---|---|
| **0** ✅ | Solucion, 4 proyectos + tests, DI/host, ventana shell, build+test verde, tooling |
| **1** ✅ | Entidades (`Troop`/`Tower`/`Enemy`) + atributos de edicion · `ContentDatabase` · repositorio JSON async y polimorfico · `TreeView` de contenido con seleccion → Inspector |
| **2** ✅ | `EditableSchema` por reflexion · Inspector con un `DataTemplate` por `FieldKind` · selector de referencias · preview JSON en vivo · ViewModels movidos a `Foundry.Presentation` (sin WPF) |
| **3** ✅ | Undo/redo (`SetFieldValueAction`) en todas las ediciones · Ctrl+Z/Y + menu · validacion por campo (`INotifyDataErrorInfo`) y de la base completa (`ContentValidator`) · dirty tracking (`*` en el titulo) · prompt de cambios sin guardar al abrir/cerrar |
| **4** | Tema oscuro · busqueda en el arbol · `IContentImporter` (CSV) · "find usages" de referencias · ADRs finales |

## Fuera de alcance (a proposito)

- **Sin editor de grafo** (dialogos / skill trees con nodos): mucho tiempo en rendering custom,
  poco en la historia de ingenieria.
- **Sin librería de UI de terceros**: el tema se hace con `ResourceDictionary` para mostrar
  dominio de recursos y estilos.
- **Sin `DataTemplateSelector`**: los templates de campo se eligen por tipo de ViewModel, no por
  un valor en runtime — para eso los templates implicitos (`DataType=`) son la herramienta
  correcta. Un selector recien haria falta si el template dependiera del *contenido*.
- **Sin integracion con control de versiones ni pipeline de build real**: se menciona como
  extension, no se implementa.

## Testing

`xUnit` + `FluentAssertions`. 46 tests:

- **Core** — `EntityId`, `ContentDatabase`, `EditableSchema` (inferencia de `FieldKind`, rango,
  referencias, cache).
- **Application** — `UndoStack`, `SetFieldValueAction` (undo/redo), `ContentValidator`
  (rango / requerido / referencia rota).
- **Infrastructure** — round-trip JSON, polimorfismo `$type`, tipo desconocido falla, formato.
- **Presentation** — `InspectorViewModel` (arma grupos/campos, editar pasa por el undo stack,
  errores de validacion) y `MainViewModel` (arbol, seleccion → inspector + preview, dirty,
  save bloqueado con datos invalidos, undo/redo). Sin runner de WPF gracias al split de assemblies.
