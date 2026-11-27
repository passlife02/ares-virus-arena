using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Ares
{
    /// <summary>
    /// Backpack (30 slots) plus the crafting bench, both in one panel so the player can see
    /// materials and recipes at the same time (feedback #13 / #14).
    /// </summary>
    public class InventoryUI : MonoBehaviour
    {
        private Canvas _canvas;
        private RectTransform _rootRect;
        private RectTransform _panel;
        private Text _usedText;
        private Text _equipText;
        private Image[] _slotBgs;
        private Image[] _slotIcons;
        private Text[] _slotCounts;
        private Text[] _slotNames;
        private Image[] _slotDurability;
        private Image _selected;
        private bool _open;

        private RectTransform _tooltip;
        private RectTransform _tipAnchor;
        private Text _tipTitle;
        private Text _tipBody;
        private RectTransform _recipeRoot;
        private string _recipeFilter = "全部";
        private Text _enhanceInfo;
        private int _enhanceTarget = -1;
        private readonly List<GameObject> _recipeRows = new List<GameObject>();

        public bool IsOpen { get { return _open; } }

        private const int Columns = 6;
        private const int Rows = 5;
        private const float SlotSize = 126f;
        private const float SlotGap = 10f;

        public void Build()
        {
            _canvas = UiFactory.CreateCanvas("InventoryCanvas", 20);
            _canvas.transform.SetParent(transform, false);
            _rootRect = (RectTransform)_canvas.transform;

            var dim = UiFactory.CreateImage(_canvas.transform, "Dim", UiFactory.WhiteSprite, new Color(0f, 0f, 0f, 0.66f));
            UiFactory.SetFullStretch(dim.rectTransform);
            dim.raycastTarget = true;

            _panel = UiFactory.CreateRect(_canvas.transform, "Panel");
            UiFactory.Anchor(_panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1620f, 900f));

            var bg = UiFactory.CreateImage(_panel, "Bg", UiFactory.PanelSprite, Color.white);
            UiFactory.SetFullStretch(bg.rectTransform);
            if (UiFactory.PanelSprite == null) bg.color = new Color(0.075f, 0.085f, 0.09f, 0.98f);
            bg.type = Image.Type.Sliced;
            bg.raycastTarget = true;

            var border = UiFactory.CreateImage(_panel, "Border", UiFactory.WhiteSprite, new Color(0.42f, 0.4f, 0.3f, 0.5f));
            UiFactory.SetFullStretch(border.rectTransform);
            border.raycastTarget = false;
            var inset = UiFactory.CreateImage(_panel, "Inset", UiFactory.WhiteSprite, new Color(0.06f, 0.065f, 0.07f, 1f));
            UiFactory.Anchor(inset.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-6f, -6f));
            inset.raycastTarget = false;

            var title = UiFactory.CreateText(_panel, "Title", "背  包", 38, new Color(0.94f, 0.9f, 0.78f), TextAnchor.MiddleLeft);
            UiFactory.Anchor(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(36f, -22f), new Vector2(300f, 52f));

            _usedText = UiFactory.CreateText(_panel, "Used", "0 / 30", 24, new Color(0.8f, 0.84f, 0.8f), TextAnchor.MiddleLeft);
            UiFactory.Anchor(_usedText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(320f, -26f), new Vector2(220f, 44f));

            _equipText = UiFactory.CreateText(_panel, "Equip", "", 20, new Color(0.85f, 0.8f, 0.6f), TextAnchor.MiddleRight);
            UiFactory.Anchor(_equipText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-110f, -28f), new Vector2(700f, 40f));

            var closeBtn = UiFactory.CreateButton(_panel, "Close", "X", 24, () => SetOpen(false));
            UiFactory.Anchor((RectTransform)closeBtn.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-18f, -16f), new Vector2(54f, 54f));

            // ---------------- slot grid ----------------
            var grid = UiFactory.CreateRect(_panel, "Grid");
            float gridW = Columns * SlotSize + (Columns - 1) * SlotGap;
            float gridH = Rows * SlotSize + (Rows - 1) * SlotGap;
            UiFactory.Anchor(grid, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(36f, -22f), new Vector2(gridW, gridH));

            _slotBgs = new Image[InventoryModel.Capacity];
            _slotIcons = new Image[InventoryModel.Capacity];
            _slotCounts = new Text[InventoryModel.Capacity];
            _slotNames = new Text[InventoryModel.Capacity];
            _slotDurability = new Image[InventoryModel.Capacity];

            var slotSprite = UiFactory.LoadSprite("ui_zhuangbeikuang_di");
            var selSprite = UiFactory.LoadSprite("ui_zhuangbeikuang_xuanzhong");

            for (int i = 0; i < InventoryModel.Capacity; i++)
            {
                int col = i % Columns;
                int row = i / Columns;
                float x = SlotSize * 0.5f + col * (SlotSize + SlotGap);
                float y = gridH * 0.5f - SlotSize * 0.5f - row * (SlotSize + SlotGap);

                var bgImg = UiFactory.CreateImage(grid, "Slot" + i, slotSprite != null ? slotSprite : UiFactory.WhiteSprite, new Color(0.28f, 0.29f, 0.31f, 0.9f));
                UiFactory.Anchor(bgImg.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, y), new Vector2(SlotSize, SlotSize));
                _slotBgs[i] = bgImg;

                // clicking a consumable (bandage) uses it
                bgImg.raycastTarget = true;
                int slotIndex = i;
                var slotBtn = bgImg.gameObject.AddComponent<Button>();
                slotBtn.transition = Selectable.Transition.None;
                slotBtn.onClick.AddListener(() => OnSlotClick(slotIndex));
                var hover = bgImg.gameObject.AddComponent<UiHover>();
                hover.onEnter = () => ShowTooltip(slotIndex);
                hover.onExit = () => HideTooltip();

                var icon = UiFactory.CreateImage(bgImg.transform, "Icon", null, Color.white);
                UiFactory.Anchor(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 14f), new Vector2(SlotSize - 34f, SlotSize - 52f));
                icon.preserveAspect = true;
                _slotIcons[i] = icon;

                var cnt = UiFactory.CreateText(bgImg.transform, "Count", "", 24, new Color(0.12f, 0.11f, 0.1f), TextAnchor.LowerRight);
                UiFactory.Anchor(cnt.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-8f, 2f), new Vector2(80f, 28f));
                _slotCounts[i] = cnt;

                var nm = UiFactory.CreateText(bgImg.transform, "Name", "", 17, new Color(0.15f, 0.14f, 0.12f), TextAnchor.LowerCenter);
                UiFactory.Anchor(nm.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(SlotSize - 10f, 20f));
                _slotNames[i] = nm;

                var dur = UiFactory.CreateImage(bgImg.transform, "Dur", UiFactory.WhiteSprite, new Color(0.9f, 0.72f, 0.28f, 1f));
                UiFactory.Anchor(dur.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(SlotSize - 22f, 7f));
                dur.type = Image.Type.Filled;
                dur.fillMethod = Image.FillMethod.Horizontal;
                _slotDurability[i] = dur;
            }

            _selected = UiFactory.CreateImage(grid, "Selected", selSprite, new Color(1f, 1f, 1f, 0f));
            UiFactory.Anchor(_selected.rectTransform, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(SlotSize + 16f, SlotSize + 16f));

            // ---------------- crafting ----------------
            var cTitle = UiFactory.CreateText(_panel, "CraftTitle", "制  作", 34, new Color(0.94f, 0.9f, 0.78f), TextAnchor.MiddleLeft);
            UiFactory.Anchor(cTitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(960f, -24f), new Vector2(300f, 50f));

            var cHint = UiFactory.CreateText(_panel, "CraftHint", "消耗背包材料合成，合成后立即获得", 17, new Color(0.62f, 0.66f, 0.62f), TextAnchor.MiddleLeft);
            UiFactory.Anchor(cHint.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(960f, -62f), new Vector2(560f, 24f));

            // category tabs
            string[] cats = new string[] { "全部", "武器", "消耗品", "弹药", "材料" };
            for (int i = 0; i < cats.Length; i++)
            {
                string c = cats[i];
                var tab = UiFactory.CreateButton(_panel, "Tab" + i, c, 17, () => SetRecipeFilter(c));
                UiFactory.Anchor((RectTransform)tab.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(950f + i * 118f, -96f), new Vector2(110f, 38f));
            }

            var scrollGo = UiFactory.CreateRect(_panel, "RecipeScroll");
            UiFactory.Anchor(scrollGo, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(950f, -144f), new Vector2(600f, 560f));
            var sr = scrollGo.gameObject.AddComponent<ScrollRect>();

            var viewport = UiFactory.CreateRect(scrollGo, "Viewport");
            UiFactory.SetFullStretch(viewport);
            var vpImg = viewport.gameObject.AddComponent<Image>();
            vpImg.color = new Color(0f, 0f, 0f, 0.004f);
            vpImg.raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();

            var content = UiFactory.CreateRect(viewport, "Content");
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0f, 0f);

            sr.viewport = viewport;
            sr.content = content;
            sr.horizontal = false;
            sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Clamped;
            sr.scrollSensitivity = 46f;
            _recipeRoot = content;

            var footer = UiFactory.CreateText(_panel, "Footer", "Tab 关闭    右键 切换武器    点选武器可指定强化目标", 18, new Color(0.68f, 0.7f, 0.66f), TextAnchor.LowerCenter);
            UiFactory.Anchor(footer.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(1000f, 26f));

            // ---------- item tooltip (feedback #3: explain every item) ----------
            var tipBg = UiFactory.CreateImage(_canvas.transform, "Tooltip", UiFactory.PanelSprite, Color.white);
            if (UiFactory.PanelSprite == null) tipBg.color = new Color(0.06f, 0.07f, 0.08f, 0.98f);
            tipBg.type = Image.Type.Sliced;
            tipBg.raycastTarget = false;
            _tooltip = tipBg.rectTransform;
            UiFactory.Anchor(_tooltip, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(400f, 250f));

            _tipTitle = UiFactory.CreateText(tipBg.transform, "TipTitle", "", 24, new Color(0.96f, 0.9f, 0.72f), TextAnchor.UpperLeft);
            UiFactory.Anchor(_tipTitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -14f), new Vector2(364f, 30f));

            _tipBody = UiFactory.CreateText(tipBg.transform, "TipBody", "", 18, new Color(0.8f, 0.84f, 0.78f), TextAnchor.UpperLeft);
            UiFactory.Anchor(_tipBody.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -50f), new Vector2(364f, 186f));
            _tipBody.horizontalOverflow = HorizontalWrapMode.Wrap;
            _tooltip.gameObject.SetActive(false);

            // Weapon reinforcement at the bench (feedback #5 item 4).
            var enhanceBtn = UiFactory.CreateButton(_panel, "Enhance", "强化伤害", 20, OnEnhance);
            UiFactory.Anchor((RectTransform)enhanceBtn.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(40f, 26f), new Vector2(160f, 56f));

            var traitBtn = UiFactory.CreateButton(_panel, "Trait", "强化特性", 20, OnEnhanceTrait);
            UiFactory.Anchor((RectTransform)traitBtn.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(210f, 26f), new Vector2(160f, 56f));

            _enhanceInfo = UiFactory.CreateText(_panel, "EnhanceInfo", "伤害：生铁x3+火药x1   特性：生铁x3+木头x4（近战 +距离 / 远程 +穿透）", 16, new Color(0.7f, 0.74f, 0.66f), TextAnchor.MiddleLeft);
            UiFactory.Anchor(_enhanceInfo.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(380f, 26f), new Vector2(520f, 56f));
            _enhanceInfo.horizontalOverflow = HorizontalWrapMode.Wrap;

            SetOpen(false);
        }

        private int MaterialCount(string itemId)
        {
            int n = 0;
            if (Game.Stash != null) n += Game.Stash.CountOf(itemId);
            if (Game.Inventory != null) n += Game.Inventory.CountOf(itemId);
            return n;
        }

        private void OnEnhance()
        {
            ItemDef def = null;
            if (_enhanceTarget >= 0 && _enhanceTarget < InventoryModel.Capacity && Game.Inventory != null)
            {
                var s = Game.Inventory.Slots[_enhanceTarget];
                if (!s.IsEmpty) def = Game.Items != null ? Game.Items.Get(s.itemId) : null;
            }
            if (def == null || !def.IsWeapon)
            {
                var eq = Game.Equipment;
                def = eq != null ? eq.ActiveDef : null;
            }
            if (def == null || !def.IsWeapon)
            {
                if (_enhanceInfo != null) _enhanceInfo.text = "先点选一把武器作为强化目标";
                return;
            }

            int lvl = Enhancement.Level(def.id);
            if (lvl >= Enhancement.MaxLevel)
            {
                if (_enhanceInfo != null) _enhanceInfo.text = def.name + " 已经是 +" + lvl + "（满级）";
                return;
            }
            int needIron = Enhancement.DamageIronCost(lvl);
            int needPowder = Enhancement.DamagePowderCost(lvl);
            int iron = MaterialCount("mat_shengtie");
            int powder = MaterialCount("ammo_huoyao");
            if (iron < needIron || powder < needPowder)
            {
                string missing = "";
                if (iron < needIron) missing += "生铁 " + iron + "/" + needIron + "  ";
                if (powder < needPowder) missing += "火药 " + powder + "/" + needPowder;
                if (_enhanceInfo != null) _enhanceInfo.text = "材料不足：" + missing.Trim();
                return;
            }

            EnhanceConsume("mat_shengtie", needIron);
            EnhanceConsume("ammo_huoyao", needPowder);

            Enhancement.SetLevel(def.id, lvl + 1);
            if (_enhanceInfo != null)
                _enhanceInfo.text = def.name + " 强化至 +" + (lvl + 1) + "（伤害 +" + (Enhancement.DamagePerLevel * (lvl + 1)) + "）";
            Refresh();
        }

        /// <summary>Melee weapons gain reach, ranged weapons gain piercing.</summary>
        private void OnEnhanceTrait()
        {
            ItemDef def = null;
            if (_enhanceTarget >= 0 && _enhanceTarget < InventoryModel.Capacity && Game.Inventory != null)
            {
                var s = Game.Inventory.Slots[_enhanceTarget];
                if (!s.IsEmpty) def = Game.Items != null ? Game.Items.Get(s.itemId) : null;
            }
            if (def == null || !def.IsWeapon)
            {
                var eq = Game.Equipment;
                def = eq != null ? eq.ActiveDef : null;
            }
            if (def == null || !def.IsWeapon)
            {
                if (_enhanceInfo != null) _enhanceInfo.text = "先点选一把武器作为强化目标";
                return;
            }

            int lvl = Enhancement.TraitLevel(def.id);
            if (lvl >= Enhancement.MaxTrait)
            {
                if (_enhanceInfo != null) _enhanceInfo.text = def.name + " 的特性已满级";
                return;
            }
            int needIron = Enhancement.TraitIronCost(lvl);
            int needWood = Enhancement.TraitWoodCost(lvl);
            int iron = MaterialCount("mat_shengtie");
            int wood = MaterialCount("mat_mutou");
            if (iron < needIron || wood < needWood)
            {
                string miss = "";
                if (iron < needIron) miss += "生铁 " + iron + "/" + needIron + "  ";
                if (wood < needWood) miss += "木头 " + wood + "/" + needWood;
                if (_enhanceInfo != null) _enhanceInfo.text = "材料不足：" + miss.Trim();
                return;
            }

            EnhanceConsume("mat_shengtie", needIron);
            EnhanceConsume("mat_mutou", needWood);
            Enhancement.SetTraitLevel(def.id, lvl + 1);

            string what = def.IsMelee
                ? ("攻击距离 +" + (Enhancement.ReachPerTrait * (lvl + 1)).ToString("0.00"))
                : ("穿透 " + Enhancement.PierceBonus(def.id));
            if (_enhanceInfo != null)
                _enhanceInfo.text = def.name + " 特性 +" + (lvl + 1) + "（" + what + "）";
            Refresh();
        }

        private void EnhanceConsume(string itemId, int count)
        {
            int left = count;
            if (Game.Stash != null)
            {
                int take = Mathf.Min(left, Game.Stash.CountOf(itemId));
                if (take > 0) { Game.Stash.RemoveCount(itemId, take); left -= take; }
            }
            if (left > 0 && Game.Inventory != null)
            {
                int take = Mathf.Min(left, Game.Inventory.CountOf(itemId));
                if (take > 0) { Game.Inventory.RemoveCount(itemId, take); left -= take; }
            }
            if (Game.Save != null)
            {
                if (Game.Stash != null) Game.Save.SaveStash(Game.Stash);
                if (Game.Inventory != null) Game.Save.SaveInventory(Game.Inventory);
            }
        }

        private void ShowTooltip(int index)
        {
            var inv = Game.Inventory;
            if (_tooltip == null || inv == null) return;
            if (index < 0 || index >= InventoryModel.Capacity) return;
            var slot = inv.Slots[index];
            if (slot.IsEmpty) { HideTooltip(); return; }
            var def = Game.Items != null ? Game.Items.Get(slot.itemId) : null;
            if (def == null) { HideTooltip(); return; }

            _tipAnchor = _slotBgs[index] != null ? _slotBgs[index].rectTransform : null;
            _tipTitle.text = def.name;
            _tipBody.text = DescribeItem(def, slot);

            // The panel must be active before preferredHeight is queried, otherwise the layout
            // generator has not run and the height comes back as 0 (text then looked blank).
            _tooltip.gameObject.SetActive(true);

            float h = Mathf.Clamp(_tipBody.preferredHeight + 84f, 110f, 460f);
            _tooltip.sizeDelta = new Vector2(400f, h);
            _tipBody.rectTransform.sizeDelta = new Vector2(364f, h - 74f);
        }

        private void HideTooltip()
        {
            if (_tooltip != null) _tooltip.gameObject.SetActive(false);
        }

        /// <summary>Human readable stat block for an item, used by the tooltip and crafting rows.</summary>
        public static string DescribeItem(ItemDef def, InventorySlot slot)
        {
            if (def == null) return "";
            var sb = new System.Text.StringBuilder();
            sb.Append("<color=#8fa08f>").Append(def.category).Append("</color>\n");
            if (!string.IsNullOrEmpty(def.desc)) sb.Append(def.desc).Append("\n");

            if (def.IsMelee)
            {
                sb.Append("\n<color=#e8c87a>伤害 ").Append(def.damage).Append("</color>");
                if (def.attackInterval > 0.01f)
                    sb.Append("   ·   DPS ").Append((def.damage / def.attackInterval).ToString("0"));
                sb.Append("\n攻击间隔 ").Append(def.attackInterval.ToString("0.00")).Append("s");
                sb.Append("   ·   范围 ").Append(def.range.ToString("0.0"));
                if (def.durabilityMax > 0)
                {
                    int dur = slot != null ? slot.durability : def.durabilityMax;
                    sb.Append("\n耐久 ").Append(dur).Append(" / ").Append(def.durabilityMax).Append("（每次挥砍 -1，归零消失）");
                }
            }
            else if (def.IsRanged)
            {
                sb.Append("\n<color=#e8c87a>伤害 ").Append(def.damage).Append("</color>");
                if (def.attackInterval > 0.01f)
                    sb.Append("   ·   DPS ").Append((def.damage / def.attackInterval).ToString("0"));
                sb.Append("\n攻击间隔 ").Append(def.attackInterval.ToString("0.00")).Append("s");
                var ammo = Game.Items != null ? Game.Items.Get(def.ammoItemId) : null;
                sb.Append("\n弹药 ").Append(ammo != null ? ammo.name : def.ammoItemId);
                sb.Append("   ·   每发消耗 ").Append(def.ammoPerShot);
            }
            else if (def.healAmount > 0)
            {
                sb.Append("\n<color=#8fe08a>恢复 ").Append(def.healAmount).Append(" 点生命</color>（点击使用）");
            }
            else
            {
                sb.Append("\n堆叠上限 ").Append(def.stackMax);
            }
            return sb.ToString();
        }

        public void SetOpen(bool open)
        {
            _open = open;
            if (_canvas != null) _canvas.enabled = open;
            if (open) Refresh();
            else HideTooltip();
        }

        public void Toggle() { SetOpen(!_open); }

        public void Refresh()
        {
            var inv = Game.Inventory;
            var items = Game.Items;
            if (inv == null || _slotBgs == null) return;

            int used = 0;
            int equippedMelee = Game.Equipment != null ? Game.Equipment.meleeSlot : -1;
            int equippedRanged = Game.Equipment != null ? Game.Equipment.rangedSlot : -1;

            for (int i = 0; i < InventoryModel.Capacity; i++)
            {
                var s = inv.Slots[i];
                bool has = !s.IsEmpty;
                if (has) used++;

                var def = has ? items.Get(s.itemId) : null;
                if (_slotIcons[i] != null)
                {
                    _slotIcons[i].enabled = has;
                    _slotIcons[i].sprite = def != null ? items.GetIcon(def.icon) : null;
                }
                if (_slotCounts[i] != null) _slotCounts[i].text = has && s.count > 1 ? s.count.ToString() : "";
                if (_slotNames[i] != null)
                {
                    _slotNames[i].text = def != null ? def.name : (has ? s.itemId : "");
                    _slotNames[i].color = has ? new Color(0.15f, 0.14f, 0.12f) : new Color(0f, 0f, 0f, 0f);
                }

                bool isEquipped = i == equippedMelee || i == equippedRanged;
                if (_slotBgs[i] != null)
                {
                    _slotBgs[i].color = !has ? new Color(0.24f, 0.25f, 0.27f, 0.85f)
                        : isEquipped ? new Color(1f, 0.92f, 0.62f, 1f)
                        : new Color(0.82f, 0.82f, 0.76f, 1f);
                }

                bool showDur = has && def != null && def.durabilityMax > 0;
                if (_slotDurability[i] != null)
                {
                    _slotDurability[i].gameObject.SetActive(showDur);
                    if (showDur)
                    {
                        _slotDurability[i].fillAmount = Mathf.Clamp01((float)s.durability / def.durabilityMax);
                        _slotDurability[i].color = s.durability <= def.durabilityMax * 0.25f
                            ? new Color(0.85f, 0.3f, 0.22f) : new Color(0.9f, 0.75f, 0.3f);
                    }
                }
            }

            if (_usedText != null) _usedText.text = used + " / " + InventoryModel.Capacity;
            if (_equipText != null)
            {
                var eq = Game.Equipment;
                string m = eq != null && eq.meleeSlot >= 0 ? (items.Get(inv.Slots[eq.meleeSlot].itemId) != null ? items.Get(inv.Slots[eq.meleeSlot].itemId).name : "") : "无";
                string r = eq != null && eq.rangedSlot >= 0 ? (items.Get(inv.Slots[eq.rangedSlot].itemId) != null ? items.Get(inv.Slots[eq.rangedSlot].itemId).name : "") : "无";
                _equipText.text = "近战：" + m + "    远程：" + r;
            }

            RebuildRecipes();
        }

        private void LateUpdate()
        {
            if (_tooltip == null || !_tooltip.gameObject.activeSelf || _rootRect == null) return;
            Vector2 local;
            Vector2 anchorLocal = Vector2.zero;
            bool have = false;

            // anchor on the hovered slot so the panel never covers the item itself
            if (_tipAnchor != null)
            {
                Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, _tipAnchor.position);
                have = RectTransformUtility.ScreenPointToLocalPointInRectangle(_rootRect, screen, null, out anchorLocal);
            }
            if (!have)
                have = RectTransformUtility.ScreenPointToLocalPointInRectangle(_rootRect, Input.mousePosition, null, out anchorLocal);
            if (!have) return;

            float w = _tooltip.sizeDelta.x;
            float h = _tooltip.sizeDelta.y;
            float halfW = w * 0.5f;
            float halfH = h * 0.5f;
            var r = _rootRect.rect;
            float anchorHalfW = _tipAnchor != null ? _tipAnchor.rect.width * 0.5f : 0f;

            float x = anchorLocal.x + anchorHalfW + 14f + halfW;
            if (x + halfW > r.xMax - 10f) x = anchorLocal.x - anchorHalfW - 14f - halfW;
            x = Mathf.Clamp(x, r.xMin + 10f + halfW, r.xMax - 10f - halfW);

            float y = Mathf.Clamp(anchorLocal.y, r.yMin + 10f + halfH, r.yMax - 10f - halfH);
            _tooltip.anchoredPosition = new Vector2(x, y);
        }

        /// <summary>Weapons become the enhancement target; consumables are used on the spot.</summary>
        private void OnSlotClick(int index)
        {
            var inv = Game.Inventory;
            if (inv == null) return;
            var s = inv.Slots[index];
            if (!s.IsEmpty)
            {
                var def = Game.Items != null ? Game.Items.Get(s.itemId) : null;
                if (def != null && def.IsWeapon)
                {
                    _enhanceTarget = index;
                    if (_enhanceInfo != null)
                        _enhanceInfo.text = "强化目标：" + def.name + "（生铁 x3 + 火药 x1 → 伤害 +6）";
                    AudioManager.PlaySfx("sfx_click", 0.5f, 0.05f);
                    Refresh();
                    return;
                }
            }
            UseSlot(index);
        }

        private void UseSlot(int index)
        {
            var inv = Game.Inventory;
            if (inv == null || index < 0 || index >= InventoryModel.Capacity) return;
            var s = inv.Slots[index];
            if (s.IsEmpty) return;
            var def = Game.Items != null ? Game.Items.Get(s.itemId) : null;
            if (def == null || def.healAmount <= 0) return;
            if (Game.Player == null || !Game.Player.IsAlive) return;

            Game.Player.Heal(def.healAmount);
            inv.RemoveCount(s.itemId, 1);
            if (Game.Hud != null) Game.Hud.ShowPrompt("使用 " + def.name + "，恢复 " + def.healAmount + " 生命", 1.4f);
            Refresh();
        }

        /// <summary>One-line stat summary shown next to a craftable item.</summary>
        public static string ItemStatsLine(ItemDef d)
        {
            if (d == null) return "";
            if (d.IsMelee) return "伤害 " + d.damage + "   间隔 " + d.attackInterval.ToString("0.00") + "s   耐久 " + d.durabilityMax;
            if (d.IsRanged) return "伤害 " + d.damage + "   间隔 " + d.attackInterval.ToString("0.00") + "s   弹药/发 " + d.ammoPerShot;
            if (d.healAmount > 0) return "恢复 " + d.healAmount + " 点生命（点击使用）";
            return d.desc;
        }

        private void SetRecipeFilter(string category)
        {
            _recipeFilter = category;
            if (_recipeRoot != null) _recipeRoot.anchoredPosition = Vector2.zero;
            RebuildRecipes();
        }

        private bool MatchesFilter(string category)
        {
            if (string.IsNullOrEmpty(_recipeFilter) || _recipeFilter == "全部") return true;
            return category == _recipeFilter;
        }

        private void RebuildRecipes()
        {
            if (_recipeRoot == null || Game.Crafting == null) return;
            for (int i = 0; i < _recipeRows.Count; i++) Destroy(_recipeRows[i]);
            _recipeRows.Clear();

            var recipes = Game.Items != null ? Game.Items.Recipes : null;
            if (recipes == null) return;

            float y = 0f;
            for (int i = 0; i < recipes.Count; i++)
            {
                var r = recipes[i];
                var outDef = Game.Items.Get(r.output);
                if (outDef == null) continue;
                if (!MatchesFilter(r.category)) continue;

                var row = UiFactory.CreateImage(_recipeRoot, "Recipe" + i, UiFactory.WhiteSprite, new Color(0.11f, 0.12f, 0.13f, 0.95f));
                UiFactory.Anchor(row.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -y), new Vector2(600f, 96f));
                _recipeRows.Add(row.gameObject);

                var icon = UiFactory.CreateImage(row.transform, "Icon", Game.Items.GetIcon(outDef.icon), Color.white);
                UiFactory.Anchor(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(10f, 0f), new Vector2(76f, 76f));
                icon.preserveAspect = true;

                var nm = UiFactory.CreateText(row.transform, "Name", outDef.name, 22, new Color(0.95f, 0.92f, 0.8f), TextAnchor.UpperLeft);
                UiFactory.Anchor(nm.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(96f, -10f), new Vector2(300f, 28f));

                string cost = "";
                if (!string.IsNullOrEmpty(r.input1))
                {
                    var d1 = Game.Items.Get(r.input1);
                    bool ok1 = Game.Inventory.CountOf(r.input1) >= r.count1;
                    cost += (ok1 ? "" : "<color=#ff6b5e>") + (d1 != null ? d1.name : r.input1) + " x" + r.count1 + (ok1 ? "" : "</color>");
                }
                if (!string.IsNullOrEmpty(r.input2) && r.count2 > 0)
                {
                    var d2 = Game.Items.Get(r.input2);
                    bool ok2 = Game.Inventory.CountOf(r.input2) >= r.count2;
                    cost += "  +  " + (ok2 ? "" : "<color=#ff6b5e>") + (d2 != null ? d2.name : r.input2) + " x" + r.count2 + (ok2 ? "" : "</color>");
                }

                var req = UiFactory.CreateText(row.transform, "Req", cost, 17, new Color(0.72f, 0.76f, 0.7f), TextAnchor.UpperLeft);
                UiFactory.Anchor(req.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(96f, -42f), new Vector2(400f, 24f));

                var desc = UiFactory.CreateText(row.transform, "Desc", ItemStatsLine(outDef), 15, new Color(0.72f, 0.7f, 0.5f), TextAnchor.UpperLeft);
                UiFactory.Anchor(desc.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(96f, -68f), new Vector2(400f, 22f));

                bool can = Game.Crafting.CanCraft(r);
                var btn = UiFactory.CreateButton(row.transform, "Craft", can ? "制作" : "材料不足", 19, () =>
                {
                    if (Game.Crafting.Craft(r)) Refresh();
                });
                UiFactory.Anchor((RectTransform)btn.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-12f, 0f), new Vector2(126f, 48f));
                btn.interactable = can;

                y += 104f;
            }

            if (_recipeRoot != null)
                _recipeRoot.sizeDelta = new Vector2(0f, Mathf.Max(y, 560f));
        }
    }
}
