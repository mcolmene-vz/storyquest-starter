using System;
using Game.Core;
using Game.Core.Data;
using UnityEngine;

namespace Game.Runtime
{
    /// The plain C# singletons of ARCHITECTURE §4, created once by GameBootstrap after the M0 settings.
    public static class Services
    {
        static GameTables? tables;
        static PartyRecord? party;
        static BattleService? battle;
        static AudioService? audio;

        public static GameTables Tables => tables ?? throw NotCreated();
        /// The party between battles (§15.5): heroes, coins, equipment and the consumable pool, in memory until M3.
        public static PartyRecord Party => party ?? throw NotCreated();
        public static BattleService Battle => battle ?? throw NotCreated();
        public static AudioService Audio => audio ?? throw NotCreated();

        /// host is the bootstrap object: AudioService lives on it as a MonoBehaviour for the focus and pause callbacks.
        public static void Create(GameObject host)
        {
            if (tables != null)
                throw new InvalidOperationException("Services.Create was already called");
            tables = TableLoader.Load(new ResourcesTableSource());
            party = DebugParty.CreateParty();
            battle = new BattleService(tables, party);
            audio = AudioService.Create(host);
#if SQ_PROBE
            // §15.8: probe builds only; the TestFlight build has neither this line nor the class.
            host.AddComponent<SoakProbe>();
#endif
        }

        static InvalidOperationException NotCreated() =>
            new InvalidOperationException("Services.Create has not run; GameBootstrap calls it in Awake");
    }
}
