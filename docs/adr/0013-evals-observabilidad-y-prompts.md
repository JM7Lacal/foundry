# 0013 — Evals, observabilidad y versionado de prompts del asistente

**Estado:** aceptado · post asistente-IA (ver [ADR 0011](0011-asistente-ia.md))

## Contexto

El asistente (ADR 0011) quedó funcional pero sin red de seguridad. El `ContentAssistant` arma un
prompt, llama al modelo y parsea la respuesta; no hay forma de:

- saber si un cambio en el system prompt o en el `SchemaDescription` mejora o empeora las
  respuestas — cada ajuste es a ojo;
- medir cuánto cuesta, cuánto tarda y con qué frecuencia falla una llamada;
- reaccionar a un fallo transitorio del proveedor (429, 5xx, timeout) sin que el panel tire error;
- probar dos versiones de prompt una contra otra.

Son propiedades de un sistema de IA en producción, no de un prototipo. El objetivo de este ADR es
cubrirlas sin atar el diseño a un proveedor ni ensuciar el dominio.

## Decisión

Tres piezas, todas sobre el puerto `IChatCompletion` que ya existe.

### 1. System prompts versionados

Los prompts salen del código y pasan a ser recursos incrustados versionados
(`src/Foundry.Application/Ai/Prompts/assistant-system.v{N}.txt`), con marcadores `{clave}` para lo
que depende del estado (hoy solo `{schema}`). `PromptLibrary` los carga; `PromptTemplate.Render`
los completa. `ContentAssistant` recibe la versión a usar (id `assistant-system@v2` o familia →
última) y propaga el id en `AssistantResult.PromptId`. Se elige por config
(`Assistant:PromptVersion`), sin recompilar.

`v1` es el prompt original tal cual. `v2` es más estricto (JSON-only explícito, regla de "no
inventes campos", ids en kebab-case, dos ejemplos few-shot).

### 2. Resiliencia + telemetría (decorador)

`ResilientChatCompletion` envuelve a cualquier `IChatCompletion` y agrega, de forma transparente:

- **reintentos** con backoff exponencial (`ResiliencePolicy`, por defecto 3 intentos / 200 ms / x2);
- **fallback** a un segundo proveedor si el primario se agota (`Assistant:Fallback`);
- una **`ChatCallReport`** por llamada (proveedor, duración, intentos, tokens y costo estimados,
  resultado) entregada a un `IChatCallListener`.

`ChatCallLog` implementa el listener: guarda las últimas N llamadas en memoria y agrega una línea
JSON por llamada a `%APPDATA%\Foundry\chat-calls.jsonl`. Tokens (`ChatTokens`, ~4 chars/token) y
costo (`ModelPricing`, precios de lista por prefijo de proveedor) son **estimaciones**: sirven para
dimensionar y detectar prompts que se van de escala, no para facturar.

El decorador es el único `IChatCompletion` que ve el resto de la app; el proveedor crudo queda
detrás. `App.RegisterAssistantProvider` arma la pila; `BuildProvider(name)` construye primario y
fallback con el mismo código.

### 3. Harness de evals + gate de CI

Proyecto `Foundry.Evals` (ejecutable) + `Foundry.Evals.Tests` (gate).

- Un **`EvalCase`** fija un pedido, la base de la que parte y una lista de **afirmaciones**
  deterministas (`Expect.*`): propone / no propone, entidades válidas (reusa `ContentValidator`),
  id en convención, campo en rango, mantiene el id al modificar, respuesta JSON-only, latencia y
  costo bajo umbral, etc.
- El **`EvalRunner`** corre cada caso N veces sobre la misma pila que la app (proveedor →
  `ResilientChatCompletion` → `ContentAssistant`), capturando la respuesta cruda y la
  `ChatCallReport`.
- La no-determinación vive en el modelo, no en la verificación: cada caso pasa si su **pass rate
  ≥ umbral** (0.6–0.8 según cuánto margen legítimo tenga el pedido).
- **`RecordedChat`** reproduce respuestas grabadas (`Fixtures/<caso>.json`). Es lo que usa el gate
  de CI: determinista y sin red — detecta regresiones en el parser, el esquema, el prompt o las
  afirmaciones. La calidad del modelo se mide con `Program.cs -- run --provider <real>` (manual o
  en el stage `evals_live`, con secret).
- `EvalReportRenderer` emite Markdown + JSON + resumen de consola. `azure-pipelines.yml` corre el
  gate dentro de `dotnet test` y publica el reporte como artefacto.

## Consecuencias

- **+** Cambiar el prompt o el esquema y correr `dotnet test` dice al toque si algo se rompió.
- **+** Toda llamada al modelo tiene traza (tiempo, costo, intentos) sin tocar el `ContentAssistant`.
- **+** Un fallo transitorio del proveedor se absorbe con reintentos y, si hace falta, fallback.
- **+** El adapter de Azure OpenAI / Azure AI Foundry entra como un `case` más de `BuildProvider`.
- **−** Los fixtures del gate hay que regenerarlos (`-- record`) cuando cambia a propósito la forma
  de la respuesta esperada; si no, el gate falla con la respuesta vieja.
- **−** El costo y los tokens son estimados por largo de texto, no el conteo real del proveedor.
- **−** El gate valida el contrato (formato, validez, convenciones), no que la respuesta sea *buena*;
  para eso hay que correr los evals contra un modelo real y mirar el pass rate.

## Alternativas consideradas

- **Semantic Kernel / un framework de orquestación**: trae su propio modelo de prompts, planners y
  telemetría. Para un puerto `mensajes → texto` y un harness de ~8 casos es sobredimensionado, y
  esconde el mecanismo que el proyecto quiere mostrar. Se puede migrar si el asistente crece a
  multi-step con tools.
- **LLM-as-judge como gate**: evaluar cada respuesta con otro modelo. Útil para calidad subjetiva,
  pero no determinista y con costo — no sirve como gate de CI. Queda como afirmación opcional
  (`Expect` podría sumar una), corriendo solo en el stage live.
- **Polly** para los reintentos: es la opción estándar, pero agrega una dependencia para tres
  líneas de backoff. Si la política se complica (circuit breaker, rate limiting), se adopta.
- **Grabar/reproducir a nivel HTTP** (un `HttpMessageHandler` de cassettes, estilo VCR): captura
  más fiel, pero acopla los fixtures a la forma del request de cada proveedor. `RecordedChat`
  graba en el nivel del puerto, que es agnóstico.
