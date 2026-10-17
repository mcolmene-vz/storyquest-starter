using System;
using System.Collections.Generic;

namespace Game.Core.Data
{
    /// Every table in each JSON file carries this so the loader can refuse a schema it does not know.
    public interface IVersionedTable
    {
        int Version { get; }
    }

    public sealed class GameTables
    {
        readonly Dictionary<string, ClassDefinition> classesById = new Dictionary<string, ClassDefinition>();
        readonly Dictionary<string, EnemyDefinition> enemiesById = new Dictionary<string, EnemyDefinition>();
        readonly Dictionary<string, ItemDefinition> itemsById = new Dictionary<string, ItemDefinition>();

        public GameTables(IReadOnlyList<ClassDefinition> classes, IReadOnlyList<EnemyDefinition> enemies,
            LevelTable levels, IReadOnlyList<ItemDefinition> items, HeroPresentation? heroes = null)
        {
            Classes = classes;
            Enemies = enemies;
            Levels = levels;
            Items = items;
            Heroes = heroes ?? HeroPresentation.None;
            foreach (var c in classes) classesById[c.Id] = c;
            foreach (var e in enemies) enemiesById[e.Id] = e;
            foreach (var i in items) itemsById[i.Id] = i;
        }

        public IReadOnlyList<ClassDefinition> Classes { get; }
        public IReadOnlyList<EnemyDefinition> Enemies { get; }
        public LevelTable Levels { get; }
        public IReadOnlyList<ItemDefinition> Items { get; }
        public HeroPresentation Heroes { get; }

        public ClassDefinition Class(string id) => Lookup(classesById, id, "class");
        public EnemyDefinition Enemy(string id) => Lookup(enemiesById, id, "enemy");
        public ItemDefinition Item(string id) => Lookup(itemsById, id, "item");
        public bool HasClass(string id) => classesById.ContainsKey(id);

        static T Lookup<T>(Dictionary<string, T> byId, string id, string what)
        {
            if (byId.TryGetValue(id, out var found))
                return found;
            throw new ArgumentException($"unknown {what} id '{id}'", nameof(id));
        }
    }
}
