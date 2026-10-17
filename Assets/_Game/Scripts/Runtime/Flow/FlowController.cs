using System;
using UnityEngine;

namespace Game.Runtime
{
    /// The one state machine of ARCHITECTURE §4: holds one active view at a time. Plain C#, owned by GameBootstrap,
    /// which is also the MonoBehaviour that views run coroutines on.
    public sealed class FlowController
    {
        readonly Transform uiRoot;
        IView? view;

        public FlowController(MonoBehaviour host, Transform uiRoot)
        {
            Host = host;
            this.uiRoot = uiRoot;
        }

        public MonoBehaviour Host { get; }
        // Static so SoakProbe can print it without holding the controller (§15.8); there is only one flow.
        public static FlowState Current { get; private set; } = FlowState.Boot;

        public void Go(FlowState next)
        {
            view?.Hide();
            view = null;
            Current = next;
            view = CreateView(next);
            view.Show(uiRoot);
        }

        IView CreateView(FlowState state) => state switch
        {
            FlowState.Camp => new CampView(this),
            FlowState.Battle => new BattleView(this),
            _ => throw new NotSupportedException("FlowState." + state + " has no view yet"),
        };
    }
}
