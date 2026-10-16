using System;
using System.Collections.Generic;
using Game.Core.Battle;
using Game.Core.Data;

namespace Game.Runtime
{
    /// Owns the running battle (ARCHITECTURE §4, §14.4). D4 starts one; Submit and the hand-off to the presenter
    /// arrive with the UI and the hit recipe.
    public sealed class BattleService
    {
        readonly GameTables tables;
        BattleState? state;
        SeededRandom? random;

        public BattleService(GameTables tables)
        {
            this.tables = tables;
        }

        public BattleState State => state ?? throw new InvalidOperationException("no battle has started");
        public IReadOnlyList<BattleEvent> LastEvents { get; private set; } = Array.Empty<BattleEvent>();

        public void Start(BattleSetup setup)
        {
            random = new SeededRandom(setup.Seed);
            var step = BattleResolver.Start(BattleFactory.Create(setup, tables), random);
            state = step.State;
            LastEvents = step.Events;
        }
    }
}
