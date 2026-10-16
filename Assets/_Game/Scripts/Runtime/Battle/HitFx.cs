using System;
using Game.Core.Battle;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Runtime
{
    public enum HitFxKind
    {
        Slash,
        Pierce,
        Magic,
        Blunt,
        Heal,
        Defend,
    }

    /// The one particle system of ARCHITECTURE §14.4 step 8, built in code when the battle view shows. Burst moves it
    /// to the hit point, sets the start colour for the damage type and emits; nothing else ever runs on it.
    public sealed class HitFx
    {
        public const int BurstCount = 12;
        public const int DefendBurstCount = 6;
        const string MaterialPath = "Materials/Toon/particle";
        const int MaxParticles = 64;
        const float ShapeRadius = 0.2f;
        const float GravityModifier = 0.5f;
        static readonly Vector2 Lifetime = new Vector2(0.3f, 0.4f);
        static readonly Vector2 Speed = new Vector2(2f, 3f);
        static readonly Vector2 Size = new Vector2(0.08f, 0.15f);
        static readonly Color SlashColour = new Color(1f, 0.95f, 0.6f, 1f);
        static readonly Color PierceColour = new Color(0.85f, 0.86f, 0.9f, 1f);
        static readonly Color MagicColour = new Color(0.62f, 0.35f, 1f, 1f);
        static readonly Color BluntColour = new Color(1f, 0.55f, 0.15f, 1f);
        static readonly Color HealColour = new Color(0.35f, 0.95f, 0.4f, 1f);
        static readonly Color DefendColour = Color.white;

        readonly ParticleSystem system;
        readonly Transform transform;

        public HitFx(Transform parent)
        {
            var go = new GameObject("HitFx");
            transform = go.transform;
            transform.SetParent(parent, false);
            system = go.AddComponent<ParticleSystem>();

            var main = system.main;
            main.loop = false;
            main.playOnAwake = false;
            main.maxParticles = MaxParticles;
            main.startLifetime = new ParticleSystem.MinMaxCurve(Lifetime.x, Lifetime.y);
            main.startSpeed = new ParticleSystem.MinMaxCurve(Speed.x, Speed.y);
            main.startSize = new ParticleSystem.MinMaxCurve(Size.x, Size.y);
            main.gravityModifier = GravityModifier;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = Color.white;

            // Bursts come from Emit only; the emission module would otherwise stream particles while playing.
            var emission = system.emission;
            emission.enabled = false;

            var shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = ShapeRadius;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            var material = Resources.Load<Material>(MaterialPath);
            if (material == null)
                Debug.LogError("[HitFx] Resources/" + MaterialPath + ".mat is missing; run Game.Editor.CombatantGenerator.Generate");
            else
                renderer.sharedMaterial = material;
            // The component started playing on AddComponent with the defaults; a stopped system still simulates what Emit spawns.
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        public static HitFxKind KindOf(DamageType type)
        {
            switch (type)
            {
                case DamageType.Slash: return HitFxKind.Slash;
                case DamageType.Pierce: return HitFxKind.Pierce;
                case DamageType.Magic: return HitFxKind.Magic;
                case DamageType.Blunt: return HitFxKind.Blunt;
                default: throw new ArgumentOutOfRangeException(nameof(type), type, "no particle colour for this damage type");
            }
        }

        public void Burst(Vector3 point, HitFxKind kind, int count = BurstCount)
        {
            transform.position = point;
            var main = system.main;
            main.startColor = Colour(kind);
            system.Emit(count);
        }

        static Color Colour(HitFxKind kind)
        {
            switch (kind)
            {
                case HitFxKind.Slash: return SlashColour;
                case HitFxKind.Pierce: return PierceColour;
                case HitFxKind.Magic: return MagicColour;
                case HitFxKind.Blunt: return BluntColour;
                case HitFxKind.Heal: return HealColour;
                default: return DefendColour;
            }
        }
    }
}
