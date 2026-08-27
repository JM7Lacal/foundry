using FluentAssertions;
using Foundry.Presentation.ViewModels;
using Foundry.Presentation.ViewModels.Inspector;
using Foundry.Core.Content;

namespace Foundry.Presentation.Tests.ViewModels;

public class InspectorViewModelTests
{
    private static (InspectorViewModel Inspector, Troop Troop) LoadedTroop()
    {
        var database = new ContentDatabase();
        var troop = new Troop { Id = new EntityId("troop.archer"), Name = "Arquero", Damage = 10 };
        var other = new Troop { Id = new EntityId("troop.knight"), Name = "Caballero" };
        database.Add(troop);
        database.Add(other);

        var inspector = new InspectorViewModel();
        inspector.Load(troop, database);
        return (inspector, troop);
    }

    private static PropertyFieldViewModel Field(InspectorViewModel inspector, string label) =>
        inspector.Groups.SelectMany(g => g.Fields).Single(f => f.Label == label);

    [Fact]
    public void Load_builds_grouped_fields_from_the_schema()
    {
        var (inspector, _) = LoadedTroop();

        inspector.HasEntity.Should().BeTrue();
        var groupNames = inspector.Groups.Select(g => g.Name).ToList();
        groupNames.Should().Contain("General");
        groupNames.Should().Contain("Combate");
        groupNames.Should().Contain("Economia");
        inspector.Groups.SelectMany(g => g.Fields).Should().Contain(f => f.Label == "Daño");
    }

    [Fact]
    public void Load_with_null_clears_and_reports_no_entity()
    {
        var (inspector, _) = LoadedTroop();

        inspector.Load(null, new ContentDatabase());

        inspector.HasEntity.Should().BeFalse();
        inspector.Groups.Should().BeEmpty();
    }

    [Fact]
    public void Editing_a_whole_number_field_writes_through_to_the_entity()
    {
        var (inspector, troop) = LoadedTroop();
        var damage = (WholeNumberFieldViewModel)Field(inspector, "Daño");

        damage.Value = 42;

        troop.Damage.Should().Be(42);
    }

    [Fact]
    public void Editing_a_field_raises_EntityEdited()
    {
        var (inspector, _) = LoadedTroop();
        var raised = 0;
        inspector.EntityEdited += (_, _) => raised++;

        ((WholeNumberFieldViewModel)Field(inspector, "Daño")).Value = 5;

        raised.Should().Be(1);
    }

    [Fact]
    public void Reference_field_lists_other_entities_of_the_target_type_plus_a_none_option()
    {
        var (inspector, _) = LoadedTroop();
        var reference = (ReferenceFieldViewModel)Field(inspector, "Mejora a");

        reference.Options.Should().HaveCount(2); // "(ninguno)" + troop.knight (no se incluye a si misma)
        reference.Options[0].Id.Should().BeNull();
        reference.Options[1].Id.Should().Be(new EntityId("troop.knight"));
    }

    [Fact]
    public void Setting_a_reference_option_writes_the_id_to_the_entity()
    {
        var (inspector, troop) = LoadedTroop();
        var reference = (ReferenceFieldViewModel)Field(inspector, "Mejora a");

        reference.SelectedOption = reference.Options[1];

        troop.UpgradesInto.Should().Be(new EntityId("troop.knight"));
    }
}
