# 0007 — ViewModels en un assembly sin WPF

**Estado:** aceptado · Dia 2

## Contexto

Los ViewModels empezaron dentro de `Foundry.App` (`net8.0-windows`, `UseWPF=true`). Para
testearlos hacia falta un proyecto de test tambien `net8.0-windows`, y el runner de tests para
ensamblados WPF choco con una politica de seguridad de la maquina de desarrollo (Smart App
Control bloquea DLLs sin firmar cargadas por el host de test).

Ademas, un ViewModel que solo usa `CommunityToolkit.Mvvm` (que es agnostico de UI, `netstandard2.0`)
no tiene ninguna razon tecnica para vivir en un proyecto WPF.

## Decision

Nuevo proyecto **`Foundry.Presentation`** (`net8.0`, sin `UseWPF`):

- Contiene todos los ViewModels (`MainViewModel`, `InspectorViewModel`, los
  `PropertyFieldViewModel`, ...) y la abstraccion `IFilePicker`.
- Depende de `Core`, `Application` y `CommunityToolkit.Mvvm`. **Cero referencias a WPF.**
- Expone `AddFoundryPresentation()` para registrar los ViewModels en el contenedor.

`Foundry.App` queda con lo estrictamente WPF: Views (XAML), converters, behaviors,
`WpfFilePicker` (`IFilePicker` con `Microsoft.Win32` dialogs) y el composition root.

Los tests de ViewModels van a `Foundry.Presentation.Tests` (`net8.0`), que corre con el runner
de consola normal.

## Consecuencias

- **+** Los ViewModels se testean sin WPF, sin STA thread, sin el bloqueo de Smart App Control.
- **+** Deja demostrado, con la estructura, que la logica de presentacion no depende de la UI
  ("¿por que los VM en otro proyecto?" tiene una respuesta concreta).
- **+** Si el dia de mañana se quisiera otra UI (Avalonia, MAUI), los ViewModels se reusan.
- **−** Un proyecto y un salto de assembly mas; los `xmlns` de XAML necesitan `;assembly=Foundry.Presentation`.
- **−** `IFilePicker` vive en Presentation pero su unica implementacion esta en App — hay que
  registrar esa implementacion desde el composition root.

## Nota relacionada: templates implicitos vs DataTemplateSelector

El Inspector tiene un tipo de ViewModel por clase de campo (`TextFieldViewModel`,
`ChoiceFieldViewModel`, ...). La eleccion del editor depende del **tipo**, no de un valor en
runtime, asi que se usan **`DataTemplate` implicitos** (`DataType="{x:Type ...}"`) y WPF resuelve
solo. Un `DataTemplateSelector` seria la herramienta correcta si el template dependiera del
*contenido* del dato (por ejemplo, mostrar un campo distinto segun el valor de otra propiedad).
Meter un selector donde alcanzan los templates implicitos es mas codigo y menos idiomatico.
