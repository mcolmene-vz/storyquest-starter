using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core.Battle
{
    public sealed class BattleState
    {
        public BattleState(IEnumerable<Combatant> combatants, IReadOnlyDictionary<string, int> inventory,
            IReadOnlyDictionary<string, ItemSpec> items)
        {
            Combatants = combatants.ToList();
            var seen = new HashSet<int>();
            foreach (var c in Combatants)
                if (!seen.Add(c.Id))
                    throw new ArgumentException($"duplicate combatant id {c.Id}", nameof(combatants));

            Inventory = new Dictionary<string, int>();
            foreach (var pair in inventory)
                Inventory[pair.Key] = pair.Value;
            var copy = new Dictionary<string, ItemSpec>();
            foreach (var pair in items)
                copy[pair.Key] = pair.Value;
            Items = copy;
        }

        public List<Combatant> Combatants { get; }
        public int Round { get; set; }
        public List<int> TurnQueue { get; } = new List<int>();
        /// -1 until Start has run.
        public int ActiveId { get; set; } = -1;
        public BattlePhase Phase { get; set; } = BattlePhase.Active;
        /// Party inventory by item id; consumables only matter here.
        public Dictionary<string, int> Inventory { get; }
        public IReadOnlyDictionary<string, ItemSpec> Items { get; }
        public int XpEarned { get; set; }
        public int CoinsEarned { get; set; }

        public Combatant Get(int id)
        {
            foreach (var c in Combatants)
                if (c.Id == id)
                    return c;
            throw new ArgumentException($"no combatant with id {id}", nameof(id));
        }

        public IEnumerable<Combatant> Living(Team team) => Combatants.Where(c => c.Team == team && c.IsAlive);

        public BattleState Clone()
        {
            var copy = new BattleState(Combatants.Select(c => c.Clone()), Inventory, Items)
            {
                Round = Round,
                ActiveId = ActiveId,
                Phase = Phase,
                XpEarned = XpEarned,
                CoinsEarned = CoinsEarned,
            };
            copy.TurnQueue.AddRange(TurnQueue);
            return copy;
        }
    }
}
