using UnityEngine;

namespace Game.Runtime
{
    /// A view builds its objects in Show and destroys them in Hide, so nothing survives a state change by accident.
    public interface IView
    {
        void Show(Transform uiRoot);
        void Hide();
    }
}
