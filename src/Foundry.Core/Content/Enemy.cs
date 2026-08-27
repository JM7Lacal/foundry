using System.ComponentModel.DataAnnotations;
using Foundry.Core.Editing;

namespace Foundry.Core.Content;

/// <summary>Unidad hostil que avanza por el camino hacia la base del jugador.</summary>
public sealed class Enemy : ContentEntity
{
    public override string CategoryName => "Enemigos";

    [EditableProperty(Label = "Tipo", Group = "General", Order = 1)]
    public EnemyKind Kind { get; set; }

    [EditableProperty(Label = "Vida", Group = "Defensa", Order = 0)]
    [Range(1, 999999)]
    public int Health { get; set; } = 10;

    [EditableProperty(Label = "Armadura", Group = "Defensa", Order = 1)]
    public ArmorClass Armor { get; set; }

    [EditableProperty(Label = "Velocidad", Group = "Movimiento", Order = 0)]
    [Range(0.1, 20.0)]
    public double MoveSpeed { get; set; } = 1.0;

    [EditableProperty(Label = "Oro al morir", Group = "Economia", Order = 0)]
    [Range(0, 9999)]
    public int GoldReward { get; set; }

    [EditableProperty(Label = "Daño a la base", Group = "Combate", Order = 0)]
    [Range(0, 999)]
    public int DamageToBase { get; set; } = 1;
}
