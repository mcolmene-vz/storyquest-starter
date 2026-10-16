using System.Collections.Generic;

namespace Game.Core.Battle
{
    public sealed record HeroSpec(string ClassId, int Level, string Name);

    public sealed record BattleSetup(HeroSpec[] Heroes, string[] EnemyIds, Dictionary<string, int> Inventory, int Seed);
}
