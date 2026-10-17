using UnityEngine;

namespace Game.Runtime
{
    /// Sits next to the Animator on every combatant prefab. OnImpact is the function name of the AnimationEvent the
    /// Synty postprocessor writes on attack clips from clips.json (ARCHITECTURE §14.2, §15.15.5); the presenter waits on
    /// Impact (§14.4 step 3).
    public sealed class AnimationEventRelay : MonoBehaviour
    {
        public event System.Action? Impact;

        public void OnImpact() => Impact?.Invoke();
    }
}
