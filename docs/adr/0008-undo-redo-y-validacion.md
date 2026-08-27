# 0008 — Undo/redo y validacion

**Estado:** aceptado · Dia 3

## Contexto

Una herramienta de edicion de contenido tiene dos requisitos no negociables: poder **deshacer**
cualquier cambio (los diseñadores tocan numeros todo el dia) y **avisar de datos invalidos**
antes de exportar al juego (un costo negativo o una referencia rota rompen el build del juego).

## Decision

### Undo/redo — patron Command

- `IUndoableAction` (`Apply` / `Revert` / `Description`) + `UndoStack` (pilas de undo y redo,
  evento `Changed`). Ya existian desde el Dia 0.
- Toda edicion del Inspector se modela como `SetFieldValueAction`: captura entidad, campo, valor
  viejo y valor nuevo. El `PropertyFieldViewModel` no muta la entidad: llama al callback que le
  pasa el `InspectorViewModel`, que hace `undoStack.Execute(new SetFieldValueAction(...))`.
- El getter de cada campo **siempre lee de la entidad**, asi que tras un undo/redo alcanza con
  refrescar los bindings (`RefreshFromModel`) para que la UI muestre el valor revertido.
- **Coalescing**: `IUndoableAction.TryCoalesceWith` deja que una accion absorba a la siguiente si
  son la misma operacion en rafaga. `SetFieldValueAction` fusiona ediciones al mismo campo de la
  misma entidad dentro de una ventana de 700 ms (arrastrar un deslizador = un solo paso de undo,
  no cincuenta). Los sliders ademas usan `Binding Delay=200` para no escribir en cada pixel.
- `MainViewModel` expone `UndoCommand` / `RedoCommand` (Ctrl+Z / Ctrl+Y, menu Editar), con
  `CanExecute` atado a `UndoStack.CanUndo/CanRedo`.

### Validacion — dos capas

1. **Por campo, en vivo** (`INotifyDataErrorInfo` en `PropertyFieldViewModel`): rango
   (`[Range]`), requerido (`[Required]`), referencia rota. El binding usa
   `ValidatesOnNotifyDataErrors=True` → borde rojo + tooltip + texto de error bajo el campo.
2. **De la base completa, al guardar** (`ContentValidator` en Application): recorre el
   `EditableSchema` de cada entidad y aplica una **lista de reglas**
   (`CheckRequired`, `CheckRange`, `CheckReference`). Devuelve `ValidationIssue`s. Si hay
   problemas, `SaveAsync` **no guarda** y los lista.

Las reglas son una lista justamente para que la idea futura de "coherencia de cadenas de mejora"
(una entidad no puede superar en stats a la que mejora) sea una regla mas, sin tocar el motor.

### Dirty tracking

`MainViewModel.IsDirty` → `true` en cualquier cambio del `UndoStack`, `false` al guardar o
cargar. Se ve como `*` en el titulo. Prompt de confirmacion al abrir otro archivo
(`IDialogService`) y al cerrar la ventana (`MainWindow.OnClosing`).

## Consecuencias

- **+** Undo/redo real sobre cualquier edicion, con costo casi nulo (un tipo de accion).
- **+** El feedback de validacion es inmediato y ademas hay un chequeo global antes de exportar.
- **+** `ContentValidator` es testeable en aislamiento y extensible por reglas.
- **−** El chequeo por campo solo cubre la entidad abierta en el Inspector; el chequeo global
  cubre todo pero recien al guardar. Es un compromiso consciente (no se instancian VMs para
  las 500 entidades).
- **−** `IsDirty` no vuelve a `false` si el usuario deshace manualmente hasta el estado guardado
  (se marcaria "limpio" solo tras guardar). Simplificacion aceptada.
- **−** `MainWindow.OnClosing` y el prompt de cierre viven en el code-behind: el ciclo de vida
  de la ventana no es bindeable y es el caso canonico de code-behind aceptable.

## Alternativas consideradas

- **Undo por snapshots** (clonar toda la base en cada cambio): simple pero caro en memoria y no
  da descripciones de "que" cambio.
- **Validacion solo con `IDataErrorInfo`** (la interfaz vieja, sincrona por propiedad): menos
  flexible que `INotifyDataErrorInfo` (que permite errores async y multiples por propiedad).
- **Bloquear la edicion de valores invalidos**: peor UX — el usuario necesita pasar por estados
  intermedios invalidos mientras tipea.
