using System;

namespace Game.Core.Battle
{
    public interface IRandom
    {
        int Next(int maxExclusive);
    }

    /// System.Random behind a seed: the same seed and commands replay the same battle (ARCHITECTURE §5).
    public sealed class SeededRandom : IRandom
    {
        readonly Random random;

        public SeededRandom(int seed)
        {
            random = new Random(seed);
        }

        public int Next(int maxExclusive) => random.Next(maxExclusive);
    }
}
