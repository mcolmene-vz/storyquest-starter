using System;
using System.Collections.Generic;
using Game.Core.Battle;
using Game.Core.Data;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Game.Runtime
{
    /// Party cards, enemy card, turn banner and the ring under the active combatant (ARCHITECTURE §14.5). Values
    /// arrive through the event applier (D5) or the presenter (D6), never by reading Core mid-batch; HP bars tween
    /// 0.3 s from the moment their event is applied. A MonoBehaviour only for Update; it has no serialized fields.
    public sealed class BattleHud : MonoBehaviour
    {
        const float Margin = 24f;
        const float CardWidth = 300f;
        const float CardHeight = 170f;
        const float CardGap = 12f;
        const float EnemyCardWidth = 480f;
        const float EnemyCardHeight = 130f;
        const float BannerWidth = 560f;
        const float BannerHeight = 110f;
        const float HpTweenSeconds = 0.3f;
        const float RingSize = 1.4f;
        const float RingLift = 0.02f;
        const string GroundMaterial = "Materials/Toon/ground";
        static readonly Color RingColour = new Color(1f, 0.85f, 0.1f, 1f);
        static readonly Color KnockedDownColour = new Color(0.25f, 0.08f, 0.08f, 0.85f);
        static readonly Color DimText = new Color(0.8f, 0.85f, 0.9f, 1f);
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");

        sealed class Entry
        {
            public int Id;
            public string Name = "";
            public int MaxHp;
            public int Hp;
            public int CooldownTurns;
            public UiBar Bar = null!;
            public TMP_Text HpText = null!;
            public UiButton? Card;
            public GameObject? DefTag;
            public TMP_Text? ReadyLine;
            public Vector3 SlotPosition;
            public float TweenFrom;
            public float TweenStart = -1f;
        }

        readonly List<Entry> entries = new List<Entry>();
        readonly Dictionary<int, Entry> byId = new Dictionary<int, Entry>();
        TMP_Text banner = null!;
        Transform ring = null!;
        Material ringMaterial = null!;

        public event Action<int>? CardTapped;

        public static BattleHud Create(Transform safeArea, BattleState state, BattleSetup setup, GameTables tables,
            IReadOnlyList<CombatantActor> actors, Transform worldParent)
        {
            var root = UiFactory.Panel(safeArea, Vector2.zero, Vector2.one, Vector2.zero, Color.clear);
            root.name = "Hud";
            var hud = root.gameObject.AddComponent<BattleHud>();
            hud.Build(state, setup, tables, actors, worldParent);
            return hud;
        }

        void Build(BattleState state, BattleSetup setup, GameTables tables, IReadOnlyList<CombatantActor> actors, Transform worldParent)
        {
            var bannerPanel = UiFactory.Panel(transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(BannerWidth, BannerHeight), UiFactory.PanelColour);
            bannerPanel.name = "TurnBanner";
            bannerPanel.anchoredPosition = new Vector2(Margin, -Margin);
            banner = UiFactory.Label(bannerPanel, "", 44f, TextAlignmentOptions.Left);
            banner.rectTransform.offsetMin = new Vector2(24f, 0f);

            int heroCount = 0;
            foreach (var c in state.Combatants)
                if (c.Team == Team.Hero)
                    heroCount++;

            int heroIndex = 0;
            foreach (var c in state.Combatants)
            {
                var entry = new Entry { Id = c.Id, Name = c.Name, MaxHp = c.Stats.MaxHp, Hp = c.Hp, SlotPosition = FindSlot(actors, c.Id) };
                if (c.Team == Team.Hero)
                {
                    var cls = tables.Class(c.DefinitionId);
                    BuildCard(entry, cls.Name, setup.Heroes[c.Slot].Level, cls.Skill?.Name ?? "", heroCount - 1 - heroIndex++);
                }
                else
                {
                    BuildEnemyCard(entry);
                }
                entries.Add(entry);
                byId[c.Id] = entry;
                Refresh(entry, false);
            }

            BuildRing(worldParent);
        }

        // rowFromBottom 0 is the lowest card; slot 0 ends up on top.
        void BuildCard(Entry entry, string className, int level, string skillName, int rowFromBottom)
        {
            int id = entry.Id;
            var card = UiFactory.Button(transform, "", new Vector2(CardWidth, CardHeight), () => CardTapped?.Invoke(id));
            card.Rect.name = "Card_" + className;
            // Cards are not greyed out when they are not targets, so the disabled tint is the normal one and
            // SetTargetable paints the highlight itself.
            card.Place(new Vector2(1f, 0f), new Vector2(-Margin, Margin + rowFromBottom * (CardHeight + CardGap)));
            card.Image.color = UiFactory.PanelColour;
            var colours = card.Button.colors;
            colours.disabledColor = Color.white;
            card.Button.colors = colours;
            card.Interactable = false;
            card.Label.gameObject.SetActive(false);
            entry.Card = card;

            var name = UiFactory.Label(card.Rect, className, 34f, TextAlignmentOptions.Left);
            SetRect(name.rectTransform, new Vector2(0f, 1f), new Vector2(14f, -6f), new Vector2(190f, 44f));
            var levelLabel = UiFactory.Label(card.Rect, "Lv " + level, 28f, TextAlignmentOptions.Right);
            SetRect(levelLabel.rectTransform, new Vector2(1f, 1f), new Vector2(-14f, -6f), new Vector2(70f, 44f));

            // On the HP row's right end: the name row is full with a long class name beside "Lv n".
            var def = UiFactory.Panel(card.Rect, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(64f, 30f), UiFactory.HighlightColour);
            def.name = "DefTag";
            def.anchoredPosition = new Vector2(-14f, -82f);
            UiFactory.Label(def, "DEF", 22f, TextAlignmentOptions.Center);
            def.gameObject.SetActive(false);
            entry.DefTag = def.gameObject;

            entry.Bar = UiFactory.Bar(card.Rect, new Vector2(CardWidth - 28f, 22f));
            SetRect(entry.Bar.Rect, new Vector2(0f, 1f), new Vector2(14f, -56f), new Vector2(CardWidth - 28f, 22f));
            entry.HpText = UiFactory.Label(card.Rect, "", 26f, TextAlignmentOptions.Left);
            SetRect(entry.HpText.rectTransform, new Vector2(0f, 1f), new Vector2(14f, -82f), new Vector2(200f, 30f));

            var skillLine = UiFactory.Label(card.Rect, skillName, 22f, TextAlignmentOptions.Left);
            skillLine.color = DimText;
            SetRect(skillLine.rectTransform, new Vector2(0f, 1f), new Vector2(14f, -112f), new Vector2(CardWidth - 28f, 26f));
            entry.ReadyLine = UiFactory.Label(card.Rect, "", 22f, TextAlignmentOptions.Left);
            entry.ReadyLine.color = UiFactory.HighlightColour;
            SetRect(entry.ReadyLine.rectTransform, new Vector2(0f, 1f), new Vector2(14f, -138f), new Vector2(CardWidth - 28f, 26f));
            entry.CooldownTurns = 0;
        }

        void BuildEnemyCard(Entry entry)
        {
            var panel = UiFactory.Panel(transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(EnemyCardWidth, EnemyCardHeight), UiFactory.PanelColour);
            panel.name = "EnemyCard";
            panel.anchoredPosition = new Vector2(0f, -Margin);
            var name = UiFactory.Label(panel, entry.Name, 36f, TextAlignmentOptions.Center);
            SetRect(name.rectTransform, new Vector2(0f, 1f), new Vector2(0f, -8f), new Vector2(EnemyCardWidth, 44f));
            entry.Bar = UiFactory.Bar(panel, new Vector2(EnemyCardWidth - 40f, 24f));
            SetRect(entry.Bar.Rect, new Vector2(0f, 1f), new Vector2(20f, -60f), new Vector2(EnemyCardWidth - 40f, 24f));
            entry.HpText = UiFactory.Label(panel, "", 24f, TextAlignmentOptions.Right);
            SetRect(entry.HpText.rectTransform, new Vector2(1f, 1f), new Vector2(-20f, -88f), new Vector2(200f, 30f));
        }

        void BuildRing(Transform worldParent)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "TurnRing";
            quad.transform.SetParent(worldParent, false);
            quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            quad.transform.localScale = new Vector3(RingSize, RingSize, 1f);
            var collider = quad.GetComponent<Collider>();
            if (collider != null)
                Object.Destroy(collider);

            // The toon shader is the only one guaranteed to be in the build; a flat yellow quad on the ground reads
            // fully lit under the key light, so it is the flat ring without a second shader.
            var ground = Resources.Load<Material>(GroundMaterial);
            if (ground == null)
                throw new InvalidOperationException("Resources/" + GroundMaterial + ".mat is missing; run Game.Editor.CombatantGenerator.Generate");
            ringMaterial = new Material(ground) { name = "TurnRing" };
            ringMaterial.SetColor(BaseColorId, RingColour);
            ringMaterial.SetFloat(OutlineWidthId, 0f);
            var renderer = quad.GetComponent<Renderer>();
            renderer.sharedMaterial = ringMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            ring = quad.transform;
            quad.SetActive(false);
        }

        public void SetActive(int id)
        {
            var entry = byId[id];
            banner.text = entry.Name + "'s turn";
            ring.position = entry.SlotPosition + Vector3.up * RingLift;
            ring.gameObject.SetActive(true);
        }

        public void Damage(int id, int amount) => SetHp(id, byId[id].Hp - amount);

        public void Heal(int id, int amount) => SetHp(id, byId[id].Hp + amount);

        public void SetHp(int id, int hp)
        {
            var entry = byId[id];
            hp = Mathf.Clamp(hp, 0, entry.MaxHp);
            if (hp == entry.Hp)
                return;
            entry.TweenFrom = entry.Bar.Value;
            entry.TweenStart = Time.time;
            entry.Hp = hp;
            Refresh(entry, true);
        }

        public void SetDefending(int id, bool on)
        {
            var tag = byId[id].DefTag;
            if (tag != null)
                tag.SetActive(on);
        }

        public void SetCooldown(int id, int turns)
        {
            var entry = byId[id];
            entry.CooldownTurns = Mathf.Max(0, turns);
            if (entry.ReadyLine != null)
                entry.ReadyLine.text = entry.CooldownTurns > 0 ? "ready in " + entry.CooldownTurns : "";
        }

        public void DecrementCooldown(int id) => SetCooldown(id, byId[id].CooldownTurns - 1);

        public void SetKnockedDown(int id, bool down)
        {
            var card = byId[id].Card;
            if (card != null)
                card.Image.color = down ? KnockedDownColour : UiFactory.PanelColour;
        }

        /// Only cards in ids respond to taps; null clears the target step.
        public void SetTargetable(ICollection<int>? ids)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry.Card == null)
                    continue;
                bool targetable = ids != null && ids.Contains(entry.Id);
                entry.Card.Interactable = targetable;
                if (targetable)
                    entry.Card.Image.color = UiFactory.HighlightColour;
                else
                    SetKnockedDown(entry.Id, entry.Hp <= 0);
            }
        }

        /// A snapshot at the end of a batch may correct drift; it never runs mid-batch (§14.4).
        public void Reconcile(BattleState state)
        {
            foreach (var c in state.Combatants)
            {
                if (!byId.TryGetValue(c.Id, out var entry))
                    continue;
                SetHp(c.Id, c.Hp);
                SetDefending(c.Id, c.Defending);
                SetCooldown(c.Id, c.SkillCooldown);
                SetKnockedDown(c.Id, !c.IsAlive);
            }
        }

        void Update()
        {
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry.TweenStart < 0f)
                    continue;
                float t = (Time.time - entry.TweenStart) / HpTweenSeconds;
                float target = entry.MaxHp > 0 ? (float)entry.Hp / entry.MaxHp : 0f;
                if (t >= 1f)
                {
                    entry.Bar.Value = target;
                    entry.TweenStart = -1f;
                }
                else
                {
                    entry.Bar.Value = Mathf.Lerp(entry.TweenFrom, target, t);
                }
            }
        }

        void OnDestroy()
        {
            if (ring != null)
                Object.Destroy(ring.gameObject);
            if (ringMaterial != null)
                Object.Destroy(ringMaterial);
        }

        void Refresh(Entry entry, bool tween)
        {
            entry.HpText.SetText("{0}/{1}", entry.Hp, entry.MaxHp);
            if (!tween)
                entry.Bar.Value = entry.MaxHp > 0 ? (float)entry.Hp / entry.MaxHp : 0f;
        }

        static Vector3 FindSlot(IReadOnlyList<CombatantActor> actors, int id)
        {
            for (int i = 0; i < actors.Count; i++)
                if (actors[i].Id == id)
                    return actors[i].SlotPosition;
            throw new ArgumentException("no actor for combatant " + id, nameof(id));
        }

        static void SetRect(RectTransform rect, Vector2 anchor, Vector2 inset, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = inset;
            rect.sizeDelta = size;
        }
    }
}
