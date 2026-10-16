using System;
using System.Linq;

namespace Game.Core.Battle
{
    /// Picks an enemy's command from its behaviour id. M1 ships attack_random_hero only; the thrower, shaman and
    /// boss behaviours (attack_lowest_hp_hero, heal_lowest_ally_or_attack) are reserved ids, not code.
    public static class EnemyBrain
    {
        public const string AttackRandomHero = "attack_random_hero";

        public static Command Choose(BattleState state, Combatant actor, IRandom random)
        {
            switch (actor.Behaviour)
            {
                case AttackRandomHero:
                    var heroes = state.Living(Team.Hero).OrderBy(c => c.Id).ToList();
                    if (heroes.Count == 0)
                        throw new InvalidOperationException("no living hero to attack");
                    return Command.Attack(actor.Id, heroes[random.Next(heroes.Count)].Id);
                default:
                    throw new NotSupportedException(
                        $"enemy behaviour '{actor.Behaviour}' is not implemented (combatant {actor.Id}, '{actor.DefinitionId}')");
            }
        }
    }
}
