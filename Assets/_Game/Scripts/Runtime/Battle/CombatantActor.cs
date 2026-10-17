using System;
using Game.Core;
using Game.Core.Battle;
using Game.Core.Data;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Runtime
{
    /// One spawned combatant prefab (ARCHITECTURE §14.4): the root the presenter moves, the Animator it cross-fades,
    /// the bones the effects attach to, and the renderers the flash touches, all looked up once at spawn.
    public sealed class CombatantActor
    {
        public const string PrefabFolder = "Prefabs/Combatants/";

        CombatantActor(int id, GameObject instance, Vector3 slotPosition)
        {
            Id = id;
            Root = instance.transform;
            SlotPosition = slotPosition;
            Animator = instance.GetComponentInChildren<Animator>()
                       ?? throw new InvalidOperationException(instance.name + " has no Animator; regenerate the prefab");
            Relay = instance.GetComponentInChildren<AnimationEventRelay>()
                    ?? throw new InvalidOperationException(instance.name + " has no AnimationEventRelay; regenerate the prefab");
            Renderers = instance.GetComponentsInChildren<Renderer>();
            BodyHeight = MeasureBodyHeight(Renderers);

            if (!Animator.isHuman)
                throw new InvalidOperationException(instance.name + " has no Humanoid avatar; regenerate the prefab");

            // Every combatant is Synty and Humanoid (ARCHITECTURE §15.7, §15.16.1): the avatar names the bones, whatever
            // the rig calls them.
            var model = Animator.transform;
            Chest = Animator.GetBoneTransform(HumanBodyBones.Chest) ?? Missing(instance.name, nameof(HumanBodyBones.Chest), model);
            Head = Animator.GetBoneTransform(HumanBodyBones.Head) ?? Missing(instance.name, nameof(HumanBodyBones.Head), model);
            HandR = Animator.GetBoneTransform(HumanBodyBones.RightHand);
        }

        public int Id { get; }
        public Transform Root { get; }
        public Animator Animator { get; }
        public AnimationEventRelay Relay { get; }
        public Transform Chest { get; }
        public Transform Head { get; }
        public Transform? HandR { get; }
        public Vector3 SlotPosition { get; }
        public Renderer[] Renderers { get; }
        /// The active body renderers' bounds height at spawn, weapons excluded (§15.16.2's anchor rule). Weapons are the
        /// only mesh renderers on a combatant; every body part is skinned.
        public float BodyHeight { get; }

        /// An enemy, or a hero as its prefab stands: base outfit and starter weapon.
        public static CombatantActor Spawn(int id, string prefabId, Vector3 position, Quaternion rotation, Transform parent) =>
            new CombatantActor(id, InstantiatePrefab(prefabId, position, rotation, parent), position);

        /// A hero: its {ClassId}_{Body} variant (§15.3), dressed in its equipment (§15.4) before the renderers are cached,
        /// so the flash covers exactly what is visible.
        public static CombatantActor Spawn(int id, HeroSpec hero, GameTables tables, Vector3 position, Quaternion rotation, Transform parent)
        {
            var instance = InstantiatePrefab(hero.ClassId + "_" + hero.Body, position, rotation, parent);
            HeroAppearance.Apply(instance.transform, tables.Class(hero.ClassId), hero.Body, hero.Equipment ?? Equipment.None, tables);
            return new CombatantActor(id, instance, position);
        }

        static GameObject InstantiatePrefab(string prefabId, Vector3 position, Quaternion rotation, Transform parent)
        {
            var prefab = Resources.Load<GameObject>(PrefabFolder + prefabId);
            if (prefab == null)
                throw new InvalidOperationException("Resources/" + PrefabFolder + prefabId + ".prefab is missing; run Game.Editor.CombatantGenerator.Generate");
            var instance = Object.Instantiate(prefab, position, rotation, parent);
            instance.name = prefabId;
            return instance;
        }

        static float MeasureBodyHeight(Renderer[] renderers)
        {
            float min = float.MaxValue, max = float.MinValue;
            foreach (var renderer in renderers)
            {
                if (!(renderer is SkinnedMeshRenderer))
                    continue;
                var bounds = renderer.bounds;
                min = Mathf.Min(min, bounds.min.y);
                max = Mathf.Max(max, bounds.max.y);
            }
            return max > min ? max - min : 0f;
        }

        static Transform Missing(string prefabId, string bone, Transform fallback)
        {
            Debug.LogError("[CombatantActor] " + prefabId + " has no bone '" + bone + "'; effects fall back to the model root");
            return fallback;
        }
    }
}
