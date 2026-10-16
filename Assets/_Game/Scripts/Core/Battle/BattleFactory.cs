using System;
using System.Collections.Generic;
using Game.Core.Data;

namespace Game.Core.Battle
{
    /// Builds the opening BattleState from a setup and the tables. Heroes get ids 0..n-1, enemies follow;
    /// Slot is the position within the team, which is what the stage and the tie-break use.
    public static class BattleFactory
    {
        public static BattleState Create(BattleSetup setup, GameTables tables)
        {
            if (setup.Heroes.Length == 0)
                throw new ArgumentException("a battle needs at least one hero", nameof(setup));
            if (setup.EnemyIds.Length == 0)
                throw new ArgumentException("a battle needs at least one enemy", nameof(setup));

            var combatants = new List<Combatant>();
            int nextId = 0;
            for (int slot = 0; slot < setup.Heroes.Length; slot++)
            {
                var spec = setup.Heroes[slot];
                var cls = tables.Class(spec.ClassId);
                var skill = cls.Skill ?? throw new ArgumentException($"class '{cls.Id}' has no skill", nameof(tables));
                var name = string.IsNullOrEmpty(spec.Name) ? cls.Name : spec.Name;
                combatants.Add(new Combatant(nextId++, name, Team.Hero, cls.Id, slot,
                    Progression.StatsFor(cls.Id, spec.Level, tables), DamageTypes.Parse(cls.DamageType), cls.AttackPower)
                {
                    Skill = new SkillSpec(skill.Id, DamageTypes.Parse(skill.DamageType), skill.Power, skill.CooldownTurns),
                });
            }

            for (int slot = 0; slot < setup.EnemyIds.Length; slot++)
            {
                var def = tables.Enemy(setup.EnemyIds[slot]);
                combatants.Add(new Combatant(nextId++, def.Name, Team.Enemy, def.Id, slot,
                    new Stats(def.Hp, def.Attack, def.Defence, def.Speed), DamageTypes.Parse(def.DamageType), def.AttackPower)
                {
                    Weakness = string.IsNullOrEmpty(def.Weakness) ? (DamageType?)null : DamageTypes.Parse(def.Weakness),
                    Behaviour = def.Behaviour,
                    XpReward = def.Xp,
                    CoinReward = def.Coins,
                });
            }

            var items = new Dictionary<string, ItemSpec>();
            foreach (var item in tables.Items)
            {
                if (item.Kind != ItemKinds.Consumable)
                    continue;
                var effect = item.Effect == ItemEffects.Revive ? ItemEffect.Revive : ItemEffect.Heal;
                items[item.Id] = new ItemSpec(item.Id, effect, item.Amount);
            }

            return new BattleState(combatants, setup.Inventory, items);
        }
    }
}
