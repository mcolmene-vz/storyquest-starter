using System;
using Game.Core.Data;
using UnityEngine;

namespace Game.Runtime
{
    /// The plain C# singletons of ARCHITECTURE §4, created once by GameBootstrap after the M0 settings.
    public static class Services
    {
        static GameTables? tables;
        static BattleService? battle;
        static AudioService? audio;

        public static GameTables Tables => tables ?? throw NotCreated();
        public static BattleService Battle => battle ?? throw NotCreated();
        public static AudioService Audio => audio ?? throw NotCreated();

        /// host is the bootstrap object: AudioService lives on it as a MonoBehaviour for the focus and pause callbacks.
        public static void Create(GameObject host)
        {
            if (tables != null)
                throw new InvalidOperationException("Services.Create was already called");
            tables = TableLoader.Load(new ResourcesTableSource());
            battle = new BattleService(tables);
            audio = AudioService.Create(host);
        }

        static InvalidOperationException NotCreated() =>
            new InvalidOperationException("Services.Create has not run; GameBootstrap calls it in Awake");
    }
}
