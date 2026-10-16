using System;
using System.Collections.Generic;
using Game.Core.Battle;
using Newtonsoft.Json;

namespace Game.Core.Data
{
    /// Reads the four tables through an ITableSource, validates them against ARCHITECTURE §14.2 and returns
    /// GameTables, or throws one TableValidationException listing every problem found.
    public static class TableLoader
    {
        public const int SupportedVersion = 1;
        public const int LevelCount = 10;

        public const string ClassesTable = "classes";
        public const string EnemiesTable = "enemies";
        public const string LevelsTable = "levels";
        public const string ItemsTable = "items";

        public static readonly string[] ClipStates =
            { "Idle", "Walk", "Attack", "Skill", "Hit", "BigStagger", "Defend", "Defeat", "Victory" };

        // JSON nulls keep the model's initialisers instead of overwriting them, so validation never meets a null list.
        static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
        };

        public static GameTables Load(ITableSource source)
        {
            var problems = new List<string>();
            var classes = Read<ClassesTable>(source, ClassesTable, problems);
            var enemies = Read<EnemiesTable>(source, EnemiesTable, problems);
            var levels = Read<LevelTable>(source, LevelsTable, problems);
            var items = Read<ItemsTable>(source, ItemsTable, problems);

            var classIds = new HashSet<string>();
            if (classes != null)
                ValidateClasses(classes, classIds, problems);
            if (enemies != null)
                ValidateEnemies(enemies, problems);
            if (levels != null)
                ValidateLevels(levels, classes != null ? classIds : null, problems);
            if (items != null)
                ValidateItems(items, classes != null ? classIds : null, problems);

            if (problems.Count > 0)
                throw new TableValidationException(problems);
            return new GameTables(classes!.Classes, enemies!.Enemies, levels!, items!.Items);
        }

        static T? Read<T>(ITableSource source, string name, List<string> problems) where T : class, IVersionedTable
        {
            string text;
            try
            {
                text = source.ReadText(name);
            }
            catch (Exception e)
            {
                problems.Add($"{name}.json: could not be read ({e.Message})");
                return null;
            }

            T? table;
            try
            {
                table = JsonConvert.DeserializeObject<T>(text, Settings);
            }
            catch (JsonException e)
            {
                problems.Add($"{name}.json: invalid JSON ({e.Message})");
                return null;
            }

            if (table == null)
            {
                problems.Add($"{name}.json: empty document");
                return null;
            }
            if (table.Version != SupportedVersion)
                problems.Add($"{name}.json: version {table.Version} is not supported (expected {SupportedVersion})");
            return table;
        }

        static void ValidateClasses(ClassesTable table, HashSet<string> ids, List<string> problems)
        {
            const string file = ClassesTable + ".json";
            if (table.Classes.Count == 0)
                problems.Add($"{file}: no classes");

            for (int i = 0; i < table.Classes.Count; i++)
            {
                var c = table.Classes[i];
                if (string.IsNullOrEmpty(c.Id))
                {
                    problems.Add($"{file}: class #{i} has no id");
                    continue;
                }
                if (!ids.Add(c.Id))
                    problems.Add($"{file}: duplicate class id '{c.Id}'");
                string who = $"{file}: class '{c.Id}'";
                if (string.IsNullOrEmpty(c.Name))
                    problems.Add($"{who} has no name");
                CheckDamageType(who + " damageType", c.DamageType, problems);
                if (c.AttackPower <= 0)
                    problems.Add($"{who} attackPower {c.AttackPower} must be > 0");

                if (c.Skill == null)
                    problems.Add($"{who} has no skill");
                else
                {
                    if (string.IsNullOrEmpty(c.Skill.Id))
                        problems.Add($"{who} skill has no id");
                    CheckDamageType($"{who} skill '{c.Skill.Id}' damageType", c.Skill.DamageType, problems);
                    if (c.Skill.Power <= 0)
                        problems.Add($"{who} skill '{c.Skill.Id}' power {c.Skill.Power} must be > 0");
                    if (c.Skill.CooldownTurns < 1)
                        problems.Add($"{who} skill '{c.Skill.Id}' cooldownTurns {c.Skill.CooldownTurns} must be >= 1");
                }
                CheckClips(who, c.Clips, problems);
            }
        }

        static void ValidateEnemies(EnemiesTable table, List<string> problems)
        {
            const string file = EnemiesTable + ".json";
            if (table.Enemies.Count == 0)
                problems.Add($"{file}: no enemies");

            var ids = new HashSet<string>();
            for (int i = 0; i < table.Enemies.Count; i++)
            {
                var e = table.Enemies[i];
                if (string.IsNullOrEmpty(e.Id))
                {
                    problems.Add($"{file}: enemy #{i} has no id");
                    continue;
                }
                if (!ids.Add(e.Id))
                    problems.Add($"{file}: duplicate enemy id '{e.Id}'");
                string who = $"{file}: enemy '{e.Id}'";
                if (string.IsNullOrEmpty(e.Name))
                    problems.Add($"{who} has no name");
                if (e.Hp < 1)
                    problems.Add($"{who} hp {e.Hp} must be >= 1");
                if (e.Attack < 0 || e.Defence < 0 || e.Speed < 0)
                    problems.Add($"{who} attack/defence/speed must be >= 0");
                CheckDamageType(who + " damageType", e.DamageType, problems);
                if (e.AttackPower <= 0)
                    problems.Add($"{who} attackPower {e.AttackPower} must be > 0");
                CheckDamageType(who + " weakness", e.Weakness, problems);
                if (string.IsNullOrEmpty(e.Behaviour))
                    problems.Add($"{who} has no behaviour");
                if (e.Xp < 0 || e.Coins < 0)
                    problems.Add($"{who} xp/coins must be >= 0");
                if (e.Scale <= 0)
                    problems.Add($"{who} scale {e.Scale} must be > 0");
                CheckClips(who, e.Clips, problems);
            }
        }

