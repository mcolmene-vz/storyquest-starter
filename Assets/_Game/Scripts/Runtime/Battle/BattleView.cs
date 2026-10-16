using System;
using System.Collections.Generic;
using Game.Core.Battle;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Runtime
{
    /// The battle on screen (ARCHITECTURE §14.4): stage, one actor per combatant in Services.Battle.State, the camera
    /// rig, the effects, the HUD, the command panel, the presenter that plays the batches and the result screen.
    /// Show builds all of it; Hide stops the presenter and destroys the rest.
    public sealed class BattleView : IView
    {
        readonly FlowController flow;
        readonly List<CombatantActor> actors = new List<CombatantActor>();
        readonly ResultView result = new ResultView();
        GameObject? root;
        Transform? uiRoot;
        CommandPanel? panel;
        DebugCorner? corner;
        BattlePresenter? presenter;

        public BattleView(FlowController flow)
        {
            this.flow = flow;
        }

        public BattleStage? Stage { get; private set; }
        public BattleCameras? Cameras { get; private set; }
        public BattleHud? Hud { get; private set; }
        public IReadOnlyList<CombatantActor> Actors => actors;

        public void Show(Transform uiRoot)
        {
            this.uiRoot = uiRoot;
            root = new GameObject("BattleView");
            Stage = new BattleStage(root.transform);

            var battle = Services.Battle;
            var state = battle.State;
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

            Hud = BattleHud.Create(uiRoot, state, battle.Setup, Services.Tables, actors, root.transform);
            panel = new CommandPanel(uiRoot, Hud, Services.Tables);
            panel.Submitted += OnCommand;
            corner = DebugCorner.Create(uiRoot);
            presenter = new BattlePresenter(flow.Host, Hud, panel, Cameras, actors, new HitFx(root.transform),
                DamageNumberPool.Create(root.transform, camera), state, OnOutcome);
            battle.BatchReady += presenter.Play;
            // The start batch (BattleStarted, RoundStarted, TurnStarted) was raised before this view existed.
            presenter.Play(battle.LastEvents);
        }

        public void Hide()
        {
            if (presenter != null)
            {
                Services.Battle.BatchReady -= presenter.Play;
                // Before the actors go: it restores the time scale and clears the flash block and subscriptions.
                presenter.Stop();
            }
            presenter = null;
            result.Hide();
            if (panel != null)
            {
                panel.Submitted -= OnCommand;
                panel.Destroy();
            }
            panel = null;
            if (corner != null)
                Object.Destroy(corner.gameObject);
            corner = null;
            if (Hud != null)
                Object.Destroy(Hud.gameObject);
            Hud = null;
            Cameras?.Destroy();
            Cameras = null;
            Stage = null;
            actors.Clear();
            if (root != null)
                Object.Destroy(root);
            root = null;
            uiRoot = null;
        }

        void OnCommand(Command command)
        {
            panel?.Hide();
            Services.Battle.Submit(command);
        }

        void OnOutcome(BattleOutcome outcome)
        {
            if (uiRoot == null)
                return;
            panel?.Hide();
            var battle = Services.Battle;
            var lines = new List<string>();
            string title;
            switch (outcome)
            {
                case BattleOutcome.Victory:
                    title = "Victory!";
                    foreach (var gain in battle.LastGains)
                        lines.Add(gain.Name + "  +" + gain.Gained + " xp" + (gain.LeveledUp ? "   Level up!" : ""));
                    lines.Add("+" + battle.State.CoinsEarned + " coins");
                    break;
                case BattleOutcome.DefeatAlwaysWin:
                    title = "Knocked down!";
                    lines.Add("Try again");
                    break;
                default:
                    title = "The heroes retreat to camp";
                    lines.Add("Coins lost: " + battle.State.CoinsEarned);
                    break;
            }
            result.Show(uiRoot, title, lines, () => OnContinue(outcome));
        }

        void OnContinue(BattleOutcome outcome)
        {
            // §14.5: defeats replay the same setup and seed with full HP; Victory starts a new battle with a new seed.
            Services.Battle.Restart(keepSetupAndSeed: outcome != BattleOutcome.Victory);
            flow.Go(FlowState.Battle);
        }
    }
}
