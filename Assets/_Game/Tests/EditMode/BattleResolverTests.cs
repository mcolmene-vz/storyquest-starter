using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Core.Battle;
using Game.Core.Data;
using NUnit.Framework;

namespace Game.Tests
{
    /// The eight BattleResolver cases of ARCHITECTURE §14.7. Fixtures are explicit Stats built in code; only the
    /// determinism test loads the real tables, which also proves they pass TableLoader.
    public class BattleResolverTests
    {
        static readonly Stats Plain = new Stats(100, 10, 3, 5);

        static Combatant Hero(int id, int slot = 0, Stats? stats = null, DamageType type = DamageType.Slash, double power = 1.0) =>
            new Combatant(id, "Hero" + id, Team.Hero, "test_hero", slot, stats ?? Plain, type, power)
            {
                Skill = new SkillSpec("test_skill", type, 1.5, 3),
            };

        static Combatant Enemy(int id, int slot = 0, Stats? stats = null, DamageType? weakness = null, int xp = 20, int coins = 10) =>
            new Combatant(id, "Troll" + id, Team.Enemy, "test_troll", slot, stats ?? Plain, DamageType.Blunt, 1.0)
            {
                Weakness = weakness,
                Behaviour = EnemyBrain.AttackRandomHero,
                XpReward = xp,
                CoinReward = coins,
            };

        static BattleState Battle(params Combatant[] combatants) => new BattleState(combatants,
            new Dictionary<string, int> { ["potion"] = 2, ["revive"] = 1 },
            new Dictionary<string, ItemSpec>
            {
                ["potion"] = new ItemSpec("potion", ItemEffect.Heal, 25),
                ["revive"] = new ItemSpec("revive", ItemEffect.Revive, 0.5),
            });

        [Test]
        public void Attack_DamageIsAttackTimesPowerMinusDefence_FlooredAtOne()
        {
            var rng = new SeededRandom(1);
            // 9 x 1.5 = 13.5 -> 13, minus defence 3 = 10
            var hero = Hero(1, stats: new Stats(100, 9, 3, 9), power: 1.5);
            var enemy = Enemy(2, stats: new Stats(100, 10, 3, 1));
            var start = BattleResolver.Start(Battle(hero, enemy), rng);
            var step = BattleResolver.Resolve(start.State, Command.Attack(1, 2), rng);

            Assert.That(step.Events[0], Is.EqualTo(new Attacked(1, 2, 10, false, false, null)));
            Assert.That(step.State.Get(2).Hp, Is.EqualTo(90));
            Assert.That(start.State.Get(2).Hp, Is.EqualTo(100), "Resolve must not mutate its input");

            // 2 x 1.0 = 2, minus defence 9 -> floored at 1
            var weak = Hero(1, stats: new Stats(100, 2, 3, 9));
            var armoured = Enemy(2, stats: new Stats(100, 10, 9, 1));
            var floor = BattleResolver.Resolve(BattleResolver.Start(Battle(weak, armoured), rng).State, Command.Attack(1, 2), rng);
            Assert.That(floor.Events[0], Is.EqualTo(new Attacked(1, 2, 1, false, false, null)));
        }

        [Test]
        public void Attack_OnWeakness_DoublesDamageAndFlagsEvent()
        {
            var rng = new SeededRandom(1);
            var fighter = Hero(1, slot: 0, stats: new Stats(100, 10, 3, 9), type: DamageType.Blunt);
            var swordsman = Hero(2, slot: 1, stats: new Stats(100, 10, 3, 8), type: DamageType.Slash);
            var troll = Enemy(3, stats: new Stats(200, 10, 3, 1), weakness: DamageType.Blunt);
            var state = BattleResolver.Start(Battle(fighter, swordsman, troll), rng).State;

            var blunt = BattleResolver.Resolve(state, Command.Attack(1, 3), rng);
            Assert.That(blunt.Events[0], Is.EqualTo(new Attacked(1, 3, 14, true, false, null)));

            var slash = BattleResolver.Resolve(blunt.State, Command.Attack(2, 3), rng);
            Assert.That(slash.Events[0], Is.EqualTo(new Attacked(2, 3, 7, false, false, null)));
            Assert.That(slash.State.Get(3).Hp, Is.EqualTo(200 - 14 - 7));
        }

        [Test]
        public void RoundStarted_OrdersBySpeed_HeroesFirstOnTie_ThenSlot()
        {
            var step = BattleResolver.Start(Battle(
                Hero(1, slot: 0, stats: new Stats(100, 10, 3, 5)),
                Hero(2, slot: 1, stats: new Stats(100, 10, 3, 8)),
                Hero(5, slot: 2, stats: new Stats(100, 10, 3, 5)),
                Enemy(3, slot: 0, stats: new Stats(100, 10, 3, 8)),
                Enemy(4, slot: 1, stats: new Stats(100, 10, 3, 5))), new SeededRandom(1));

            Assert.That(step.Events, Is.EqualTo(new BattleEvent[]
            {
                new BattleStarted(),
                new RoundStarted(1, new[] { 2, 3, 1, 5, 4 }),
                new TurnStarted(2),
            }));
            Assert.That(step.State.ActiveId, Is.EqualTo(2));
            Assert.That(step.State.Round, Is.EqualTo(1));
        }

