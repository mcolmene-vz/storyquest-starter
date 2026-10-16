using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Runtime
{
    /// Ground, ambient fill and the slot layout of ARCHITECTURE §14.4. Built in code when BattleView shows.
    public sealed class BattleStage
    {
        public const float HeroX = -2.4f;
        public const float EnemyX = 2.4f;
        const float SlotSpacing = 1.2f;
        // 100 m: the group camera sits ~10 m out at a 12 degree pitch, and a 30 m plane put the grass edge across the heads.
        const float GroundScale = 10f;
        const string GroundMaterial = "Materials/Toon/ground";
        static readonly Color AmbientFill = new Color(0.55f, 0.60f, 0.70f, 1f);

        public BattleStage(Transform parent)
        {
            Root = new GameObject("Stage").transform;
            Root.SetParent(parent, false);

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(Root, false);
            ground.transform.localScale = Vector3.one * GroundScale;
            // Nothing in the battle has physics (§5); the primitive's MeshCollider goes before the first frame.
            var collider = ground.GetComponent<Collider>();
            if (collider != null)
                Object.Destroy(collider);
            var material = Resources.Load<Material>(GroundMaterial);
            if (material == null)
                Debug.LogError("[BattleStage] Resources/" + GroundMaterial + ".mat is missing; run Game.Editor.CombatantGenerator.Generate");
            else
                ground.GetComponent<Renderer>().sharedMaterial = material;

            // A flat fill is what the toon shader's SampleSH expects; no skybox, no fog.
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = AmbientFill;
        }

        public Transform Root { get; }

        public static Vector3 Centre => Vector3.zero;
        public static Quaternion HeroFacing => Quaternion.LookRotation(Vector3.right);
        public static Quaternion EnemyFacing => Quaternion.LookRotation(Vector3.left);

        public static Vector3 HeroSlot(int index, int count) => new Vector3(HeroX, 0f, SlotZ(index, count));
        public static Vector3 EnemySlot(int index, int count) => new Vector3(EnemyX, 0f, SlotZ(index, count));

        // Four slots sit at z = -1.8, -0.6, 0.6, 1.8; fewer keep the spacing and stay centred on z = 0.
        static float SlotZ(int index, int count) => (index - (count - 1) * 0.5f) * SlotSpacing;
    }
}
