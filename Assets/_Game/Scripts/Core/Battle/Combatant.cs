namespace Game.Core.Battle
{
    public sealed class Combatant
    {
        public Combatant(int id, string name, Team team, string definitionId, int slot, Stats stats,
            DamageType attackType, double attackPower)
        {
            Id = id;
            Name = name;
            Team = team;
            DefinitionId = definitionId;
            Slot = slot;
            Stats = stats;
            Hp = stats.MaxHp;
            AttackType = attackType;
            AttackPower = attackPower;
        }

        public int Id { get; }
        public string Name { get; }
        public Team Team { get; }
        public string DefinitionId { get; }
        public int Slot { get; }
        public Stats Stats { get; }
        public int Hp { get; set; }
        public DamageType AttackType { get; }
        public double AttackPower { get; }
        public DamageType? Weakness { get; set; }
        public SkillSpec? Skill { get; set; }
        public bool Defending { get; set; }
        public int SkillCooldown { get; set; }
        public string Behaviour { get; set; } = "";
        public int XpReward { get; set; }
        public int CoinReward { get; set; }

        public bool IsAlive => Hp > 0;

        // Every reference member is immutable (string, Stats, SkillSpec), so a shallow copy is a full copy.
        public Combatant Clone() => (Combatant)MemberwiseClone();
    }
}
