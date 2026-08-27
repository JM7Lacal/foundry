# 0003 — Composicion con Generic Host + Microsoft.Extensions.DependencyInjection

**Estado:** aceptado · Dia 0

## Contexto

Los ViewModels y servicios necesitan armarse desde algun lado. Las opciones van desde `new`
manual hasta un contenedor de DI completo. El equipo (y el evaluador) vienen de backend .NET,
donde `Microsoft.Extensions.DependencyInjection` es el default.

## Decision

`App.xaml.cs` es el **composition root**. Construye un `IHost`
(`Host.CreateDefaultBuilder`) y registra los servicios por capa mediante metodos de extension:

```csharp
services.AddFoundryApplication();     // UndoStack, casos de uso
services.AddFoundryInfrastructure();  // repositorio JSON, importadores
services.AddSingleton<MainViewModel>();
services.AddSingleton<MainWindow>();
```

`MainWindow` recibe su `MainViewModel` por constructor. `App` controla el ciclo de vida:
`StartAsync` en `OnStartup`, `StopAsync` + `Dispose` en `OnExit`.

Cada capa expone su propio `Add<Capa>()` en el namespace `Microsoft.Extensions.DependencyInjection`
(convencion .NET) para que Infrastructure no filtre sus tipos concretos a App.

## Consecuencias

- **+** Mismo modelo mental que ASP.NET: lifetimes, `IServiceProvider`, `IOptions`, logging.
- **+** Los ViewModels declaran sus dependencias por constructor → faciles de testear.
- **+** El registro de cada capa esta encapsulado; App no conoce `JsonContentRepository`.
- **+** `IHost` trae logging y configuracion listos si despues hacen falta.
- **−** Overhead conceptual para quien espera el patron `ViewModelLocator` clasico de WPF.
- **−** Hay que tener cuidado con ViewModels `Singleton` que deberian ser `Transient`.

## Alternativas consideradas

- **`ViewModelLocator` + contenedor propio**: patron historico de WPF; hoy se siente anticuado.
- **Sin DI, `new` manual**: viable a esta escala pero no muestra la practica que el puesto pide
  explicitamente.
