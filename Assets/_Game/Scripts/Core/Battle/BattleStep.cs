using System.Collections.Generic;

namespace Game.Core.Battle
{
    public sealed record BattleStep(BattleState State, IReadOnlyList<BattleEvent> Events);
}
