using System;
using System.Collections.Generic;
using Game.Core.Battle;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Runtime
{
    /// The battle on screen (ARCHITECTURE §14.4): stage, one actor per combatant in Services.Battle.State, the camera
    /// rig. Show builds all of it under one root; Hide destroys the root.
    public sealed class BattleView : IView
    {
        readonly List<CombatantActor> actors = new List<CombatantActor>();
        GameObject? root;

        public BattleStage? Stage { get; private set; }
        public BattleCameras? Cameras { get; private set; }
        public IReadOnlyList<CombatantActor> Actors => actors;

        public void Show(Transform uiRoot)
        {
            root = new GameObject("BattleView");
            Stage = new BattleStage(root.transform);

            var state = Services.Battle.State;
            int heroes = 0, enemies = 0;
            foreach (var c in state.Combatants)
                if (c.Team == Team.Hero) heroes++;
                else enemies++;
            foreach (var c in state.Combatants)
            {
                bool hero = c.Team == Team.Hero;
                var slot = hero ? BattleStage.HeroSlot(c.Slot, heroes) : BattleStage.EnemySlot(c.Slot, enemies);
                var facing = hero ? BattleStage.HeroFacing : BattleStage.EnemyFacing;
                actors.Add(CombatantActor.Spawn(c.Id, c.DefinitionId, slot, facing, root.transform));
            }

            var camera = Camera.main;
            if (camera == null)
                throw new InvalidOperationException("no MainCamera; GameBootstrap creates it before the flow starts");
            Cameras = new BattleCameras(camera, actors, root.transform);
        }

        public void Hide()
        {
            Cameras?.Destroy();
            Cameras = null;
            Stage = null;
            actors.Clear();
            if (root != null)
                Object.Destroy(root);
            root = null;
        }
    }
}
