using Foundry.Core.Content;

namespace Foundry.Evals;

/// <summary>
/// Los casos de evaluacion del asistente. Cada uno fija un pedido, la base de la que parte y las
/// afirmaciones que debe cumplir el resultado. El <c>Name</c> tambien nombra el fixture grabado
/// (<c>Fixtures/&lt;name&gt;.json</c>).
/// </summary>
public static class EvalCatalog
{
    public static IReadOnlyList<EvalCase> All { get; } =
    [
        new EvalCase(
            "create-counter-heavy",
            "creacion",
            "Creá una tropa de nivel medio que sirva de counter contra enemigos con armadura pesada.",
            SampleContent.Build,
            [
                Expect.ProposesEntities(1, 1),
                Expect.ProposedAreOfType<Troop>(),
                Expect.AllProposedEntitiesValid(),
                Expect.IdsFollowConvention(),
                Expect.ProposedFieldInRange("", "cost", 40, 320),
                Expect.ProposedChoiceIsOneOf("damageType", "siege", "magic", "true"),
            ]),

        new EvalCase(
            "price-question",
            "consulta",
            "¿Cuál es la torre más cara que tengo y cuánto cuesta?",
            SampleContent.Build,
            [
                Expect.AnswersWithoutProposal(),
                Expect.AnswerMentionsAnyOf("cañon", "cañón", "180"),
            ]),

        new EvalCase(
            "nerf-knight",
            "modificacion",
            "El caballero (troop.knight) está demasiado fuerte. Bajale el daño alrededor de un 20%.",
            SampleContent.Build,
            [
                Expect.ProposesEntities(1, 1),
                Expect.KeepsExistingId("troop.knight"),
                Expect.ProposedFieldLessThan("knight", "damage", 28),
                Expect.AllProposedEntitiesValid(),
            ]),

        new EvalCase(
            "json-only",
            "formato",
            "¿Qué tropa me conviene desplegar contra el saqueador?",
            SampleContent.Build,
            [
                Expect.ReplyIsJsonOnly(),
                Expect.AnswersWithoutProposal(),
            ]),

        new EvalCase(
            "out-of-schema-level",
            "limites",
            "Agregá un nivel nuevo con 5 oleadas de enemigos y un jefe al final.",
            SampleContent.Build,
            [Expect.AnswersWithoutProposal()],
            Threshold: 0.6),

        new EvalCase(
            "multi-create-siege",
            "creacion",
            "Creá dos tropas de asedio distintas: una barata para el early game y una cara para el late.",
            SampleContent.Build,
            [
                Expect.ProposesEntities(2, 2),
                Expect.ProposedAreOfType<Troop>(),
                Expect.AllProposedEntitiesValid(),
                Expect.IdsFollowConvention(),
            ],
            Threshold: 0.7),

        new EvalCase(
            "budget",
            "consulta",
            "Listame las torres ordenadas por costo, de la más barata a la más cara.",
            SampleContent.Build,
            [
                Expect.AnswersWithoutProposal(),
                Expect.LatencyUnder(TimeSpan.FromSeconds(25)),
                Expect.EstimatedCostUnder(0.03m),
            ]),

        new EvalCase(
            "no-invented-fields",
            "limites",
            "Creá una tropa que sea invisible y pueda volar por encima de las torres.",
            SampleContent.Build,
            [HandledWithoutInventingSchema()],
            Threshold: 0.6),
    ];

    public static EvalCase ByName(string name) =>
        All.FirstOrDefault(c => c.Name == name)
        ?? throw new KeyNotFoundException($"No existe el caso de eval '{name}'.");

    /// <summary>
    /// El modelo puede resolver un pedido imposible de dos formas validas: explicando en texto que
    /// esos campos no existen, o proponiendo una tropa valida (sin inventar campos). Cualquiera de
    /// las dos pasa; inventar un campo o proponer algo invalido, no.
    /// </summary>
    private static EvalAssertion HandledWithoutInventingSchema() => new(
        "resuelve sin inventar campos ni tipos (texto explicativo o entidad válida)",
        ctx =>
        {
            if (!ctx.Result.HasProposal)
            {
                return EvalCheck.From(
                    !string.IsNullOrWhiteSpace(ctx.Result.Answer), "sin propuesta y sin texto en answer");
            }

            var valid = Expect.AllProposedEntitiesValid().Check(ctx);
            var ids = Expect.IdsFollowConvention().Check(ctx);
            return valid.Passed && ids.Passed
                ? EvalCheck.Pass()
                : EvalCheck.Fail(valid.Passed ? ids.Detail! : valid.Detail!);
        });
}
