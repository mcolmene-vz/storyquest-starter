using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core.Battle;
using Game.Core.Data;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Runtime
{
    /// Attack / Skill / Item / Defend, bottom-left (ARCHITECTURE §14.5). Shown for a hero's TurnStarted with a snapshot
    /// taken at the end of that batch, hidden while a batch applies. Attack and Skill auto-target the only living enemy
    /// and fire on the first tap; items go through an item row and then the party cards. Every illegal command is
    /// withheld here so Core never has to throw.
    public sealed class CommandPanel
    {
        const float Margin = 24f;
        const float ButtonWidth = 280f;
        const float ButtonHeight = 180f;
        const float Gap = 24f;
        const float CancelWidth = 170f;
        const float HintWidth = 560f;

        readonly BattleHud hud;
        readonly RectTransform root;
        readonly RectTransform mainRow;
        readonly RectTransform itemRow;
        readonly RectTransform targetRow;
        readonly UiButton skillButton;
        readonly UiButton itemButton;
        readonly List<KeyValuePair<ItemDefinition, UiButton>> itemButtons = new List<KeyValuePair<ItemDefinition, UiButton>>();
        readonly TMP_Text targetHint;
        int heroId = -1;
        BattleState? snapshot;
        ItemDefinition? pendingItem;

        public event Action<Command>? Submitted;

        public CommandPanel(Transform safeArea, BattleHud hud, GameTables tables)
        {
            this.hud = hud;
            root = UiFactory.Panel(safeArea, Vector2.zero, Vector2.zero, new Vector2(4 * ButtonWidth + 3 * Gap, ButtonHeight), Color.clear);
            root.name = "CommandPanel";
            root.anchoredPosition = new Vector2(Margin, Margin);

            mainRow = Row("MainRow");
            PlaceInRow(UiFactory.Button(mainRow, "Attack", new Vector2(ButtonWidth, ButtonHeight), OnAttack), 0f);
            skillButton = UiFactory.Button(mainRow, "Skill", new Vector2(ButtonWidth, ButtonHeight), OnSkill);
            PlaceInRow(skillButton, ButtonWidth + Gap);
            itemButton = UiFactory.Button(mainRow, "Item", new Vector2(ButtonWidth, ButtonHeight), OnItem);
            PlaceInRow(itemButton, 2 * (ButtonWidth + Gap));
            PlaceInRow(UiFactory.Button(mainRow, "Defend", new Vector2(ButtonWidth, ButtonHeight), OnDefend), 3 * (ButtonWidth + Gap));

            itemRow = Row("ItemRow");
            float x = 0f;
            foreach (var item in tables.Items)
            {
                if (item.Kind != ItemKinds.Consumable)
                    continue;
                var captured = item;
                var button = UiFactory.Button(itemRow, item.Name, new Vector2(ButtonWidth, ButtonHeight), () => OnItemChosen(captured));
                PlaceInRow(button, x);
                itemButtons.Add(new KeyValuePair<ItemDefinition, UiButton>(item, button));
                x += ButtonWidth + Gap;
            }
            PlaceInRow(UiFactory.Button(itemRow, "Cancel", new Vector2(CancelWidth, ButtonHeight), ShowMain), x);
            itemRow.gameObject.SetActive(false);

            targetRow = Row("TargetRow");
            var hintPanel = UiFactory.Panel(targetRow, Vector2.zero, Vector2.zero, new Vector2(HintWidth, ButtonHeight), UiFactory.PanelColour);
            targetHint = UiFactory.Label(hintPanel, "", 40f, TextAlignmentOptions.Center);
            PlaceInRow(UiFactory.Button(targetRow, "Cancel", new Vector2(CancelWidth, ButtonHeight), ShowMain), HintWidth + Gap);
            targetRow.gameObject.SetActive(false);

            hud.CardTapped += OnCardTapped;
            root.gameObject.SetActive(false);
        }

        public bool IsShown => root.gameObject.activeSelf;

        public void ShowFor(int hero, BattleState state)
        {
            heroId = hero;
            snapshot = state;
            var actor = state.Get(hero);

            int cooldown = actor.SkillCooldown;
            skillButton.SetText(cooldown > 0 ? "Skill\n<size=60%>ready in " + cooldown + "</size>" : "Skill");
            skillButton.Interactable = cooldown == 0 && actor.Skill != null;

            bool anyItem = false;
            foreach (var pair in itemButtons)
            {
                state.Inventory.TryGetValue(pair.Key.Id, out int count);
                bool usable = count > 0 && Targets(pair.Key, state).Count > 0;
                pair.Value.SetText(pair.Key.Name + "\n<size=60%>x" + count + "</size>");
                pair.Value.Interactable = usable;
                anyItem |= usable;
            }
            itemButton.Interactable = anyItem;

            ShowMain();
            root.gameObject.SetActive(true);
        }

        public void Hide()
        {
            pendingItem = null;
            hud.SetTargetable(null);
            root.gameObject.SetActive(false);
        }

        public void Destroy()
        {
            hud.CardTapped -= OnCardTapped;
            if (root != null)
                Object.Destroy(root.gameObject);
        }

        void ShowMain()
        {
            pendingItem = null;
            hud.SetTargetable(null);
            mainRow.gameObject.SetActive(true);
            itemRow.gameObject.SetActive(false);
            targetRow.gameObject.SetActive(false);
        }

        void OnAttack()
        {
            if (!IsShown)
                return;
            Fire(Command.Attack(heroId, LivingEnemy()));
        }

        void OnSkill()
        {
            if (!IsShown)
                return;
            Fire(Command.Skill(heroId, LivingEnemy()));
        }

        void OnDefend()
        {
            if (!IsShown)
                return;
            Fire(Command.Defend(heroId));
        }

        void OnItem()
        {
            mainRow.gameObject.SetActive(false);
            itemRow.gameObject.SetActive(true);
        }

        void OnItemChosen(ItemDefinition item)
        {
            pendingItem = item;
            itemRow.gameObject.SetActive(false);
            targetRow.gameObject.SetActive(true);
            targetHint.text = item.Effect == ItemEffects.Revive ? "Tap a fallen hero" : "Tap a hero";
            hud.SetTargetable(Targets(item, Snapshot));
        }

        void OnCardTapped(int targetId)
        {
            if (!IsShown || pendingItem == null)
                return;
            var item = pendingItem;
            if (!Targets(item, Snapshot).Contains(targetId))
                return;
            Fire(Command.Item(heroId, targetId, item.Id));
        }

        void Fire(Command command)
        {
            Services.Audio.Play(SfxId.MenuTap);
            Hide();
            Submitted?.Invoke(command);
        }

        int LivingEnemy() => Snapshot.Living(Team.Enemy).First().Id;

        BattleState Snapshot => snapshot ?? throw new InvalidOperationException("CommandPanel used before ShowFor");

        // Potion lists living heroes, revive knocked-down ones (§14.5).
        static List<int> Targets(ItemDefinition item, BattleState state)
        {
            bool revive = item.Effect == ItemEffects.Revive;
            var ids = new List<int>();
            foreach (var c in state.Combatants)
                if (c.Team == Team.Hero && c.IsAlive != revive)
                    ids.Add(c.Id);
            return ids;
        }

        RectTransform Row(string name)
        {
            var row = UiFactory.Panel(root, Vector2.zero, Vector2.one, Vector2.zero, Color.clear);
            row.name = name;
            return row;
        }

        static void PlaceInRow(UiButton button, float x) => button.Place(Vector2.zero, new Vector2(x, 0f));
    }
}
