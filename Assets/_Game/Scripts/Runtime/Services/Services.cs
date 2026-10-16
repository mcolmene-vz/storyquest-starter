using System;
using Game.Core.Data;

namespace Game.Runtime
{
    /// The plain C# singletons of ARCHITECTURE §4, created once by GameBootstrap after the M0 settings.
    public static class Services
    {
        static GameTables? tables;
        static BattleService? battle;

        public static GameTables Tables => tables ?? throw NotCreated();
        public static BattleService Battle => battle ?? throw NotCreated();

        public static void Create()
        {
            if (tables != null)
                throw new InvalidOperationException("Services.Create was already called");
            tables = TableLoader.Load(new ResourcesTableSource());
            battle = new BattleService(tables);
        }

        static InvalidOperationException NotCreated() =>
            new InvalidOperationException("Services.Create has not run; GameBootstrap calls it in Awake");
    }
}
