using Foundry.Core.Content;

namespace Foundry.Evals;

/// <summary>
/// Base de contenido chica y estable para los evals: una cadena de tropas, tres torres con precios
/// distintos y dos enemigos (uno con armadura pesada). Cada caso parte de una copia fresca.
/// </summary>
public static class SampleContent
{
    public static ContentDatabase Build()
    {
        var db = new ContentDatabase();

        db.Add(new Troop
        {
            Id = new EntityId("troop.militia"), Name = "Milicia", Cost = 50, Damage = 8,
            DamageType = DamageType.Physical, AttacksPerSecond = 1.0, AttackRange = 1.0, Health = 60,
            Armor = ArmorClass.Unarmored, UpgradesInto = new EntityId("troop.swordsman"),
        });
        db.Add(new Troop
        {
            Id = new EntityId("troop.swordsman"), Name = "Espadachin", Cost = 120, Damage = 16,
            DamageType = DamageType.Physical, AttacksPerSecond = 1.1, AttackRange = 1.0, Health = 130,
            Armor = ArmorClass.Light, UpgradesInto = new EntityId("troop.knight"),
        });
        db.Add(new Troop
        {
            Id = new EntityId("troop.knight"), Name = "Caballero", Cost = 260, Damage = 28,
            DamageType = DamageType.Physical, AttacksPerSecond = 1.0, AttackRange = 1.0, Health = 320,
            Armor = ArmorClass.Heavy,
        });
        db.Add(new Troop
        {
            Id = new EntityId("troop.archer"), Name = "Arquero", Cost = 90, Damage = 12,
            DamageType = DamageType.Physical, AttacksPerSecond = 1.4, AttackRange = 6.0, Health = 45,
            Armor = ArmorClass.Unarmored,
        });

        db.Add(new Tower
        {
            Id = new EntityId("tower.arrow"), Name = "Torre de arqueros", Cost = 100, BuildTimeSeconds = 3.0,
            Damage = 14, DamageType = DamageType.Physical, AttacksPerSecond = 1.5, AttackRange = 8.0,
        });
        db.Add(new Tower
        {
            Id = new EntityId("tower.frost"), Name = "Torre de hielo", Cost = 150, BuildTimeSeconds = 4.0,
            Damage = 8, DamageType = DamageType.Magic, AttacksPerSecond = 1.0, AttackRange = 6.5,
        });
        db.Add(new Tower
        {
            Id = new EntityId("tower.cannon"), Name = "Torre de cañon", Cost = 180, BuildTimeSeconds = 5.0,
            Damage = 40, DamageType = DamageType.Siege, AttacksPerSecond = 0.6, AttackRange = 7.0,
        });

        db.Add(new Enemy
        {
            Id = new EntityId("enemy.raider"), Name = "Saqueador", Kind = EnemyKind.Ground, Health = 90,
            Armor = ArmorClass.Light, MoveSpeed = 1.4, GoldReward = 12, DamageToBase = 1,
        });
        db.Add(new Enemy
        {
            Id = new EntityId("enemy.juggernaut"), Name = "Juggernaut", Kind = EnemyKind.Boss, Health = 900,
            Armor = ArmorClass.Heavy, MoveSpeed = 0.6, GoldReward = 120, DamageToBase = 10,
        });

        return db;
    }
}
