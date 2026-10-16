using UnityEngine;

namespace Game.Runtime
{
    /// M0 placeholder only; deleted in M1 (ARCHITECTURE §13.4).
    public sealed class Spinner : MonoBehaviour
    {
        public float DegreesPerSecond { get; set; } = 90f;

        void Update() =>
            transform.Rotate(0f, DegreesPerSecond * Time.deltaTime, 0f, Space.World);
    }
}