        [Test]
        public void Defend_HalvesEveryHitUntilDefendersNextTurn()
        {
            var rng = new SeededRandom(1);
            var hero = Hero(1, stats: new Stats(100, 10, 3, 9));
            // Each troll hit on the hero is 10 - 3 = 7 before Defend.
            var trollA = Enemy(2, slot: 0, stats: new Stats(500, 10, 3, 5));
            var trollB = Enemy(3, slot: 1, stats: new Stats(500, 10, 3, 4));
            var state = BattleResolver.Start(Battle(hero, trollA, trollB), rng).State;

            var defend = BattleResolver.Resolve(state, Command.Defend(1), rng);
            Assert.That(defend.Events, Is.EqualTo(new BattleEvent[] { new Defended(1), new TurnStarted(2) }));
            Assert.That(defend.State.Get(1).Defending, Is.True);

            var first = BattleResolver.Resolve(defend.State, Command.Attack(2, 1), rng);
            Assert.That(first.Events[0], Is.EqualTo(new Attacked(2, 1, 3, false, false, null)), "7 halved and floored");
            var second = BattleResolver.Resolve(first.State, Command.Attack(3, 1), rng);
            Assert.That(second.Events[0], Is.EqualTo(new Attacked(3, 1, 3, false, false, null)), "every hit is halved, not only the first");
            Assert.That(second.Events.Skip(1), Is.EqualTo(new BattleEvent[] { new RoundStarted(2, new[] { 1, 2, 3 }), new TurnStarted(1) }));
            Assert.That(second.State.Get(1).Defending, Is.False, "cleared at the defender's own TurnStarted");

            var heroTurn = BattleResolver.Resolve(second.State, Command.Attack(1, 2), rng);
            var full = BattleResolver.Resolve(heroTurn.State, Command.Attack(2, 1), rng);
            Assert.That(full.Events[0], Is.EqualTo(new Attacked(2, 1, 7, false, false, null)));
            Assert.That(full.State.Get(1).Hp, Is.EqualTo(100 - 3 - 3 - 7));
        }

        [Test]
        public void Victory_WhenLastEnemyFalls_CarriesXpAndCoins()
        {
            var rng = new SeededRandom(1);
            var hero = Hero(1, stats: new Stats(100, 50, 3, 9));
            var trollA = Enemy(2, slot: 0, stats: new Stats(30, 10, 2, 5), xp: 20, coins: 10);
            var trollB = Enemy(3, slot: 1, stats: new Stats(30, 10, 2, 4), xp: 15, coins: 5);
            var state = BattleResolver.Start(Battle(hero, trollA, trollB), rng).State;

            var firstKill = BattleResolver.Resolve(state, Command.Attack(1, 2), rng);
            Assert.That(firstKill.Events, Is.EqualTo(new BattleEvent[]
                { new Attacked(1, 2, 48, false, false, null), new Defeated(2), new TurnStarted(3) }));
            Assert.That(firstKill.State.Phase, Is.EqualTo(BattlePhase.Active));

            var trollTurn = BattleResolver.Resolve(firstKill.State, Command.Attack(3, 1), rng);
            Assert.That(trollTurn.Events.Last(), Is.EqualTo(new TurnStarted(1)));

            var win = BattleResolver.Resolve(trollTurn.State, Command.Attack(1, 3), rng);
            Assert.That(win.Events, Is.EqualTo(new BattleEvent[]
                { new Attacked(1, 3, 48, false, false, null), new Defeated(3), new Victory(35, 15) }));
            Assert.That(win.State.Phase, Is.EqualTo(BattlePhase.Won));
            Assert.That(win.State.XpEarned, Is.EqualTo(35));
            Assert.That(win.State.CoinsEarned, Is.EqualTo(15));
            Assert.Throws<InvalidOperationException>(() => BattleResolver.Resolve(win.State, Command.Attack(1, 3), rng));
        }

        [Test]
        public void PartyDefeated_WhenLastHeroFalls()
        {
            var rng = new SeededRandom(1);
            var heroA = Hero(1, slot: 0, stats: new Stats(10, 5, 0, 5));
            var heroB = Hero(2, slot: 1, stats: new Stats(10, 5, 0, 4));
            var troll = Enemy(3, stats: new Stats(1000, 50, 0, 9));
            var state = BattleResolver.Start(Battle(heroA, heroB, troll), rng).State;
            Assert.That(state.ActiveId, Is.EqualTo(3));

            var firstDown = BattleResolver.Resolve(state, Command.Attack(3, 1), rng);
            Assert.That(firstDown.Events, Is.EqualTo(new BattleEvent[]
                { new Attacked(3, 1, 50, false, false, null), new Defeated(1), new TurnStarted(2) }));

            var heroTurn = BattleResolver.Resolve(firstDown.State, Command.Attack(2, 3), rng);
            Assert.That(heroTurn.Events.Skip(1), Is.EqualTo(new BattleEvent[] { new RoundStarted(2, new[] { 3, 2 }), new TurnStarted(3) }),
                "the knocked-down hero is out of the next round's order");

            var lost = BattleResolver.Resolve(heroTurn.State, Command.Attack(3, 2), rng);
            Assert.That(lost.Events, Is.EqualTo(new BattleEvent[]
                { new Attacked(3, 2, 50, false, false, null), new Defeated(2), new PartyDefeated() }));
            Assert.That(lost.State.Phase, Is.EqualTo(BattlePhase.Lost));
            Assert.Throws<InvalidOperationException>(() => BattleResolver.Resolve(lost.State, Command.Attack(3, 2), rng));
        }