        static void ValidateLevels(LevelTable table, HashSet<string>? classIds, List<string> problems)
        {
            const string file = LevelsTable + ".json";
            var xp = table.XpToReach;
            if (xp.Count != LevelCount)
                problems.Add($"{file}: xpToReach has {xp.Count} entries, expected {LevelCount}");
            if (xp.Count > 0 && xp[0] != 0)
                problems.Add($"{file}: xpToReach[0] is {xp[0]}, expected 0 (level 1 is free)");
            for (int i = 1; i < xp.Count; i++)
                if (xp[i] <= xp[i - 1])
                    problems.Add($"{file}: xpToReach[{i}] = {xp[i]} is not above xpToReach[{i - 1}] = {xp[i - 1]}");

            if (classIds != null)
                foreach (var id in classIds)
                    if (!table.Stats.ContainsKey(id))
                        problems.Add($"{file}: no stats rows for class '{id}'");

            foreach (var pair in table.Stats)
            {
                string who = $"{file}: stats '{pair.Key}'";
                if (classIds != null && !classIds.Contains(pair.Key))
                    problems.Add($"{who} is not a class id in {ClassesTable}.json");
                if (pair.Value.Count != LevelCount)
                    problems.Add($"{who} has {pair.Value.Count} rows, expected {LevelCount}");
                for (int i = 0; i < pair.Value.Count; i++)
                {
                    var row = pair.Value[i];
                    if (row.Hp < 1)
                        problems.Add($"{who} level {i + 1} hp {row.Hp} must be >= 1");
                    if (row.Attack < 0 || row.Defence < 0 || row.Speed < 0)
                        problems.Add($"{who} level {i + 1} attack/defence/speed must be >= 0");
                }
            }
        }

        static void ValidateItems(ItemsTable table, HashSet<string>? classIds, List<string> problems)
        {
            const string file = ItemsTable + ".json";
            var ids = new HashSet<string>();
            for (int i = 0; i < table.Items.Count; i++)
            {
                var item = table.Items[i];
                if (string.IsNullOrEmpty(item.Id))
                {
                    problems.Add($"{file}: item #{i} has no id");
                    continue;
                }
                if (!ids.Add(item.Id))
                    problems.Add($"{file}: duplicate item id '{item.Id}'");
                string who = $"{file}: item '{item.Id}'";
                if (string.IsNullOrEmpty(item.Name))
                    problems.Add($"{who} has no name");
                if (item.Price < 0)
                    problems.Add($"{who} price {item.Price} must be >= 0");

                if (Array.IndexOf(ItemKinds.All, item.Kind) < 0)
                {
                    problems.Add($"{who} kind '{item.Kind}' is not one of {string.Join("/", ItemKinds.All)}");
                    continue;
                }
                if (item.Kind == ItemKinds.Consumable)
                {
                    if (item.Effect == null || Array.IndexOf(ItemEffects.All, item.Effect) < 0)
                        problems.Add($"{who} effect '{item.Effect}' is not one of {string.Join("/", ItemEffects.All)}");
                    if (item.Amount <= 0)
                        problems.Add($"{who} amount {item.Amount} must be > 0");
                }
                else
                {
                    if (string.IsNullOrEmpty(item.ClassId))
                        problems.Add($"{who} has no classId");
                    else if (classIds != null && !classIds.Contains(item.ClassId))
                        problems.Add($"{who} classId '{item.ClassId}' is not a class id in {ClassesTable}.json");
                    if (item.Stat == null || Array.IndexOf(StatNames.All, item.Stat) < 0)
                        problems.Add($"{who} stat '{item.Stat}' is not one of {string.Join("/", StatNames.All)}");
                    if (item.Bonus < 1)
                        problems.Add($"{who} bonus {item.Bonus} must be >= 1");
                }
            }
        }

        static void CheckDamageType(string what, string? value, List<string> problems)
        {
            if (!DamageTypes.TryParse(value, out _))
                problems.Add($"{what} '{value}' is not one of {DamageTypes.Names}");
        }

        static void CheckClips(string who, Dictionary<string, string> clips, List<string> problems)
        {
            foreach (var state in ClipStates)
                if (!clips.TryGetValue(state, out var clip) || string.IsNullOrEmpty(clip))
                    problems.Add($"{who} clips has no '{state}'");
        }
    }
}
