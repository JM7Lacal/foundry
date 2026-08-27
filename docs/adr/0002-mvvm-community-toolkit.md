# 0002 — MVVM con CommunityToolkit.Mvvm

**Estado:** aceptado · Dia 0

## Contexto

WPF necesita un patron de separacion entre XAML y logica de presentacion. MVVM es el estandar.
La parte repetitiva es implementar `INotifyPropertyChanged` y `ICommand` a mano en cada
ViewModel.

## Decision

Usar **`CommunityToolkit.Mvvm`** (el sucesor de MvvmLight, mantenido por Microsoft):

- `[ObservableProperty]` genera la propiedad con notificacion a partir de un campo.
- `[RelayCommand]` genera un `ICommand` a partir de un metodo, con `CanExecute` opcional.
- Son **source generators**: el codigo se genera en compilacion, sin reflexion ni costo en
  runtime.

## Consecuencias

- **+** ViewModels cortos y legibles; menos superficie para bugs de boilerplate.
- **+** Es la librería que la mayoria de los equipos WPF usa hoy — señal de estar al dia.
- **+** No impone framework de aplicacion: se puede combinar con cualquier estrategia de
  navegacion o DI.
- **−** El ViewModel tiene que ser `partial` y los campos siguen una convencion de nombre.
- **−** Un lector que no conoce la librería tiene que saber que la propiedad `Status` sale del
  campo `_status`.

## Alternativas consideradas

- **`INotifyPropertyChanged` a mano / clase base propia**: mas explicito, cero dependencias.
  Vale saber hacerlo (y hay que poder explicarlo en la entrevista), pero reimplementarlo no
  aporta valor al proyecto.
- **Prism / Caliburn.Micro**: frameworks completos. Demasiado peso y opinion para un tool de
  este tamaño.
