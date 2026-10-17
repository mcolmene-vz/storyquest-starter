using System.Collections.Generic;
using Game.Core.Data;

namespace Game.Core.Battle
{
    /// Equipment null is Equipment.None; Body is read by the presenter (prefab {ClassId}_{Body}), not by the resolver.
    public sealed record HeroSpec(string ClassId, int Level, string Name, string Body = Bodies.Boy, Equipment? Equipment = null);

    public sealed record BattleSetup(HeroSpec[] Heroes, string[] EnemyIds, Dictionary<string, int> Inventory, int Seed);
}