        [Test]
        public void Skill_IsRefusedWhileOnCooldown_AndReturnsAfterNTurns()
        {
            var rng = new SeededRandom(1);
            // Skill: 10 x 1.5 = 15, minus defence 3 = 12; cooldownTurns 3 -> used on turn 1, back on turn 4.
            var hero = Hero(1, stats: new Stats(100, 10, 3, 9));
            var troll = Enemy(2, stats: new Stats(1000, 10, 3, 1));
            var state = BattleResolver.Start(Battle(hero, troll), rng).State;

            var used = BattleResolver.Resolve(state, Command.Skill(1, 2), rng);
            Assert.That(used.Events[0], Is.EqualTo(new Attacked(1, 2, 12, false, false, "test_skill")));
            Assert.That(used.State.Get(1).SkillCooldown, Is.EqualTo(3));

            state = TrollSwings(used.State, rng);
            Assert.That(state.Get(1).SkillCooldown, Is.EqualTo(2));
            Assert.Throws<ArgumentException>(() => BattleResolver.Resolve(state, Command.Skill(1, 2), rng));

            state = TrollSwings(BattleResolver.Resolve(state, Command.Attack(1, 2), rng).State, rng);
            Assert.That(state.Get(1).SkillCooldown, Is.EqualTo(1));
            Assert.Throws<ArgumentException>(() => BattleResolver.Resolve(state, Command.Skill(1, 2), rng));

            state = TrollSwings(BattleResolver.Resolve(state, Command.Attack(1, 2), rng).State, rng);
            Assert.That(state.Get(1).SkillCooldown, Is.EqualTo(0));
            var again = BattleResolver.Resolve(state, Command.Skill(1, 2), rng);
            Assert.That(again.Events[0], Is.EqualTo(new Attacked(1, 2, 12, false, false, "test_skill")));
        }

        static BattleState TrollSwings(BattleState state, IRandom rng)
        {
            Assert.That(state.ActiveId, Is.EqualTo(2));
            return BattleResolver.Resolve(state, Command.Attack(2, 1), rng).State;
        }

        [Test]
        public void Resolve_SameSeedAndCommands_ProduceIdenticalEvents()
        {
            var tables = TableLoader.Load(new FileTableSource(Path.Combine(UnityEngine.Application.dataPath, "_Game/Resources/Data")));
            var setup = new BattleSetup(
                new[]
                {
                    new HeroSpec("swordsman", 1, "Swordsman"), new HeroSpec("archer", 1, "Archer"),
                    new HeroSpec("mage", 1, "Mage"), new HeroSpec("fighter", 1, "Fighter"),
                },
                new[] { "troll_grunt" },
                new Dictionary<string, int> { ["potion"] = 2, ["revive"] = 1 },
                1);

            var first = PlayOut(setup, tables);
            var second = PlayOut(setup, tables);

            Assert.That(first, Is.EqualTo(second));
            Assert.That(first.First(), Is.EqualTo(new BattleStarted()));
            Assert.That(first.Last(), Is.InstanceOf<Victory>().Or.InstanceOf<PartyDefeated>());
            Assert.That(first.OfType<Attacked>().Any(a => a.Attacker == 4), "the grunt (id 4) took at least one random swing");
        }

        /// Heroes use the skill when it is ready and Attack otherwise; the grunt plays attack_random_hero.
        static List<BattleEvent> PlayOut(BattleSetup setup, GameTables tables)
        {
            var rng = new SeededRandom(setup.Seed);
            var step = BattleResolver.Start(BattleFactory.Create(setup, tables), rng);
            var events = new List<BattleEvent>(step.Events);
            int guard = 0;
            while (step.State.Phase == BattlePhase.Active)
            {
                Assert.That(++guard, Is.LessThan(200), "the battle did not end");
                var state = step.State;
                var actor = state.Get(state.ActiveId);
                Command command;
                if (actor.Team == Team.Enemy)
                    command = BattleResolver.EnemyCommand(state, rng);
                else
                {
                    int target = state.Living(Team.Enemy).First().Id;
                    command = actor.SkillCooldown == 0 ? Command.Skill(actor.Id, target) : Command.Attack(actor.Id, target);
                }
                step = BattleResolver.Resolve(state, command, rng);
                events.AddRange(step.Events);
            }
            return events;
        }

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
