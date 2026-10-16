using System;
using System.Collections.Generic;
using Game.Core;
using Game.Core.Battle;
using Game.Core.Data;

namespace Game.Runtime
{
    public enum BattleOutcome
    {
        Victory,
        DefeatAlwaysWin,
        DefeatNormal,
    }

    public sealed class HeroXpGain
    {
        public HeroXpGain(int combatantId, string name, int gained, int oldLevel, int newLevel)
        {
            CombatantId = combatantId;
            Name = name;
            Gained = gained;
            OldLevel = oldLevel;
            NewLevel = newLevel;
        }

        public int CombatantId { get; }
        public string Name { get; }
        public int Gained { get; }
        public int OldLevel { get; }
        public int NewLevel { get; }
        public bool LeveledUp => NewLevel > OldLevel;
    }

    /// Owns the running battle (ARCHITECTURE §4, §14.4). Submit resolves a hero command and every enemy turn that
    /// follows into one event batch; the difficulty rule of §14.5 is applied here, so Core stays difficulty-free.
    /// Hero XP totals live in memory per class until SaveService arrives in M3.
    public sealed class BattleService
    {
        readonly GameTables tables;
        readonly Dictionary<string, int> xpByClass = new Dictionary<string, int>();
        readonly List<HeroXpGain> lastGains = new List<HeroXpGain>();
        BattleSetup? setup;
        BattleState? state;
        SeededRandom? random;

        public BattleService(GameTables tables)
        {
            this.tables = tables;
        }

        public event Action<IReadOnlyList<BattleEvent>>? BatchReady;

        public BattleState State => state ?? throw new InvalidOperationException("no battle has started");
        public BattleSetup Setup => setup ?? throw new InvalidOperationException("no battle has started");
        public IReadOnlyList<BattleEvent> LastEvents { get; private set; } = Array.Empty<BattleEvent>();
        /// Set by the batch that ended the battle; null while it runs.
        public BattleOutcome? Outcome { get; private set; }
        /// Per-hero XP applied by the Victory batch, for the result screen.
        public IReadOnlyList<HeroXpGain> LastGains => lastGains;

        public void Start(BattleSetup setup)
        {
            this.setup = setup;
            random = new SeededRandom(setup.Seed);
            var step = BattleResolver.Start(BattleFactory.Create(setup, tables), random);
            state = step.State;
            LastEvents = step.Events;
            Outcome = null;
            lastGains.Clear();
        }

        /// Resolves the hero's command, then every enemy turn until a hero's turn or the end, as one batch.
        public void Submit(Command command)
        {
            var rng = random ?? throw new InvalidOperationException("no battle has started");
            var step = BattleResolver.Resolve(State, command, rng);
            var events = new List<BattleEvent>(step.Events);
            var next = step.State;
            while (next.Phase == BattlePhase.Active && next.Get(next.ActiveId).Team == Team.Enemy)
            {
                step = BattleResolver.Resolve(next, BattleResolver.EnemyCommand(next, rng), rng);
                events.AddRange(step.Events);
                next = step.State;
            }

            state = next;
            LastEvents = events;
            if (next.Phase == BattlePhase.Won)
            {
                ApplyVictoryXp(next);
                Outcome = BattleOutcome.Victory;
            }
            else if (next.Phase == BattlePhase.Lost)
            {
                // §14.5: the difficulty rule is read when the party falls, not when the battle began.
                Outcome = DebugSettings.Current == DebugSettings.Difficulty.AlwaysWin
                    ? BattleOutcome.DefeatAlwaysWin
                    : BattleOutcome.DefeatNormal;
            }
            BatchReady?.Invoke(events);
        }

        /// Defeats restart the same setup and seed with full HP and the starting inventory; Victory draws a new battle.
        public void Restart(bool keepSetupAndSeed) => Start(keepSetupAndSeed ? Setup : DebugParty.Create());

        void ApplyVictoryXp(BattleState won)
        {
            lastGains.Clear();
            foreach (var c in won.Combatants)
            {
                if (c.Team != Team.Hero)
                    continue;
                xpByClass.TryGetValue(c.DefinitionId, out int total);
                var (newTotal, oldLevel, newLevel) = Progression.ApplyXp(total, won.XpEarned, tables.Levels.XpToReach);
                xpByClass[c.DefinitionId] = newTotal;
                lastGains.Add(new HeroXpGain(c.Id, c.Name, won.XpEarned, oldLevel, newLevel));
            }
        }
    }
}
