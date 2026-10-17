using System;
using System.Collections.Generic;
using Game.Core;
using Game.Core.Data;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Runtime
{
    /// The camp in M2 (ARCHITECTURE §15.5): a hero column with coins, the Shop and Equip tabs and Next battle, in a
    /// 1600 x 900 reference-px box centred in the safe area. Progression owns every rule; this view offers only the
    /// legal taps and redraws after each one. M3 adds story, backdrop and rest to the same view.
    public sealed class CampView : IView
    {
        const float RowHeight = 170f;
        const float RowStep = RowHeight + 12f;
        const float ColumnWidth = 380f;
        const float AreaWidth = 1196f;
        const float TabWidth = 300f;
        const float ItemWidth = 586f;
        const float Gap = 24f;
        const int ShopSlots = 5;
        static readonly Vector2 BoxSize = new Vector2(1600f, 900f);
        static readonly Vector2 TopLeft = new Vector2(0f, 1f);
        static readonly string[] SlotKinds = { ItemKinds.Weapon, ItemKinds.Armor, ItemKinds.Charm };
        static readonly string[] SlotNames = { "Weapon", "Armor", "Charm" };
        // Owned is settled rather than out of reach, so it gets its own colour instead of the disabled grey.
        static readonly Color OwnedColour = new Color(0.22f, 0.52f, 0.30f, 1f);

        enum Tab
        {
            Shop,
            Equip,
        }

        readonly FlowController flow;
        readonly List<UiButton> heroButtons = new List<UiButton>();
        readonly UiButton[] itemButtons = new UiButton[ShopSlots];
        readonly ItemDefinition?[] shownItems = new ItemDefinition?[ShopSlots];
        readonly UiButton[] slotButtons = new UiButton[3];
        RectTransform? root;
        RectTransform shopPage = null!;
        RectTransform equipPage = null!;
        UiButton shopTab = null!;
        UiButton equipTab = null!;
        UiButton bodyButton = null!;
        Color greyedTint;
        int selected;
        Tab tab = Tab.Shop;

        public CampView(FlowController flow)
        {
            this.flow = flow;
        }

        public void Show(Transform uiRoot)
        {
            root = UiFactory.Panel(uiRoot, Vector2.zero, Vector2.one, Vector2.zero, Color.clear);
            root.name = "CampView";
            var centre = new Vector2(0.5f, 0.5f);
            var box = UiFactory.Panel(root, centre, centre, BoxSize, Color.clear);
            box.name = "CampBox";

            var heroes = Services.Party.Heroes;
            for (int i = 0; i < heroes.Count; i++)
            {
                int hero = i;
                var button = UiFactory.Button(box, "", new Vector2(ColumnWidth, RowHeight), () => SelectHero(hero));
                button.Place(TopLeft, new Vector2(0f, -i * RowStep));
                heroButtons.Add(button);
            }
            UiFactory.Button(box, "Next battle", new Vector2(ColumnWidth, RowHeight), OnNextBattle).Place(Vector2.zero, Vector2.zero);

            var area = UiFactory.Panel(box, new Vector2(1f, 0f), Vector2.one, new Vector2(AreaWidth, 0f), Color.clear);
            area.name = "Area";
            shopTab = UiFactory.Button(area, "Shop", new Vector2(TabWidth, RowHeight), () => SelectTab(Tab.Shop));
            shopTab.Place(TopLeft, Vector2.zero);
            equipTab = UiFactory.Button(area, "Equip", new Vector2(TabWidth, RowHeight), () => SelectTab(Tab.Equip));
            equipTab.Place(TopLeft, new Vector2(TabWidth + Gap, 0f));
            bodyButton = UiFactory.Button(area, "", new Vector2(TabWidth, RowHeight), OnBody);
            bodyButton.Place(Vector2.one, Vector2.zero);

            shopPage = UiFactory.Panel(area, Vector2.zero, Vector2.one, Vector2.zero, Color.clear);
            shopPage.name = "Shop";
            for (int i = 0; i < ShopSlots; i++)
            {
                int slot = i;
                var button = UiFactory.Button(shopPage, "", new Vector2(ItemWidth, RowHeight), () => OnBuy(slot));
                // Hero equipment down the left column, the party's consumables down the right.
                int column = i < SlotKinds.Length ? 0 : 1;
                int row = i < SlotKinds.Length ? i : i - SlotKinds.Length;
                button.Place(TopLeft, new Vector2(column * (ItemWidth + Gap), -(row + 1) * RowStep));
                itemButtons[i] = button;
            }
            greyedTint = itemButtons[0].Button.colors.disabledColor;

            equipPage = UiFactory.Panel(area, Vector2.zero, Vector2.one, Vector2.zero, Color.clear);
            equipPage.name = "Equip";
            for (int i = 0; i < SlotKinds.Length; i++)
            {
                int slot = i;
                var button = UiFactory.Button(equipPage, "", new Vector2(AreaWidth, RowHeight), () => OnSlot(slot));
                button.Place(TopLeft, new Vector2(0f, -(i + 1) * RowStep));
                slotButtons[i] = button;
            }

            Refresh();
        }

        public void Hide()
        {
            if (root != null)
                Object.Destroy(root.gameObject);
            root = null;
            heroButtons.Clear();
        }

        void SelectHero(int hero)
        {
            selected = hero;
            Refresh();
        }

        void SelectTab(Tab next)
        {
            tab = next;
            Refresh();
        }

        void OnBuy(int slot)
        {
            var item = shownItems[slot];
            if (item == null)
                return;
            Progression.Buy(Services.Party, selected, item.Id, Services.Tables);
            Services.Audio.Play(SfxId.MenuTap);
            Refresh();
        }

        // Starter first, then each owned item of that kind. V1 has one item per class and kind, so a tap toggles.
        void OnSlot(int slot)
        {
            var tables = Services.Tables;
            var hero = Services.Party.Heroes[selected];
            string kind = SlotKinds[slot];
            var options = new List<string?> { null };
            foreach (var id in hero.Owned)
                if (tables.Item(id).Kind == kind)
                    options.Add(id);
            if (options.Count == 1)
                return;
            int at = options.IndexOf(Equipped(hero.Equipped, kind));
            Progression.Equip(Services.Party, selected, kind, options[(at + 1) % options.Count], tables);
            Services.Audio.Play(SfxId.MenuTap);
            Refresh();
        }

        // M2 only (§15.3): with no profiles yet, this is how all eight variants get seen. Read at the next battle.
        void OnBody()
        {
            var hero = Services.Party.Heroes[selected];
            hero.Body = hero.Body == Bodies.Girl ? Bodies.Boy : Bodies.Girl;
            Services.Audio.Play(SfxId.MenuTap);
            Refresh();
        }

        void OnNextBattle()
        {
            Services.Audio.Play(SfxId.MenuTap);
            Services.Battle.StartNext();
            flow.Go(FlowState.Battle);
        }

        void Refresh()
        {
            var party = Services.Party;
            var tables = Services.Tables;
            for (int i = 0; i < heroButtons.Count; i++)
            {
                var record = party.Heroes[i];
                heroButtons[i].SetText(record.Name + "\n<size=32>" + record.Coins + " coins</size>");
                heroButtons[i].Image.color = i == selected ? UiFactory.HighlightColour : UiFactory.ButtonColour;
            }
            shopTab.Image.color = tab == Tab.Shop ? UiFactory.HighlightColour : UiFactory.ButtonColour;
            equipTab.Image.color = tab == Tab.Equip ? UiFactory.HighlightColour : UiFactory.ButtonColour;
            shopPage.gameObject.SetActive(tab == Tab.Shop);
            equipPage.gameObject.SetActive(tab == Tab.Equip);
            bodyButton.Rect.gameObject.SetActive(tab == Tab.Equip);

            var hero = party.Heroes[selected];
            RefreshShop(hero, party, tables);
            for (int i = 0; i < SlotKinds.Length; i++)
            {
                string kind = SlotKinds[i];
                string? equipped = Equipped(hero.Equipped, kind);
                string shown = equipped != null ? tables.Item(equipped).Name : kind == ItemKinds.Charm ? "none" : "starter";
                slotButtons[i].SetText(SlotNames[i] + ": " + shown);
                slotButtons[i].Interactable = OwnsAny(hero, kind, tables);
            }
            bodyButton.SetText("Body: " + hero.Body);
        }

        void RefreshShop(HeroRecord hero, PartyRecord party, GameTables tables)
        {
            for (int i = 0; i < SlotKinds.Length; i++)
                shownItems[i] = ClassItem(hero.ClassId, SlotKinds[i], tables);
            int next = SlotKinds.Length;
            foreach (var item in tables.Items)
                if (item.Kind == ItemKinds.Consumable && next < ShopSlots)
                    shownItems[next++] = item;

            for (int i = 0; i < ShopSlots; i++)
            {
                var item = shownItems[i];
                var button = itemButtons[i];
                button.Rect.gameObject.SetActive(item != null);
                if (item == null)
                    continue;

                bool consumable = item.Kind == ItemKinds.Consumable;
                string name = item.Name;
                if (consumable)
                {
                    party.Consumables.TryGetValue(item.Id, out int count);
                    name += "  x" + count;
                }
                bool owned = !consumable && hero.Owned.Contains(item.Id);
                bool affordable = item.Price <= hero.Coins;
                string state = owned ? "Owned" : affordable ? "Buy " + item.Price : item.Price + " coins";
                button.SetText(name + "\n<size=32>" + Effect(item) + "</size>\n" + state);
                button.Interactable = !owned && affordable;
                button.Image.color = owned ? OwnedColour : UiFactory.ButtonColour;
                var colours = button.Button.colors;
                colours.disabledColor = owned ? Color.white : greyedTint;
                button.Button.colors = colours;
            }
        }

        static ItemDefinition ClassItem(string classId, string kind, GameTables tables)
        {
            foreach (var item in tables.Items)
                if (item.ClassId == classId && item.Kind == kind)
                    return item;
            throw new InvalidOperationException($"no {kind} for class '{classId}'; TableLoader requires one");
        }

        static bool OwnsAny(HeroRecord hero, string kind, GameTables tables)
        {
            foreach (var id in hero.Owned)
                if (tables.Item(id).Kind == kind)
                    return true;
            return false;
        }

        static string? Equipped(Equipment equipment, string kind) => kind switch
        {
            ItemKinds.Weapon => equipment.Weapon,
            ItemKinds.Armor => equipment.Armor,
            _ => equipment.Charm,
        };

        static string Effect(ItemDefinition item)
        {
            if (item.Effect == ItemEffects.Heal)
                return "Heals " + (int)item.Amount;
            if (item.Effect == ItemEffects.Revive)
                return item.Amount == 0.5 ? "Revives at half HP" : "Revives at " + (int)Math.Round(item.Amount * 100) + "% HP";
            return "+" + item.Bonus + " " + (item.Stat == StatNames.Hp ? "HP" : item.Stat);
        }
    }
}
