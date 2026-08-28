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
2. **De la base completa** (`ContentValidator` en Application): dos familias de reglas en lista —
   **por campo** (`CheckRequired`, `CheckRange`, `CheckReference`) recorriendo el `EditableSchema`,
   y **por entidad** (`CheckUpgradeChain`). Devuelve `ValidationIssue`s con `Severity`:
   - **Error** (falta un requerido, fuera de rango, referencia rota, cadena circular): rompe el
     parser del juego → se marca en rojo en el panel y en el arbol, y `OnClosing` avisa antes de
     salir. No bloquea el guardado en curso (ver "Dirty tracking y guardado").
   - **Warning** (una entidad supera en un stat de `Progression` a la que declara como "mejora a"):
     huele mal pero no rompe nada → se avisa.

`CheckUpgradeChain` compara los campos marcados `[EditableProperty(Progression = true)]`
(daño, vida, costo) contra los de la entidad referenciada, y detecta ciclos. Las reglas son
listas: agregar una no toca `Validate`.

### Dirty tracking y guardado

Modelo: **edicion libre en memoria, guardado explicito.** Se edita cualquier cosa sin fricción;
lo que persiste a disco lo decide el usuario con Ctrl+S.

- **Dirty por profundidad de pila**: `MainViewModel` guarda `_savedUndoDepth` (el
  `UndoStack.UndoDepth` al cargar o guardar). `IsDirty` = "la profundidad actual difiere de esa".
  Deshacer a mano hasta el estado guardado vuelve a marcar "limpio". Se ve como `*` en el titulo.
- **Editar campos / cambiar de entidad** no autoguarda ni pregunta nada. Los pasos se apilan en
  el `UndoStack` y quedan pendientes hasta el próximo guardado.
- **Guardar (Ctrl+S)** escribe **toda** la base a disco. **Sin gate de validacion**: el editor
  deja guardar trabajo en progreso — los estados intermedios invalidos son normales al editar, y
  bloquear el guardado hace perder trabajo. El panel de problemas esta siempre visible y el
  chequeo duro corre al cerrar. Si el archivo actual es el ejemplo (dentro del directorio del
  `.exe`) o no hay archivo, pide "Guardar como".
- **El ejemplo empaquetado**: en el primer arranque se copia a `Documentos\Foundry\` y se abre
  desde ahi como archivo normal → Ctrl+S guarda sin pedir destino y el trabajo sobrevive a los
  rebuilds (que regeneran `bin\` y se llevaban puesto lo guardado). Si Documentos no se puede
  escribir, cae a abrirlo como documento "sin titulo" (`CurrentFilePath = null`, primer guardado
  pide destino).
- **Aplicar una propuesta del asistente** = igual que crear una entidad a mano: entra a la base
  por el `UndoStack` (deshacible), se selecciona para que se vea, y se persiste con Ctrl+S como
  todo lo demas.
- **Al cerrar** (`MainWindow.OnClosing`): corre la validacion completa (abre el panel) y, si hay
  errores o cambios sin guardar, avisa — se puede cerrar igual.

> Hubo iteraciones con autoguardado y con una "transaccion por entidad" (revertir la entidad al
> cambiar de seleccion sin guardar). Ambas confundian más de lo que ayudaban; quedaron en la rama
> `save-workflow-wip` por si se retoman.

## Consecuencias

- **+** Undo/redo real sobre cualquier edicion, con costo casi nulo (un tipo de accion).
- **+** El feedback de validacion es inmediato y ademas hay un chequeo global antes de exportar.
- **+** `ContentValidator` es testeable en aislamiento y extensible por reglas.
- **−** El chequeo por campo solo cubre la entidad abierta en el Inspector; el chequeo global
  cubre todo pero recien al validar/cerrar. Es un compromiso consciente (no se instancian VMs
  para las 500 entidades).
- **−** El chequeo de "datos rotos al exportar" es blando: avisa al cerrar pero no impide
  guardar. Se prioriza no perder trabajo; un `Exportar` separado con gate duro seria el
  siguiente paso.
- **−** `MainWindow.OnClosing` y el prompt de cierre viven en el code-behind: el ciclo de vida
  de la ventana no es bindeable y es el caso canonico de code-behind aceptable.

## Alternativas consideradas

- **Undo por snapshots** (clonar toda la base en cada cambio): simple pero caro en memoria y no
  da descripciones de "que" cambio.
- **Validacion solo con `IDataErrorInfo`** (la interfaz vieja, sincrona por propiedad): menos
  flexible que `INotifyDataErrorInfo` (que permite errores async y multiples por propiedad).
- **Bloquear la edicion de valores invalidos**: peor UX — el usuario necesita pasar por estados
  intermedios invalidos mientras tipea.
- **Autoguardado al editar**: descartado por el usuario — prefiere control explicito del momento
  de escritura, y el autoguardado persiste estados intermedios rotos.
- **Prompt "¿guardar?" a nivel documento al cambiar de entidad**: molesto (salta aunque no
  hayas tocado la entidad que dejas) y confuso (el "no" dejaba los cambios igual).
- **Transaccion por entidad** (revertir la entidad al cambiar de seleccion sin guardar): más
  predecible que el prompt, pero seguia siendo un modelo mental raro para un editor. Prototipo
  en `save-workflow-wip`.
