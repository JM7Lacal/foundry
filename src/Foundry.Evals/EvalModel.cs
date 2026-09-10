using Foundry.Application.Ai;
using Foundry.Application.Validation;
using Foundry.Core.Content;

namespace Foundry.Evals;

/// <summary>Un escenario de evaluacion: un pedido sobre una base concreta y lo que se espera del resultado.</summary>
/// <param name="Threshold">
/// Pass rate minimo (0..1) para considerar el caso aprobado. Menor a 1 para casos donde el modelo
/// tiene margen legitimo de variacion (pedidos ambiguos, razonamiento de balance).
/// </param>
public sealed record EvalCase(
    string Name,
    string Category,
    string Request,
    Func<ContentDatabase> Arrange,
    IReadOnlyList<EvalAssertion> Assertions,
    double Threshold = 0.8);

/// <summary>Una afirmacion verificable sobre el resultado de un caso.</summary>
public sealed record EvalAssertion(string Description, Func<EvalContext, EvalCheck> Check);

/// <summary>Todo lo observable de una corrida: el pedido, la base, la respuesta cruda y parseada, la traza de la llamada.</summary>
public sealed record EvalContext(
    string Request,
    ContentDatabase Database,
    string RawReply,
    AssistantResult Result,
    ChatCallReport? Call,
    ContentValidator Validator,
    string? Error);

/// <summary>Resultado de evaluar una <see cref="EvalAssertion"/>.</summary>
public sealed record EvalCheck(bool Passed, string? Detail = null)
{
    public static EvalCheck Pass(string? detail = null) => new(true, detail);

    public static EvalCheck Fail(string detail) => new(false, detail);

    public static EvalCheck From(bool condition, string failDetail) =>
        condition ? Pass() : Fail(failDetail);
}
