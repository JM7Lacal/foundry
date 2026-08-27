using FluentAssertions;
using Foundry.Application.Content;
using Foundry.Core.Content;
using Foundry.Infrastructure.Content;

namespace Foundry.Infrastructure.Tests.Content;

public sealed class CsvContentImporterTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"foundry-{Guid.NewGuid():N}.csv");
    private readonly CsvContentImporter _importer = new();

    private IReadOnlyList<ContentEntity> ImportText(string csv)
    {
        File.WriteAllText(_path, csv);
        return _importer.Import(_path);
    }

    [Fact]
    public void Maps_columns_to_properties_by_name_and_label()
    {
        var entities = ImportText(
            """
            type,id,Nombre,Costo,damage,damageType
            troop,troop.archer,Arquero,90,12,siege
            enemy,enemy.orc,Orco,,,
            """);

        entities.Should().HaveCount(2);

        var troop = entities.OfType<Troop>().Single();
        troop.Id.Should().Be(new EntityId("troop.archer"));
        troop.Name.Should().Be("Arquero");
        troop.Cost.Should().Be(90);
        troop.Damage.Should().Be(12);
        troop.DamageType.Should().Be(DamageType.Siege);
    }

    [Fact]
    public void Requires_the_type_and_id_columns()
    {
        var act = () => ImportText("name,cost\nArquero,90");

        act.Should().Throw<ContentImportException>().WithMessage("*type*id*");
    }

    [Fact]
    public void Fails_on_an_unknown_entity_type()
    {
        var act = () => ImportText("type,id\nspaceship,x");

        act.Should().Throw<ContentImportException>().WithMessage("*spaceship*");
    }

    [Fact]
    public void Fails_with_line_context_on_a_value_that_does_not_parse()
    {
        var act = () => ImportText("type,id,Costo\ntroop,troop.x,noventa");

        act.Should().Throw<ContentImportException>().WithMessage("*Linea 2*");
    }

    [Fact]
    public void Handles_quoted_fields_with_commas()
    {
        var troop = ImportText("type,id,Nombre\ntroop,troop.x,\"Arquero, elite\"").Single();

        troop.Name.Should().Be("Arquero, elite");
    }

    public void Dispose()
    {
        if (File.Exists(_path))
        {
            File.Delete(_path);
        }
    }
}
