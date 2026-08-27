using System.ComponentModel.DataAnnotations;
using Foundry.Core.Editing;

namespace Foundry.Core.Content;

/// <summary>Unidad que controla el jugador y despliega para defender.</summary>
public sealed class Troop : ContentEntity
{
    public override string CategoryName => "Tropas";

    [EditableProperty(Label = "Costo", Group = "Economia", Order = 0)]
    [Range(0, 9999)]
    public int Cost { get; set; }

    [EditableProperty(Label = "Daño", Group = "Combate", Order = 0)]
    [Range(0, 9999)]
    public int Damage { get; set; }

    [EditableProperty(Label = "Tipo de daño", Group = "Combate", Order = 1)]
    public DamageType DamageType { get; set; }

    [EditableProperty(Label = "Ataques por segundo", Group = "Combate", Order = 2)]
    [Range(0.1, 10.0)]
    public double AttacksPerSecond { get; set; } = 1.0;

    [EditableProperty(Label = "Alcance", Group = "Combate", Order = 3)]
    [Range(0.5, 30.0)]
    public double AttackRange { get; set; } = 1.0;

    [EditableProperty(Label = "Vida", Group = "Defensa", Order = 0)]
    [Range(1, 99999)]
    public int Health { get; set; } = 1;

    [EditableProperty(Label = "Armadura", Group = "Defensa", Order = 1)]
    public ArmorClass Armor { get; set; }

    [EditableProperty(Label = "Mejora a", Group = "Progresion", Order = 0,
        Description = "Tropa a la que asciende esta unidad, si corresponde.")]
    [AssetReference(typeof(Troop))]
    public EntityId? UpgradesInto { get; set; }
}
