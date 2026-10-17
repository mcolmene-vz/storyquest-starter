using System;
using System.Collections.Generic;
using Game.Core.Battle;
using Game.Core.Data;

namespace Game.Core
{
    /// Level thresholds and per-level stats from levels.json. The level cap is the length of xpToReach (ten);
    /// surplus XP past the cap is kept and does nothing. Also the camp rules on a PartyRecord (ARCHITECTURE §15.5):
    /// every illegal call throws an ArgumentException before it changes anything, so the UI never offers one.
    public static class Progression
    {
        public static int LevelFor(int totalXp, IReadOnlyList<int> xpToReach)
        {
            int level = 1;
            for (int i = 1; i < xpToReach.Count; i++)
            {
                if (totalXp < xpToReach[i])
                    break;
                level = i + 1;
            }
            return level;
        }

        public static Stats StatsFor(string classId, int level, GameTables tables)
        {
            if (!tables.Levels.Stats.TryGetValue(classId, out var rows))
                throw new ArgumentException($"no level rows for class '{classId}'", nameof(classId));
            if (level < 1 || level > rows.Count)
                throw new ArgumentOutOfRangeException(nameof(level), level, $"class '{classId}' has levels 1..{rows.Count}");
            var row = rows[level - 1];
            return new Stats(row.Hp, row.Attack, row.Defence, row.Speed);
        }

        public static (int NewTotal, int OldLevel, int NewLevel) ApplyXp(int totalXp, int gained, IReadOnlyList<int> xpToReach)
        {
            if (gained < 0)
                throw new ArgumentOutOfRangeException(nameof(gained), gained, "XP gained cannot be negative");
            int newTotal = totalXp + gained;
            return (newTotal, LevelFor(totalXp, xpToReach), LevelFor(newTotal, xpToReach));
        }

        /// The level row plus each equipped item's bonus on its stat; a null slot (the starter) adds nothing.
        public static Stats HeroStats(string classId, int level, Equipment equipment, GameTables tables)
        {
            var stats = StatsFor(classId, level, tables);
            stats = AddBonus(stats, classId, ItemKinds.Weapon, equipment.Weapon, tables);
            stats = AddBonus(stats, classId, ItemKinds.Armor, equipment.Armor, tables);
            return AddBonus(stats, classId, ItemKinds.Charm, equipment.Charm, tables);
        }

        /// The inventory is a copy, so the battle and a replay of it never change the party's pool.
        public static BattleSetup ToBattleSetup(PartyRecord party, string[] enemyIds, int seed, GameTables tables)
        {
            var heroes = new HeroSpec[party.Heroes.Count];
            for (int i = 0; i < heroes.Length; i++)
            {
                var hero = party.Heroes[i];
                heroes[i] = new HeroSpec(hero.ClassId, LevelFor(hero.Xp, tables.Levels.XpToReach), hero.Name, hero.Body,
                    hero.Equipped);
            }
            return new BattleSetup(heroes, enemyIds, new Dictionary<string, int>(party.Consumables), seed);
        }

        /// The hero pays. Equipment must be for the hero's class and not owned yet, and is equipped on purchase so it
        /// shows in the next battle; a consumable adds one to the party pool.
        public static void Buy(PartyRecord party, int hero, string itemId, GameTables tables)
        {
            var record = Hero(party, hero);
            var item = tables.Item(itemId);
            if (item.Price > record.Coins)
                throw new ArgumentException($"{record.Name} has {record.Coins} coins and '{itemId}' costs {item.Price}", nameof(itemId));

            if (item.Kind == ItemKinds.Consumable)
            {
                record.Coins -= item.Price;
                party.Consumables.TryGetValue(itemId, out int count);
                party.Consumables[itemId] = count + 1;
                return;
            }

            if (item.ClassId != record.ClassId)
                throw new ArgumentException($"'{itemId}' is for class '{item.ClassId}', not '{record.ClassId}'", nameof(itemId));
            if (record.Owned.Contains(itemId))
                throw new ArgumentException($"{record.Name} already owns '{itemId}'", nameof(itemId));
            var equipped = WithSlot(record.Equipped, item.Kind, itemId);

            record.Coins -= item.Price;
            record.Owned.Add(itemId);
            record.Equipped = equipped;
        }

        /// Puts an owned item of that kind in its slot, or null for the starter (for the charm: none).
        public static void Equip(PartyRecord party, int hero, string kind, string? itemId, GameTables tables)
        {
            var record = Hero(party, hero);
            if (itemId != null)
            {
                var item = tables.Item(itemId);
                if (item.Kind != kind)
                    throw new ArgumentException($"'{itemId}' is a {item.Kind}, not a {kind}", nameof(itemId));
                if (!record.Owned.Contains(itemId))
                    throw new ArgumentException($"{record.Name} does not own '{itemId}'", nameof(itemId));
            }
            record.Equipped = WithSlot(record.Equipped, kind, itemId);
        }

        /// Every hero receives the full amount, as every hero receives the full XP (producer ruling 2026-10-16).
        public static void AwardCoins(PartyRecord party, int coins)
        {
            if (coins < 0)
                throw new ArgumentOutOfRangeException(nameof(coins), coins, "coins awarded cannot be negative");
            foreach (var hero in party.Heroes)
                hero.Coins += coins;
        }

        static Stats AddBonus(Stats stats, string classId, string kind, string? itemId, GameTables tables)
        {
            if (itemId == null)
                return stats;
            var item = tables.Item(itemId);
            if (item.Kind != kind || item.ClassId != classId)
                throw new ArgumentException($"'{itemId}' is not a {classId} {kind}", nameof(itemId));
            return item.Stat switch
            {
                StatNames.Hp => stats with { MaxHp = stats.MaxHp + item.Bonus },
                StatNames.Attack => stats with { Attack = stats.Attack + item.Bonus },
                StatNames.Defence => stats with { Defence = stats.Defence + item.Bonus },
                StatNames.Speed => stats with { Speed = stats.Speed + item.Bonus },
                _ => throw new ArgumentException($"'{itemId}' has unknown stat '{item.Stat}'", nameof(itemId)),
            };
        }

        static Equipment WithSlot(Equipment equipment, string kind, string? itemId) => kind switch
        {
            ItemKinds.Weapon => equipment with { Weapon = itemId },
            ItemKinds.Armor => equipment with { Armor = itemId },
            ItemKinds.Charm => equipment with { Charm = itemId },
            _ => throw new ArgumentException($"'{kind}' is not an equipment slot (weapon/armor/charm)", nameof(kind)),
        };

        static HeroRecord Hero(PartyRecord party, int hero)
        {
            if (hero < 0 || hero >= party.Heroes.Count)
                throw new ArgumentOutOfRangeException(nameof(hero), hero, $"the party has heroes 0..{party.Heroes.Count - 1}");
            return party.Heroes[hero];
        }
    }
}
