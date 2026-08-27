namespace Foundry.Core.Content;

/// <summary>Como se calcula el daño contra la armadura del objetivo.</summary>
public enum DamageType
{
    Physical,
    Magic,
    Siege,
    True,
}

/// <summary>Clase de armadura de una unidad; define su resistencia a cada <see cref="DamageType"/>.</summary>
public enum ArmorClass
{
    Unarmored,
    Light,
    Heavy,
    Fortified,
}

/// <summary>Categoria de comportamiento de un enemigo en la oleada.</summary>
public enum EnemyKind
{
    Ground,
    Flying,
    Boss,
}
