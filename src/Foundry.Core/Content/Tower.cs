using System.ComponentModel.DataAnnotations;
using Foundry.Core.Editing;

namespace Foundry.Core.Content;

/// <summary>Estructura defensiva fija que ataca a los enemigos que pasan a rango.</summary>
public sealed class Tower : ContentEntity
{
    public override string CategoryName => "Torres";

    [EditableProperty(Label = "Costo", Group = "Economia", Order = 0, Progression = true)]
    [Range(0, 9999)]
    public int Cost { get; set; }

    [EditableProperty(Label = "Segundos de construccion", Group = "Economia", Order = 1)]
    [Range(0.0, 60.0)]
    public double BuildTimeSeconds { get; set; }

    [EditableProperty(Label = "Daño", Group = "Combate", Order = 0, Progression = true)]
    [Range(0, 9999)]
    public int Damage { get; set; }

    [EditableProperty(Label = "Tipo de daño", Group = "Combate", Order = 1)]
    public DamageType DamageType { get; set; }

    [EditableProperty(Label = "Ataques por segundo", Group = "Combate", Order = 2)]
    [Range(0.1, 10.0)]
    public double AttacksPerSecond { get; set; } = 1.0;

    [EditableProperty(Label = "Alcance", Group = "Combate", Order = 3)]
    [Range(1.0, 40.0)]
    public double AttackRange { get; set; } = 5.0;

    [EditableProperty(Label = "Mejora a", Group = "Progresion", Order = 0,
        Description = "Torre a la que asciende esta, si corresponde.")]
    [AssetReference(typeof(Tower))]
    public EntityId? UpgradesInto { get; set; }
}
