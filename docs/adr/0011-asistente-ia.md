# 0011 — Asistente con IA y proveedor intercambiable

**Estado:** aceptado · post Dia 4

## Contexto

Se quiere un panel donde un diseñador escriba en lenguaje natural ("creá un counter del orco",
"¿esta torre esta rota?") y el editor genere o revise contenido. Requisitos: no atarse a un
proveedor de LLM, poder cambiarlo sin refactorizar, y que la app funcione aunque no haya ningun
modelo configurado.

## Decision

Dos capas:

```
AssistantViewModel  →  ContentAssistant  →  IChatCompletion  →  (proveedor)
```

- **`IChatCompletion`** (puerto en Application): `mensajes → texto`. Genérico, no sabe nada de
  Foundry. Los proveedores lo implementan y son tontos.
- **`ContentAssistant`** (Application): el "cerebro". Arma el system prompt con
  `SchemaDescription.ForPrompt()` (el **mismo esquema que arma el Inspector**) y el user prompt
  con un resumen del contenido actual; parsea la respuesta (tolera fences ```json```), valida las
  entidades propuestas deserializandolas con las mismas `JsonSerializerOptions` del repositorio.
  **Esta clase no cambia al cambiar de modelo.**
- **Aplicar una propuesta** pasa por el `UndoStack` (`AddEntitiesAction`): se puede deshacer.

Proveedores (una clase cada uno, en Infrastructure):

| Provider | Que hace | Costo | Requiere |
|---|---|---|---|
| `stub` (default) | respuestas armadas | 0 | nada |
| `anthropic` | API `/v1/messages` (Haiku) | centavos/llamada | API key |
| `ollama` | modelo local `localhost:11434` | 0 | Ollama + hardware |
| `claude-code` | CLI `claude -p` | incluido en la suscripcion | `claude` en el PATH, uso individual |

**El unico lugar donde se elige** es `App.RegisterAssistantProvider`, manejado por config:
`Assistant:Provider` (y `ApiKey` / `Model`). Cambiar de modelo = editar una palabra, sin
recompilar. Agregar un proveedor nuevo = una clase `IChatCompletion` + un `case`.

### Credenciales

El default `claude-code` **no usa API key**: shellea `claude -p` y hereda la sesion local de
Claude Code (token en `%APPDATA%\Claude` / keychain del SO, fuera del proyecto). Por eso el repo
no tiene ni necesita secretos.

Para `anthropic`, la key se lee en este orden (gana el ultimo):

1. `appsettings.json` — **siempre vacio**, va al repo.
2. `appsettings.Local.json` — ignorado por git (`.gitignore` + un `.example` de plantilla),
   se copia al output asi sobrevive a los rebuilds. Aca va la key en una maquina de dev.
3. Variable de entorno `Assistant__ApiKey` — para CI o para no tener el secreto en ningun archivo.

`RegisterAssistantProvider` trata `""` como "no seteado", asi un campo vacio en el JSON no pisa
a la variable de entorno.

## Consecuencias

- **+** El proveedor es una decision de configuracion, no de codigo.
- **+** La app arranca y el panel funciona sin configurar nada (`stub`).
- **+** El prompt se genera del esquema: agregar una entidad nueva tambien la habilita en el
  asistente, sin tocarlo.
- **+** Lo que el modelo propone se valida con el mismo pipeline que el resto del contenido; una
  respuesta con basura no entra a la base.
- **−** La calidad depende del proveedor: `stub` es de juguete, un modelo local chico es
  mediocre, `anthropic`/`claude-code` son buenos.
- **−** `claude-code` sirve solo para uso individual (no distribuible con una cuenta compartida)
  y necesita el CLI instalado.
- **−** No hay streaming: la respuesta aparece de una. Aceptable para respuestas cortas.

## Alternativas consideradas

- **Un solo proveedor hardcodeado**: mas simple, pero justamente lo que se queria evitar.
- **"Una API propia que hable con Claude Code"**: Claude Code es una herramienta de desarrollo,
  no un backend que una app pueda consumir de forma soportada. El provider `claude-code` (CLI en
  modo headless) cubre el caso individual sin montar un servicio.
- **Aplicar lo generado directamente**: se descarto — el usuario revisa la propuesta y la aplica
  con un boton, y queda en el historial de undo.
