using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Runtime
{
    /// The two-shot rig of ARCHITECTURE §14.4 on the M0 main camera: CM_Group frames every combatant through a
    /// target group, CM_Action is re-posed high behind the attacker's shoulder per action. Built in code when BattleView shows.
    /// Components added at runtime skip Cinemachine's Reset(), so every value the inspector would have set is set here.
    public sealed class BattleCameras
    {
        public const string GroupName = "CM_Group";
        public const string ActionName = "CM_Action";
        const int GroupPriority = 10;
        const int ActionPriority = 20;
        const float DefaultBlendSeconds = 0.25f;
        const float ActionToGroupBlendSeconds = 0.4f;
        const float MemberWeight = 1f;
        // Anchors sit at chest height with a 1.2 m radius (y -0.2..2.2), covering every hero the generator allows.
        // A combatant taller than TallBody gets both scaled to its height (§15.16.2, §15.16.8), the troll today and M4's boss.
        const float AnchorHeight = 1.0f;
        const float MemberRadius = 1.2f;
        const float TallBody = 2.2f;
        const float TallAnchorPerMetre = 0.45f;
        const float TallRadiusPerMetre = 0.55f;
        const float GroupFramingSize = 0.75f;
        const float GroupDamping = 0.3f;
        const float GroupFov = 30f;
        // §15.18.8 item 3, ruled after D6b step 3's re-measure: FOV 26 with the offsets below at 1.4x M1's.
        const float ActionFov = 26f;
        const float ImpulseDuration = 0.2f;
        // Three-quarter view from the party's rear-left (§14.4, amended 2026-10-16); FOV is fixed, the framing dollies.
        static readonly Vector3 GroupPose = new Vector3(-6.6f, 3.2f, -7.9f);
        static readonly Vector3 GroupEuler = new Vector3(12f, 40f, 0f);
        // Half-frame units; positive y lifts the group (the package moves the camera down by this amount); negative x
        // moves it left so no combatant sits under a party card on 4:3 (§14.4, amended after D6).
        static readonly Vector2 GroupCenterOffset = new Vector2(-0.10f, 0.22f);
        static readonly Vector3 ImpulseVelocity = new Vector3(0f, -0.25f, 0f);
        // §14.4 (amended after D6): high behind the attacker's weapon shoulder, scaled by the attacker's root so the
        // troll's scale fits; the aim is the attacker-target midpoint at an unscaled height, over every neighbour's head.
        // §15.18.8 item 3: M1's 3.0 / 0.3 / 2.8 scaled by 1.4 about the attacker, so the Synty attacker and its weapon
        // take less of the frame while the narrower lens keeps the target's size.
        const float ShoulderBack = 4.2f;
        const float ShoulderRight = 0.42f;
        const float ShoulderUp = 3.92f;
        // §15.18.8 item 3: 1.5 (was 1.4) keeps the troll's head below the enemy card on the iPhone; not scaled.
        const float AimHeight = 1.5f;

        readonly CinemachineBrain brain;
        readonly CinemachineBlenderSettings blends;

        public BattleCameras(Camera mainCamera, IReadOnlyList<CombatantActor> actors, Transform parent)
        {
            Root = new GameObject("Cameras").transform;
            Root.SetParent(parent, false);

            if (!mainCamera.TryGetComponent(out brain))
                brain = mainCamera.gameObject.AddComponent<CinemachineBrain>();
            brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, DefaultBlendSeconds);
            blends = ScriptableObject.CreateInstance<CinemachineBlenderSettings>();
            blends.CustomBlends = new[]
            {
                new CinemachineBlenderSettings.CustomBlend
                {
                    From = GroupName, To = ActionName,
                    Blend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f),
                },
                new CinemachineBlenderSettings.CustomBlend
                {
                    From = ActionName, To = GroupName,
                    Blend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, ActionToGroupBlendSeconds),
                },
            };
            brain.CustomBlends = blends;

            var groupGo = new GameObject("TargetGroup");
            groupGo.transform.SetParent(Root, false);
            TargetGroup = groupGo.AddComponent<CinemachineTargetGroup>();
            TargetGroup.PositionMode = CinemachineTargetGroup.PositionModes.GroupCenter;
            TargetGroup.RotationMode = CinemachineTargetGroup.RotationModes.Manual;
            TargetGroup.UpdateMethod = CinemachineTargetGroup.UpdateMethods.LateUpdate;
            foreach (var actor in actors)
            {
                float h = actor.BodyHeight;
                bool tall = h > TallBody;
                float anchorHeight = tall ? Mathf.Max(AnchorHeight, TallAnchorPerMetre * h) : AnchorHeight;
                float radius = tall ? Mathf.Max(MemberRadius, TallRadiusPerMetre * h) : MemberRadius;
                TargetGroup.AddMember(CreateAnchor(actor, anchorHeight), MemberWeight, radius);
            }

            Group = CreateCamera(GroupName, GroupPriority);
            Group.transform.SetPositionAndRotation(GroupPose, Quaternion.Euler(GroupEuler));
            Group.Lens.FieldOfView = GroupFov;
            Group.Target.TrackingTarget = TargetGroup.transform;
            var framing = Group.gameObject.AddComponent<CinemachineGroupFraming>();
            framing.FramingMode = CinemachineGroupFraming.FramingModes.Vertical;
            framing.FramingSize = GroupFramingSize;
            framing.SizeAdjustment = CinemachineGroupFraming.SizeAdjustmentModes.DollyOnly;
            framing.LateralAdjustment = CinemachineGroupFraming.LateralAdjustmentModes.ChangePosition;
            framing.CenterOffset = GroupCenterOffset;
            framing.Damping = GroupDamping;
            framing.DollyRange = new Vector2(-100f, 100f);

            // No Position/Rotation Control components: PoseOverShoulder sets the transform itself per action.
            Action = CreateCamera(ActionName, 0);
            Action.Lens.FieldOfView = ActionFov;

            Impulse = Root.gameObject.AddComponent<CinemachineImpulseSource>();
            Impulse.ImpulseDefinition = new CinemachineImpulseDefinition
            {
                ImpulseChannel = 1,
                ImpulseShape = CinemachineImpulseDefinition.ImpulseShapes.Rumble,
                CustomImpulseShape = new AnimationCurve(),
                ImpulseDuration = ImpulseDuration,
                ImpulseType = CinemachineImpulseDefinition.ImpulseTypes.Uniform,
                DissipationDistance = 100f,
                DissipationRate = 0.25f,
                PropagationSpeed = 343f,
            };
            Impulse.DefaultVelocity = ImpulseVelocity;
            GroupShotLive = true;
        }

        /// True while CM_Group has priority; SoakProbe averages GPU time over these frames only (§15.8).
        public static bool GroupShotLive { get; private set; }

        public Transform Root { get; }
        public CinemachineCamera Group { get; }
        public CinemachineCamera Action { get; }
        public CinemachineTargetGroup TargetGroup { get; }
        public CinemachineImpulseSource Impulse { get; }

        public void PoseOverShoulder(CombatantActor attacker, CombatantActor target)
        {
            var from = attacker.Root;
            float s = from.lossyScale.y;
            var position = from.position - from.forward * (ShoulderBack * s) + from.right * (ShoulderRight * s) + Vector3.up * (ShoulderUp * s);
            var aim = (from.position + target.Root.position) * 0.5f + Vector3.up * AimHeight;
            Action.transform.SetPositionAndRotation(position, Quaternion.LookRotation(aim - position));
        }

        public void SetActionActive(bool active)
        {
            Action.Priority = active ? ActionPriority : 0;
            GroupShotLive = !active;
        }

        // World offset, not local: the troll's root scale must not lift its anchor again. Parented so it follows knockback.
        static Transform CreateAnchor(CombatantActor actor, float height)
        {
            var anchor = new GameObject("GroupAnchor").transform;
            anchor.SetParent(actor.Root, false);
            anchor.position = actor.Root.position + Vector3.up * height;
            return anchor;
        }

        public void Destroy()
        {
            GroupShotLive = false;
            if (brain != null && brain.CustomBlends == blends)
                brain.CustomBlends = null;
            Object.Destroy(blends);
            if (Root != null)
                Object.Destroy(Root.gameObject);
        }

        CinemachineCamera CreateCamera(string name, int priority)
        {
            var go = new GameObject(name);
            go.transform.SetParent(Root, false);
            var camera = go.AddComponent<CinemachineCamera>();
            camera.Priority = priority;
            camera.Lens = LensSettings.Default;

            var listener = go.AddComponent<CinemachineImpulseListener>();
            listener.ApplyAfter = CinemachineCore.Stage.Noise;
            listener.ChannelMask = 1;
            listener.Gain = 1f;
            listener.Use2DDistance = false;
            listener.UseCameraSpace = true;
            listener.SignalCombinationMode = CinemachineImpulseListener.SignalCombinationModes.Additive;
            listener.ReactionSettings = new CinemachineImpulseListener.ImpulseReaction
            {
                AmplitudeGain = 1f,
                FrequencyGain = 1f,
                Duration = 1f,
            };
            return camera;
        }
    }
}
