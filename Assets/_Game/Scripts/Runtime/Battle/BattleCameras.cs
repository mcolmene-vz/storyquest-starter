using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Runtime
{
    /// The two-shot rig of ARCHITECTURE §14.4 on the M0 main camera: CM_Group frames every combatant through a
    /// target group, CM_Action is re-posed over the attacker's shoulder per action. Built in code when BattleView shows.
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
        // Anchors sit at chest height with a 1.2 m radius (y -0.2..2.2), covering every body the generator allows.
        const float AnchorHeight = 1.0f;
        const float MemberRadius = 1.2f;
        const float GroupFramingSize = 0.95f;
        const float GroupDamping = 0.3f;
        const float GroupFov = 30f;
        const float ActionFov = 32f;
        const float ImpulseDuration = 0.2f;
        // Three-quarter view from the party's rear-left (§14.4, amended 2026-10-16); FOV is fixed, the framing dollies.
        static readonly Vector3 GroupPose = new Vector3(-6.6f, 3.2f, -7.9f);
        static readonly Vector3 GroupEuler = new Vector3(12f, 40f, 0f);
        // Half-frame units; positive y lifts the group (the package moves the camera down by this amount).
        static readonly Vector2 GroupCenterOffset = new Vector2(0f, 0.30f);
        static readonly Vector3 ImpulseVelocity = new Vector3(0f, -0.25f, 0f);
        // §14.4: camera behind and to the right of the attacker, aim 70 % of the way to the target at chest height.
        const float ShoulderBack = 2.0f;
        const float ShoulderRight = 0.9f;
        const float ShoulderUp = 1.5f;
        const float AimFraction = 0.7f;
        const float AimHeight = 1.1f;

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
                TargetGroup.AddMember(CreateAnchor(actor), MemberWeight, MemberRadius);

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

            ActionAim = new GameObject("ActionAim").transform;
            ActionAim.SetParent(Root, false);
            Action = CreateCamera(ActionName, 0);
            Action.Lens.FieldOfView = ActionFov;
            Action.Target.TrackingTarget = ActionAim;
            var composer = Action.gameObject.AddComponent<CinemachineRotationComposer>();
            composer.TargetOffset = Vector3.zero;
            composer.Lookahead = new LookaheadSettings();
            composer.Damping = new Vector2(0.5f, 0.5f);
            composer.Composition = ScreenComposerSettings.Default;
            composer.CenterOnActivate = true;

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
        }

        public Transform Root { get; }
        public CinemachineCamera Group { get; }
        public CinemachineCamera Action { get; }
        public CinemachineTargetGroup TargetGroup { get; }
        public Transform ActionAim { get; }
        public CinemachineImpulseSource Impulse { get; }

        public void PoseOverShoulder(CombatantActor attacker, CombatantActor target)
        {
            var from = attacker.Root;
            Action.transform.position = from.position - from.forward * ShoulderBack + from.right * ShoulderRight + Vector3.up * ShoulderUp;
            var aim = Vector3.Lerp(from.position, target.Root.position, AimFraction);
            aim.y = AimHeight;
            ActionAim.position = aim;
            Action.transform.LookAt(aim);
        }

        public void SetActionActive(bool active) => Action.Priority = active ? ActionPriority : 0;

        // World offset, not local: the troll's 1.35 root scale must not lift its anchor. Parented so it follows knockback.
        static Transform CreateAnchor(CombatantActor actor)
        {
            var anchor = new GameObject("GroupAnchor").transform;
            anchor.SetParent(actor.Root, false);
            anchor.position = actor.Root.position + Vector3.up * AnchorHeight;
            return anchor;
        }

        public void Destroy()
        {
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
