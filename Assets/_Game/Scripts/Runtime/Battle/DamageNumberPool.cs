using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Runtime
{
    /// Eight world-space TextMeshPro numbers built once when the battle view shows (ARCHITECTURE §14.4 step 10),
    /// reused round-robin: pop 0 -> 1.3 -> 1 over 0.15 s, rise 0.6 m over 0.8 s, fade over the last 0.3 s, billboarded
    /// to the camera every frame while active. A MonoBehaviour only for LateUpdate; no serialized fields.
    public sealed class DamageNumberPool : MonoBehaviour
    {
        public const int Size = 8;
        // 3D TMP: 1 point = 0.1 m, so digits stand ~0.29 m, about 55 px on the iPhone at the troll's depth in the group
        // shot (measured 2026-10-16: 2.4 gave ~30 px there).
        const float FontSize = 4.0f;
        const float WeaknessScale = 1.4f;
        const float PopSeconds = 0.15f;
        const float PopPeak = 1.3f;
        const float PopPeakFraction = 2f / 3f;
        const float RiseMetres = 0.6f;
        const float LifeSeconds = 0.8f;
        const float FadeSeconds = 0.3f;
        // The chest point is inside the body; the number is pulled towards the camera so the body never covers it.
        const float TowardsCamera = 0.6f;
        static readonly Vector2 TextArea = new Vector2(4f, 1f);
        static readonly Color NormalColour = Color.white;
        static readonly Color WeaknessColour = new Color(1f, 0.88f, 0.2f, 1f);
        static readonly Color HealColour = new Color(0.4f, 1f, 0.45f, 1f);

        sealed class Entry
        {
            public TMP_Text Text = null!;
            public Transform Transform = null!;
            public Vector3 Anchor;
            public float Start;
            public float Scale;
            public bool Active;
        }

        readonly Entry[] entries = new Entry[Size];
        int next;
        int activeCount;
        Camera cam = null!;

        public static DamageNumberPool Create(Transform parent, Camera camera)
        {
            var go = new GameObject("DamageNumbers");
            go.transform.SetParent(parent, false);
            var pool = go.AddComponent<DamageNumberPool>();
            pool.cam = camera;
            for (int i = 0; i < Size; i++)
                pool.entries[i] = pool.Build(i);
            return pool;
        }

        public void Show(Vector3 point, int value, bool isWeakness)
        {
            var entry = Take();
            entry.Text.color = isWeakness ? WeaknessColour : NormalColour;
            entry.Scale = isWeakness ? WeaknessScale : 1f;
            entry.Text.SetText("{0}", value);
            Launch(entry, point);
        }

        public void ShowHeal(Vector3 point, int value)
        {
            var entry = Take();
            entry.Text.color = HealColour;
            entry.Scale = 1f;
            entry.Text.SetText("+{0}", value);
            Launch(entry, point);
        }

        Entry Build(int index)
        {
            var go = new GameObject("DamageNumber", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var text = go.AddComponent<TextMeshPro>();
            text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = FontSize;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.rectTransform.sizeDelta = TextArea;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            go.SetActive(false);
            return new Entry { Text = text, Transform = go.transform };
        }

        Entry Take()
        {
            var entry = entries[next];
            next = (next + 1) % Size;
            if (!entry.Active)
                activeCount++;
            return entry;
        }

        void Launch(Entry entry, Vector3 point)
        {
            entry.Anchor = point;
            entry.Start = Time.time;
            entry.Active = true;
            entry.Text.alpha = 1f;
            Animate(entry, 0f);
            entry.Text.gameObject.SetActive(true);
        }

        void LateUpdate()
        {
            if (activeCount == 0)
                return;
            float now = Time.time;
            for (int i = 0; i < Size; i++)
            {
                var entry = entries[i];
                if (!entry.Active)
                    continue;
                float t = now - entry.Start;
                if (t >= LifeSeconds)
                {
                    entry.Active = false;
                    activeCount--;
                    entry.Text.gameObject.SetActive(false);
                    continue;
                }
                Animate(entry, t);
            }
        }

        void Animate(Entry entry, float t)
        {
            float pop;
            if (t >= PopSeconds)
                pop = 1f;
            else if (t < PopSeconds * PopPeakFraction)
                pop = PopPeak * (t / (PopSeconds * PopPeakFraction));
            else
                pop = Mathf.Lerp(PopPeak, 1f, (t - PopSeconds * PopPeakFraction) / (PopSeconds * (1f - PopPeakFraction)));
            entry.Transform.localScale = Vector3.one * (pop * entry.Scale);

            var camTransform = cam.transform;
            var position = entry.Anchor + Vector3.up * (RiseMetres * (t / LifeSeconds));
            position += (camTransform.position - position).normalized * TowardsCamera;
            entry.Transform.SetPositionAndRotation(position, camTransform.rotation);

            float fadeStart = LifeSeconds - FadeSeconds;
            entry.Text.alpha = t > fadeStart ? 1f - (t - fadeStart) / FadeSeconds : 1f;
        }
    }
}
