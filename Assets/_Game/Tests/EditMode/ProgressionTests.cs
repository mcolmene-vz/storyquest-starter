using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Core;
using Game.Core.Battle;
using Game.Core.Data;
using NUnit.Framework;

namespace Game.Tests
{
    /// The five Progression cases of ARCHITECTURE §15.6. Tests 1–4 read the real tables through a file-backed source;
    /// test 5 builds its tables and party in code so each refusal is isolated from tuning.
    public class ProgressionTests
    {
        GameTables tables = null!;

        [OneTimeSetUp]
        public void LoadTables()
        {
            tables = TableLoader.Load(new FileTableSource(Path.Combine(UnityEngine.Application.dataPath, "_Game/Resources/Data")));
        }

        [Test]
        public void LevelFor_UsesCumulativeThresholds_AndCapsAtTen()
        {
            var xp = tables.Levels.XpToReach;
            Assert.That(Progression.LevelFor(0, xp), Is.EqualTo(1));
            Assert.That(Progression.LevelFor(19, xp), Is.EqualTo(1));
            Assert.That(Progression.LevelFor(20, xp), Is.EqualTo(2));
            Assert.That(Progression.LevelFor(540, xp), Is.EqualTo(10));
            Assert.That(Progression.LevelFor(9999, xp), Is.EqualTo(10));
        }

        [Test]
        public void StatsFor_ReadsTheClassRowForTheLevel()
        {
            // The rows of levels.json; an M4 tuning pass updates these numbers with the table.
            Assert.That(Progression.StatsFor("swordsman", 1, tables), Is.EqualTo(new Stats(40, 9, 4, 5)));
            Assert.That(Progression.StatsFor("archer", 4, tables), Is.EqualTo(new Stats(43, 11, 6, 9)));
            Assert.That(Progression.StatsFor("mage", 10, tables), Is.EqualTo(new Stats(66, 20, 11, 9)));
        }

        [Test]
        public void HeroStats_AddsEquippedBonuses_StarterAddsNothing()
        {
            var swordsman = Progression.StatsFor("swordsman", 1, tables);
            Assert.That(Progression.HeroStats("swordsman", 1, Equipment.None, tables), Is.EqualTo(swordsman));

            var kitted = Progression.HeroStats("swordsman", 1, new Equipment("sword_2", "helm_swordsman", "charm_tough"), tables);
            Assert.That(kitted, Is.EqualTo(swordsman with
            {
                MaxHp = swordsman.MaxHp + tables.Item("charm_tough").Bonus,
                Attack = swordsman.Attack + tables.Item("sword_2").Bonus,
                Defence = swordsman.Defence + tables.Item("helm_swordsman").Bonus,
            }));

            var archer = Progression.StatsFor("archer", 3, tables);
            Assert.That(Progression.HeroStats("archer", 3, new Equipment(null, null, "charm_swift"), tables),
                Is.EqualTo(archer with { Speed = archer.Speed + tables.Item("charm_swift").Bonus }));
        }

        [Test]
        public void Buy_Equipment_SpendsOwnsAndEquips_Consumable_AddsToPool()
        {
            var party = new PartyRecord
            {
                Heroes =
                {
                    new HeroRecord { Name = "Swordsman", ClassId = "swordsman", Body = Bodies.Girl, Coins = 60 },
                    new HeroRecord { Name = "Archer", ClassId = "archer", Coins = 60 },
                },
                Consumables = { ["potion"] = 2, ["revive"] = 1 },
            };

            Progression.Buy(party, 0, "sword_2", tables);
            var swordsman = party.Heroes[0];
            Assert.That(swordsman.Coins, Is.EqualTo(60 - tables.Item("sword_2").Price));
            Assert.That(swordsman.Owned, Is.EqualTo(new[] { "sword_2" }));
            Assert.That(swordsman.Equipped, Is.EqualTo(new Equipment("sword_2", null, null)), "bought equipment is worn");

            Progression.Buy(party, 1, "potion", tables);
            var archer = party.Heroes[1];
            Assert.That(archer.Coins, Is.EqualTo(60 - tables.Item("potion").Price), "the buyer pays");
            Assert.That(party.Consumables, Is.EqualTo(new Dictionary<string, int> { ["potion"] = 3, ["revive"] = 1 }));
            Assert.That(archer.Owned, Is.Empty, "consumables go to the pool, not to the hero");
            Assert.That(archer.Equipped, Is.EqualTo(Equipment.None));
            Assert.That(swordsman.Coins, Is.EqualTo(60 - tables.Item("sword_2").Price), "other heroes keep their coins");
        }

        [Test]
        public void Buy_IsRefused_WhenCoinsShort_WrongClass_OrOwned_AndChangesNothing()
        {
            var fixture = new GameTables(new List<ClassDefinition>(), new List<EnemyDefinition>(), new LevelTable(),
                new List<ItemDefinition>
                {
                    new ItemDefinition { Id = "sword", Kind = ItemKinds.Weapon, ClassId = "swordsman", Stat = StatNames.Attack, Bonus = 3, Price = 45 },
                    new ItemDefinition { Id = "bow", Kind = ItemKinds.Weapon, ClassId = "archer", Stat = StatNames.Attack, Bonus = 3, Price = 45 },
                    new ItemDefinition { Id = "potion", Kind = ItemKinds.Consumable, Effect = ItemEffects.Heal, Amount = 25, Price = 15 },
                });
            var party = new PartyRecord
            {
                Heroes =
                {
                    new HeroRecord { Name = "Short", ClassId = "swordsman", Coins = 44 },
                    new HeroRecord { Name = "Shorter", ClassId = "swordsman", Coins = 14 },
                    new HeroRecord { Name = "Rich", ClassId = "swordsman", Coins = 100, Owned = { "sword" }, Equipped = new Equipment("sword", null, null) },
                },
                Consumables = { ["potion"] = 2 },
            };
            var before = Describe(party);

            Assert.Throws<ArgumentException>(() => Progression.Buy(party, 0, "sword", fixture), "one coin short of equipment");
            Assert.Throws<ArgumentException>(() => Progression.Buy(party, 1, "potion", fixture), "one coin short of a consumable");
            Assert.Throws<ArgumentException>(() => Progression.Buy(party, 2, "bow", fixture), "another class's weapon");
            Assert.Throws<ArgumentException>(() => Progression.Buy(party, 2, "sword", fixture), "already owned");

            Assert.That(Describe(party), Is.EqualTo(before));
        }

        static string Describe(PartyRecord party) =>
            string.Join(" | ", party.Heroes.Select(h => $"{h.Name} {h.Coins} [{string.Join(",", h.Owned)}] {h.Equipped}"))
            + " | " + string.Join(",", party.Consumables.OrderBy(p => p.Key).Select(p => $"{p.Key}={p.Value}"));

        sealed class FileTableSource : ITableSource
        {
            readonly string folder;

            public FileTableSource(string folder)
            {
                this.folder = folder;
            }

            public string ReadText(string tableName) => File.ReadAllText(Path.Combine(folder, tableName + ".json"));
        }
    }
}
