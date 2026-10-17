using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Runtime
{
    /// One spawned combatant prefab (ARCHITECTURE §14.4): the root the presenter moves, the Animator it cross-fades,
    /// the bones the effects attach to, and the renderers the flash touches, all looked up once at spawn.
    public sealed class CombatantActor
    {
        public const string PrefabFolder = "Prefabs/Combatants/";
        const string ChestBone = "chest";
        const string HeadBone = "head";
        const string HandRBone = "handslot.r";

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

            var model = Animator.transform;
            if (Animator.isHuman)
            {
                // Synty heroes (ARCHITECTURE §15.7): the avatar names the bones, whatever the rig calls them.
                Chest = Animator.GetBoneTransform(HumanBodyBones.Chest) ?? Missing(instance.name, nameof(HumanBodyBones.Chest), model);
                Head = Animator.GetBoneTransform(HumanBodyBones.Head) ?? Missing(instance.name, nameof(HumanBodyBones.Head), model);
                HandR = Animator.GetBoneTransform(HumanBodyBones.RightHand);
            }
            else
            {
                Chest = FindBone(model, ChestBone) ?? Missing(instance.name, ChestBone, model);
                Head = FindBone(model, HeadBone) ?? Missing(instance.name, HeadBone, model);
                // The troll's mannequin rig has no hand slots; weapons and hand effects check for null.
                HandR = FindBone(model, HandRBone);
            }
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

        public static CombatantActor Spawn(int id, string prefabId, Vector3 position, Quaternion rotation, Transform parent)
        {
            var prefab = Resources.Load<GameObject>(PrefabFolder + prefabId);
            if (prefab == null)
                throw new InvalidOperationException("Resources/" + PrefabFolder + prefabId + ".prefab is missing; run Game.Editor.CombatantGenerator.Generate");
            var instance = Object.Instantiate(prefab, position, rotation, parent);
            instance.name = prefabId;
            return new CombatantActor(id, instance, position);
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

        static Transform? FindBone(Transform parent, string name)
        {
            if (parent.name == name)
                return parent;
            for (int i = 0; i < parent.childCount; i++)
            {
                var found = FindBone(parent.GetChild(i), name);
                if (found != null)
                    return found;
            }
            return null;
        }
    }
}
