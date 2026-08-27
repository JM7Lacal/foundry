# 0010 — Theming: sistema de paleta con Light/Dark en runtime

**Estado:** aceptado · revisado post pasada de UX

## Contexto

Version original de este ADR: *"sin tema oscuro — necesita `ControlTemplate`s para cada control o
una libreria"*. Eso cambio: la pasada de UX (M1) ya reescribio los `ControlTemplate` de los
controles que Foundry usa (`Button`, `ToolBar`, `TabItem`, `TreeViewItem`, `ListBoxItem`) con una
paleta de brushes con nombre. Con eso, agregar un tema oscuro pasa de "semanas" a "una paleta mas".

Y un tema oscuro **bien hecho** es material de estudio y defensa para la entrevista:
`ResourceDictionary`, merge de diccionarios, `DynamicResource` vs `StaticResource`, y por que el
segundo no sirve para cambiar de tema en caliente.

## Decision

- `Themes/Controls.xaml` — los estilos y templates, **agnosticos de paleta**. Cada color se
  resuelve con `{DynamicResource XxxBrush}`.
- `Themes/Palette.Light.xaml` y `Themes/Palette.Dark.xaml` — solo `SolidColorBrush`, con las
  **mismas claves**. Dark es neutro tipo IDE (VS Code / Rider), sin negros puros.
- `App.xaml` mergea `Controls.xaml` + `Palette.Light.xaml`.
- `WpfThemeService` (`IThemeService` en Presentation) intercambia el diccionario de paleta en
  `Application.Current.Resources.MergedDictionaries` en runtime. Como todo referencia los brushes
  con `DynamicResource`, el cambio se ve al instante, sin reiniciar. La preferencia se persiste en
  `%APPDATA%\Foundry\theme.txt` y se aplica antes de mostrar la ventana.
- Toggle en la toolbar (sol/luna) y en el menu *Ver*. `MainViewModel.ToggleThemeCommand` habla
  con `IThemeService` — el VM no toca WPF.

## Consecuencias

- **+** Light y Dark completos, cambio en caliente. Demuestra el manejo de recursos/estilos de
  WPF de punta a punta.
- **+** Agregar un tercer tema = un `Palette.*.xaml` mas.
- **+** `IThemeService` es un puerto: el VM y sus tests no dependen de WPF.
- **−** Los popups de submenu del `Menu` y los `ScrollBar` siguen con templates del sistema; en
  oscuro se ven un poco mas claros que el resto. Re-templar `Menu`/`ScrollBar` es mucho XAML para
  poco valor; se acepta el compromiso.
- **−** Cada color debe referenciarse con `DynamicResource` sin excepcion; un `StaticResource`
  perdido no cambia al togglear.

## Alternativas consideradas

- **Libreria de UI** (`Wpf.Ui` / MahApps): da los dos temas gratis pero es una dependencia grande
  que contradice la linea del proyecto ([ADR 0002](0002-mvvm-community-toolkit.md)). Ademas se
  pierde el ejercicio de armar el sistema de temas a mano.
- **`ThemeDictionaries`** (el mecanismo nuevo de WPF/WinUI): mas declarativo pero con menos
  control y peor soporte en .NET 8 WPF que el swap manual de `MergedDictionaries`.
