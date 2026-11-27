using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Ares
{
    /// <summary>
    /// Shelter hub (feedback #5 item 7): the entrance shows three icon rooms - backpack,
    /// merchant and blacksmith. The backpack is a separate 30 slot container that you fill by
    /// clicking items across; the merchant sells supplies; the smith repairs, reinforces and
    /// converts weapons.
    /// </summary>
    public class ShelterUI : MonoBehaviour
    {
        private enum View { Hub, Bag, Merchant, Smith, Milestones, Armory }

        private Canvas _canvas;
        private RectTransform _panel;
        private RectTransform _hubRoot, _bagRoot, _shopRoot, _smithRoot, _msRoot, _armoryRoot;
        private Text _hubStatus;
        private Text[] _msRows;
        private Text _shopStatus;
        private Text _smithStatus;
        private Text[] _modStates;
        private Button[] _modBtns;
        private Image[][] _modPips;
        private Image[][] _armorPips;
        private Button _rewardBtn;
        private Text _bagHint;
        private View _view = View.Hub;
        private bool _open;

        // bag grids
        private Image[] _stashBgs, _bagBgs;
        private Image[] _stashIcons, _bagIcons;
        private Text[] _stashCounts, _bagCounts;
        private Text[] _stashNames, _bagNames;
        private Text _stashHeader, _bagHeader;

        private RectTransform _tooltip;
        private RectTransform _tipAnchor;
        private Text _tipTitle;
        private Text _tipBody;

        private const int Columns = 6;
        private const int Rows = 5;
        private const float SlotSize = 92f;
        private const float SlotGap = 8f;

        public bool IsOpen { get { return _open; } }

        private float GridWidth { get { return Columns * SlotSize + (Columns - 1) * SlotGap; } }
        private float GridHeight { get { return Rows * SlotSize + (Rows - 1) * SlotGap; } }

        // ------------------------------------------------------------------ build

        public void Build()
        {
            _canvas = UiFactory.CreateCanvas("ShelterCanvas", 34);
            _canvas.transform.SetParent(transform, false);

            var dim = UiFactory.CreateImage(_canvas.transform, "Dim", UiFactory.WhiteSprite, new Color(0f, 0f, 0f, 0.84f));
            UiFactory.SetFullStretch(dim.rectTransform);
            dim.raycastTarget = true;

            _panel = UiFactory.CreateRect(_canvas.transform, "Panel");
            UiFactory.Anchor(_panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1560f, 880f));

            var bg = UiFactory.CreateImage(_panel, "Bg", UiFactory.PanelSprite, Color.white);
            UiFactory.SetFullStretch(bg.rectTransform);
            if (UiFactory.PanelSprite == null) bg.color = new Color(0.07f, 0.075f, 0.08f, 0.98f);
            bg.type = Image.Type.Sliced;
            bg.raycastTarget = true;

            var title = UiFactory.CreateText(_panel, "Title", "庇  护  所", 42, new Color(0.92f, 0.87f, 0.7f), TextAnchor.MiddleLeft);
            UiFactory.Anchor(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(50f, -24f), new Vector2(600f, 56f));

            var close = UiFactory.CreateButton(_panel, "Close", "X", 24, () => SetOpen(false));
            UiFactory.Anchor((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -22f), new Vector2(56f, 56f));

            _hubRoot = UiFactory.CreateRect(_panel, "Hub");
            UiFactory.SetFullStretch(_hubRoot);
            _bagRoot = UiFactory.CreateRect(_panel, "Bag");
            UiFactory.SetFullStretch(_bagRoot);
            _shopRoot = UiFactory.CreateRect(_panel, "Merchant");
            UiFactory.SetFullStretch(_shopRoot);
            _smithRoot = UiFactory.CreateRect(_panel, "Smith");
            UiFactory.SetFullStretch(_smithRoot);
            _msRoot = UiFactory.CreateRect(_panel, "Milestones");
            UiFactory.SetFullStretch(_msRoot);
            _armoryRoot = UiFactory.CreateRect(_panel, "Armory");
            UiFactory.SetFullStretch(_armoryRoot);

            BuildHub();
            BuildBag();
            BuildMerchant();
            BuildSmith();
            BuildMilestones();
            BuildArmory();
            BuildTooltip();

            ShowView(View.Hub);
            SetOpen(false);
        }

        // ------------------------------------------------------------------ hub

        private void BuildHub()
        {
            var sub = UiFactory.CreateText(_hubRoot, "Sub", "选择要前往的房间", 20, new Color(0.66f, 0.7f, 0.64f), TextAnchor.MiddleCenter);
            UiFactory.Anchor(sub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -96f), new Vector2(800f, 28f));

            RoomButtonFrom("背包", "Codex/backpack_sheet", -620f, () => ShowView(View.Bag));
            RoomButtonFrom("商人", "Portrait/head_merchant", -310f, () => ShowView(View.Merchant));
            RoomButtonFrom("铁匠", "Portrait/head_smith", 0f, () => ShowView(View.Smith));
                        RoomButtonFrom("装备", "UI/icon_chest", 310f, () => ShowView(View.Armory));
            RoomButtonFrom("里程碑", "Hub/sheet_monument", 620f, () => ShowView(View.Milestones));

            var deploy = UiFactory.CreateButton(_hubRoot, "Deploy", "带 上 背 包 出 战", 28, Deploy);
            UiFactory.Anchor((RectTransform)deploy.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 56f), new Vector2(420f, 82f));

            _hubStatus = UiFactory.CreateText(_hubRoot, "Status", "", 19, new Color(0.76f, 0.8f, 0.72f), TextAnchor.MiddleCenter);
            UiFactory.Anchor(_hubStatus.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 152f), new Vector2(1300f, 60f));
            _hubStatus.horizontalOverflow = HorizontalWrapMode.Wrap;
        }

        private void RoomButton(string label, string sheet, float x, System.Action onClick)
        {
            RoomButtonFrom(label, "Hub/" + sheet, x, onClick);
        }

        private void RoomButtonFrom(string label, string resourcePath, float x, System.Action onClick)
        {
            var frames = Resources.LoadAll<Sprite>(resourcePath);
            Sprite art = frames != null && frames.Length > 0 ? frames[0] : null;

            var btn = UiFactory.CreateButton(_hubRoot, "Room" + label, "", 22, onClick);
            var rt = (RectTransform)btn.transform;
            UiFactory.Anchor(rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, 40f), new Vector2(300f, 300f));

            var frame = UiFactory.CreateImage(rt, "Frame", UiFactory.WhiteSprite, new Color(0.14f, 0.15f, 0.16f, 0.92f));
            UiFactory.SetFullStretch(frame.rectTransform);

            if (art != null)
            {
                var img = UiFactory.CreateImage(rt, "Art", art, Color.white);
                UiFactory.Anchor(img.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(250f, 230f));
                img.preserveAspect = true;
                img.raycastTarget = false;
            }

            var text = UiFactory.CreateText(rt, "Label", label, 28, new Color(0.94f, 0.88f, 0.7f), TextAnchor.MiddleCenter);
            UiFactory.Anchor(text.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(280f, 40f));
        }

        // ------------------------------------------------------------------ bag

        private void BuildBag()
        {
            var sub = UiFactory.CreateText(_bagRoot, "Sub", "左：仓库   右：随身背包   点击物品在两栏之间移动", 19, new Color(0.62f, 0.66f, 0.6f), TextAnchor.MiddleLeft);
            UiFactory.Anchor(sub.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(52f, -74f), new Vector2(1000f, 26f));

            var back = UiFactory.CreateButton(_bagRoot, "Back", "< 返回", 22, () => ShowView(View.Hub));
            UiFactory.Anchor((RectTransform)back.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-100f, -18f), new Vector2(130f, 44f));

            float gridTop = -110f;
            _stashHeader = UiFactory.CreateText(_bagRoot, "StashHeader", "仓库", 23, new Color(0.86f, 0.8f, 0.6f), TextAnchor.MiddleLeft);
            UiFactory.Anchor(_stashHeader.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(60f, gridTop), new Vector2(600f, 30f));

            _bagHeader = UiFactory.CreateText(_bagRoot, "BagHeader", "随身背包", 23, new Color(0.86f, 0.8f, 0.6f), TextAnchor.MiddleLeft);
            UiFactory.Anchor(_bagHeader.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(840f, gridTop), new Vector2(600f, 30f));

            BuildGrid(_bagRoot, "Stash", 60f, gridTop - 40f, out _stashBgs, out _stashIcons, out _stashCounts, out _stashNames, true);
            BuildGrid(_bagRoot, "Bag", 840f, gridTop - 40f, out _bagBgs, out _bagIcons, out _bagCounts, out _bagNames, false);

            var repair = UiFactory.CreateButton(_bagRoot, "Repair", "修理武器（生铁 x2）", 21, RepairWeapons);
            UiFactory.Anchor((RectTransform)repair.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(60f, 40f), new Vector2(320f, 58f));

            var sort = UiFactory.CreateButton(_bagRoot, "Sort", "整 理 仓 库", 21, SortStash);
            UiFactory.Anchor((RectTransform)sort.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(400f, 40f), new Vector2(230f, 58f));

            _bagHint = UiFactory.CreateText(_bagRoot, "Hint", "", 17, new Color(0.7f, 0.74f, 0.68f), TextAnchor.MiddleCenter);
            UiFactory.Anchor(_bagHint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 108f), new Vector2(1400f, 44f));
            _bagHint.horizontalOverflow = HorizontalWrapMode.Wrap;
        }

        private void BuildGrid(RectTransform parent, string prefix, float ox, float oy,
                               out Image[] bgs, out Image[] icons, out Text[] counts, out Text[] names, bool isStash)
        {
            var grid = UiFactory.CreateRect(parent, prefix + "Grid");
            UiFactory.Anchor(grid, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(ox, oy), new Vector2(GridWidth, GridHeight));

            bgs = new Image[InventoryModel.Capacity];
            icons = new Image[InventoryModel.Capacity];
            counts = new Text[InventoryModel.Capacity];
            names = new Text[InventoryModel.Capacity];

            var slotSprite = UiFactory.LoadSprite("ui_zhuangbeikuang_di");
            for (int i = 0; i < InventoryModel.Capacity; i++)
            {
                int col = i % Columns;
                int row = i / Columns;
                float x = SlotSize * 0.5f + col * (SlotSize + SlotGap);
                float y = -SlotSize * 0.5f - row * (SlotSize + SlotGap);

                var cell = UiFactory.CreateImage(grid, "S" + i, slotSprite != null ? slotSprite : UiFactory.WhiteSprite, new Color(0.3f, 0.31f, 0.33f, 0.9f));
                UiFactory.Anchor(cell.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(x, y), new Vector2(SlotSize, SlotSize));
                cell.raycastTarget = true;

                var icon = UiFactory.CreateImage(cell.transform, "Icon", null, Color.white);
                UiFactory.Anchor(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 6f), new Vector2(SlotSize - 28f, SlotSize - 36f));
                icon.preserveAspect = true;

                var cnt = UiFactory.CreateText(cell.transform, "Count", "", 18, new Color(0.1f, 0.1f, 0.1f), TextAnchor.LowerRight);
                UiFactory.Anchor(cnt.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-5f, 2f), new Vector2(62f, 20f));

                var nm = UiFactory.CreateText(cell.transform, "Name", "", 13, new Color(0.14f, 0.13f, 0.11f), TextAnchor.LowerCenter);
                UiFactory.Anchor(nm.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(SlotSize - 6f, 16f));

                bgs[i] = cell; icons[i] = icon; counts[i] = cnt; names[i] = nm;

                int idx = i;
                var btn = cell.gameObject.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                if (isStash) btn.onClick.AddListener(() => OnStashClick(idx));
                else btn.onClick.AddListener(() => OnBagClick(idx));

                var hover = cell.gameObject.AddComponent<UiHover>();
                var cellRect = cell.rectTransform;
                if (isStash) hover.onEnter = () => ShowTip(Ares.Game.Stash, idx, cellRect);
                else hover.onEnter = () => ShowTip(Ares.Game.Inventory, idx, cellRect);
                hover.onExit = HideTip;

                var drag = cell.gameObject.AddComponent<UiDragSlot>();
                drag.index = idx;
                drag.isStash = isStash;
                drag.Canvas = _canvas;
                drag.OnDropped = OnDragDropped;
            }
        }

        // ------------------------------------------------------------------ merchant

        private struct Offer
        {
            public string itemId;
            public int count;
            public string costId;
            public int costCount;
            public string label;
        }

        private static readonly Offer[] Pool = new Offer[]
        {
            new Offer { itemId = "ammo_huoyao",    count = 30, costId = "mat_mutou",    costCount = 4, label = "火药 x30" },
            new Offer { itemId = "ammo_yumaojian", count = 30, costId = "mat_mutou",    costCount = 4, label = "铁箭羽 x30" },
            new Offer { itemId = "mat_shengtie",   count = 3,  costId = "mat_mutou",    costCount = 8, label = "生铁 x3" },
            new Offer { itemId = "tool_bengdai",   count = 2,  costId = "mat_shengtie", costCount = 1, label = "绷带 x2" },
            new Offer { itemId = "mat_jiaodai",    count = 4,  costId = "mat_mutou",    costCount = 5, label = "胶带 x4" },
            new Offer { itemId = "mat_yumao",      count = 6,  costId = "mat_mutou",    costCount = 5, label = "羽毛 x6" },
            new Offer { itemId = "mat_masheng",    count = 4,  costId = "mat_mutou",    costCount = 4, label = "麻绳 x4" },
            new Offer { itemId = "tool_grenade",   count = 2,  costId = "mat_shengtie", costCount = 2, label = "手榴弹 x2" },
            new Offer { itemId = "tool_fence",     count = 2,  costId = "mat_mutou",    costCount = 4, label = "木栅栏 x2" },
        };

        private const int ShopSlots = 4;
        private Offer[] _offers = new Offer[ShopSlots];
        private Text[] _offerNames;
        private Text[] _offerPrices;
        private Text _blessingText;
        private float _discount;

        private void BuildMerchant()
        {
            var sub = UiFactory.CreateText(_shopRoot, "Sub", "商人：购买补给、祈福，也可以花物资刷新货架", 19, new Color(0.62f, 0.66f, 0.6f), TextAnchor.MiddleLeft);
            UiFactory.Anchor(sub.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(52f, -74f), new Vector2(1000f, 26f));

            var back = UiFactory.CreateButton(_shopRoot, "Back", "< 返回", 22, () => ShowView(View.Hub));
            UiFactory.Anchor((RectTransform)back.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-100f, -18f), new Vector2(130f, 44f));

            {
                var merchantPortrait = Resources.Load<Sprite>("Portrait/bust_merchant");
                if (merchantPortrait == null) merchantPortrait = Resources.Load<Sprite>("Portrait/merchant");
                if (merchantPortrait == null)
                {
                    var art = Resources.LoadAll<Sprite>("Hub/sheet_merchant");
                    if (art != null && art.Length > 0) merchantPortrait = art[0];
                }
                BuildNpcPortrait(_shopRoot, merchantPortrait, true);
            }

            _offerNames = new Text[ShopSlots];
            _offerPrices = new Text[ShopSlots];

            for (int i = 0; i < ShopSlots; i++)
            {
                int idx = i;
                var row = UiFactory.CreateImage(_shopRoot, "Row" + i, UiFactory.WhiteSprite, new Color(0.16f, 0.17f, 0.18f, 0.9f));
                UiFactory.Anchor(row.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(420f, -140f - i * 92f), new Vector2(1000f, 78f));

                var name = UiFactory.CreateText(row.transform, "Name", "", 24, new Color(0.9f, 0.86f, 0.72f), TextAnchor.MiddleLeft);
                UiFactory.Anchor(name.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -10f), new Vector2(420f, 30f));
                _offerNames[i] = name;

                var price = UiFactory.CreateText(row.transform, "Price", "", 19, new Color(0.7f, 0.74f, 0.66f), TextAnchor.MiddleLeft);
                UiFactory.Anchor(price.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(20f, 12f), new Vector2(420f, 22f));
                _offerPrices[i] = price;

                var buy = UiFactory.CreateButton(row.transform, "Buy", "购 买", 22, () => Buy(idx));
                UiFactory.Anchor((RectTransform)buy.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-16f, 0f), new Vector2(150f, 50f));
            }

            var blessing = UiFactory.CreateButton(_shopRoot, "Blessing", "祈 福（火药x20 + 生铁x2）", 22, DoBlessing);
            UiFactory.Anchor((RectTransform)blessing.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(420f, 108f), new Vector2(420f, 62f));

            var reroll = UiFactory.CreateButton(_shopRoot, "Reroll", "刷新货架（木头x6）", 22, Reroll);
            UiFactory.Anchor((RectTransform)reroll.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(860f, 108f), new Vector2(340f, 62f));

            var reward = UiFactory.CreateButton(_shopRoot, "Reward", "领 取 通 关 奖 励", 22, GrantClearReward);
            _rewardBtn = reward;
            UiFactory.Anchor((RectTransform)reward.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(1220f, 34f), new Vector2(280f, 62f));

            var relief = UiFactory.CreateButton(_shopRoot, "Relief", "领 取 救 济 物 资", 22, TakeRelief);
            UiFactory.Anchor((RectTransform)relief.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(1220f, 108f), new Vector2(280f, 62f));

            _blessingText = UiFactory.CreateText(_shopRoot, "BlessingInfo", "", 19, new Color(0.8f, 0.84f, 0.72f), TextAnchor.MiddleLeft);
            UiFactory.Anchor(_blessingText.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(420f, 34f), new Vector2(1100f, 56f));
            _blessingText.horizontalOverflow = HorizontalWrapMode.Wrap;

            _discount = Game.Save != null ? Mathf.Clamp01(Game.Save.GetInt("shop_discount", 0) / 100f) : 0f;
            RerollOffers();
            RefreshRewardButton();
        }

        private void RerollOffers()
        {
            var picks = new List<int>();
            for (int i = 0; i < Pool.Length; i++) picks.Add(i);
            for (int i = picks.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                int tmp = picks[i]; picks[i] = picks[j]; picks[j] = tmp;
            }
            for (int i = 0; i < ShopSlots; i++)
                _offers[i] = Pool[picks[i % picks.Count]];
            RefreshOffers();
        }

        private void RefreshOffers()
        {
            if (_offerNames == null) return;
            for (int i = 0; i < ShopSlots; i++)
            {
                var o = _offers[i];
                var cost = Game.Items != null ? Game.Items.Get(o.costId) : null;
                string costName = cost != null ? cost.name : o.costId;
                if (_offerNames[i] != null) _offerNames[i].text = o.label;
                int price = Discounted(o.costCount);
                string tag = _discount > 0.01f ? "  <color=#8fd08a>打折 " + Mathf.RoundToInt(_discount * 100f) + "%</color>" : "";
                if (_offerPrices[i] != null) _offerPrices[i].text = "价格：" + costName + " x" + price + tag
                    + "   （持有 " + TotalCount(o.costId) + "）";
            }
            if (_blessingText != null)
            {
                var b = Blessing.Current();
                _blessingText.text = string.IsNullOrEmpty(b.id)
                    ? "当前没有祈福效果。祈福后本局获得增益，出战即生效。"
                    : "当前祈福：" + b.name + " —— " + b.desc;
            }
        }

        private void DoBlessing()
        {
            if (TotalCount("ammo_huoyao") < 20 || TotalCount("mat_shengtie") < 2)
            {
                if (_blessingText != null) _blessingText.text = "祈福需要 火药 x20 + 生铁 x2";
                return;
            }
            Consume("ammo_huoyao", 20);
            Consume("mat_shengtie", 2);
            var b = Blessing.All[Random.Range(0, Blessing.All.Length)];
            Blessing.Set(b.id);
            Persist();
            if (_blessingText != null) _blessingText.text = "祈福成功：" + b.name + " —— " + b.desc;
        }

        private void Reroll()
        {
            if (TotalCount("mat_mutou") < 6)
            {
                if (_blessingText != null) _blessingText.text = "刷新需要 木头 x6";
                return;
            }
            Consume("mat_mutou", 6);

            // each reroll has a 10% chance to raise the shop discount by 10% (max 40%)
            if (Random.value < 0.10f && _discount < 0.4f)
            {
                _discount = Mathf.Min(0.4f, _discount + 0.1f);
                if (Game.Save != null) Game.Save.SetInt("shop_discount", Mathf.RoundToInt(_discount * 100f));
            }

            RerollOffers();
            Persist();
            if (_blessingText != null) _blessingText.text = "货架已刷新。";
        }
        private int TotalCount(string itemId)
        {
            int n = 0;
            if (Game.Stash != null) n += Game.Stash.CountOf(itemId);
            if (Game.Inventory != null) n += Game.Inventory.CountOf(itemId);
            return n;
        }

        private bool Consume(string itemId, int count)
        {
            if (TotalCount(itemId) < count) return false;
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
            return true;
        }

        private void Buy(int index)
        {
            var o = _offers[index];
            if (!Consume(o.costId, Discounted(o.costCount)))
            {
                SetShopStatus("材料不足：需要 " + o.costCount + " 个");
                return;
            }
            Game.Stash.TryAdd(o.itemId, o.count);
            SetShopStatus("已购买 " + o.label);
            Persist();
        }

        /// <summary>
        /// Relief only covers "I have nothing to fight with": a weapon and matching ammo.
        /// It deliberately gives no crafting materials, so it can never be used to upgrade.
        /// </summary>
        private void RefreshRewardButton()
        {
            if (_rewardBtn == null) return;
            var fx = _rewardBtn.GetComponent<UiPressFx>();
            bool ready = Game.Save != null && Game.Save.GetInt("clear_reward", 0) > 0;
            Color normal = ready ? new Color(0.72f, 0.56f, 0.16f, 0.98f) : new Color(0.2f, 0.22f, 0.23f, 0.95f);
            Color hover = ready ? new Color(0.92f, 0.76f, 0.28f, 1f) : new Color(0.36f, 0.43f, 0.3f, 1f);
            if (fx != null) { fx.Normal = normal; fx.Hover = hover; }
            var label = _rewardBtn.GetComponentInChildren<Text>();
            if (label != null) label.color = ready ? new Color(1f, 0.97f, 0.86f) : new Color(0.93f, 0.94f, 0.87f);
        }

        private int Discounted(int cost)
        {
            return Mathf.Max(1, Mathf.RoundToInt(cost * (1f - _discount)));
        }

        /// <summary>One free reward per cleared run; the deeper the clear, the better.</summary>
        private void GrantClearReward()
        {
            if (Game.Save == null) return;
            if (Game.Save.GetInt("clear_reward", 0) <= 0)
            {
                SetShopStatus("还没有可领取的通关奖励——赢一局再来");
                return;
            }

            int rawWave = Game.Save.GetInt("clear_wave", 1);
            int wave = Mathf.Clamp(rawWave, 1, 30);
            bool endless = rawWave > 30;
            float mult = endless ? 1.5f : 1f;

            int powder = Mathf.RoundToInt((20 + wave * 4) * mult);
            int iron = Mathf.RoundToInt((2 + wave / 4f) * mult);

            Game.Stash.TryAdd("ammo_huoyao", powder);
            Game.Stash.TryAdd("mat_shengtie", iron);
            if (wave >= 25) Game.Stash.TryAdd("wep_dao_jing", 1);
            else if (wave >= 15) Game.Stash.TryAdd("wep_gong", 1);
            if (wave >= 10) Game.Stash.TryAdd("tool_bengdai", 2);

            Game.Save.SetInt("clear_reward", 0);
            SetShopStatus("通关奖励（" + (endless ? "无尽 " : "") + "第 " + rawWave + " 波"
                + (endless ? "，已按 30 波基础 ×1.5" : "") + "）：火药 x" + powder + "、生铁 x" + iron);
            RefreshRewardButton();
            Persist();
        }

        private void TakeRelief()
        {
            bool hasWeapon = false;
            bool hasAmmo = false;
            InventoryModel[] models = new InventoryModel[] { Game.Inventory, Game.Stash };
            for (int m = 0; m < models.Length; m++)
            {
                var model = models[m];
                if (model == null) continue;
                for (int i = 0; i < InventoryModel.Capacity; i++)
                {
                    var s = model.Slots[i];
                    if (s.IsEmpty) continue;
                    var def = Game.Items != null ? Game.Items.Get(s.itemId) : null;
                    if (def == null) continue;
                    if (def.IsWeapon) hasWeapon = true;
                    if (def.ammoItemId == "ammo_huoyao" && model.CountOf("ammo_huoyao") > 0) hasAmmo = true;
                }
            }

            bool hasMelee = false;
            bool hasGun = false;
            for (int m = 0; m < models.Length; m++)
            {
                var model = models[m];
                if (model == null) continue;
                for (int i = 0; i < InventoryModel.Capacity; i++)
                {
                    var s = model.Slots[i];
                    if (s.IsEmpty) continue;
                    var def = Game.Items != null ? Game.Items.Get(s.itemId) : null;
                    if (def == null || !def.IsWeapon) continue;
                    if (def.IsMelee) hasMelee = true;
                    if (def.IsRanged) hasGun = true;
                }
            }

            bool gaveSomething = false;

            // a melee weapon is mandatory: without it the player cannot fight at short range
            if (!hasMelee) { Game.Stash.TryAdd("wep_dao", 1); gaveSomething = true; }

            if (!hasGun) { Game.Stash.TryAdd("wep_huoqiang", 1); gaveSomething = true; }

            if (!hasAmmo)
            {
                // both gun and ammo missing -> half the usual ammo
                int amount = hasGun ? 60 : 30;
                Game.Stash.TryAdd("ammo_huoyao", amount);
                gaveSomething = true;
            }

            // relief always carries field medicine plus one random supply (feedback #14)
            Game.Stash.TryAdd("tool_bengdai", 2);
            string[] extra = new string[] { "tool_grenade", "ammo_huoyao", "tool_fence", "ammo_yumaojian", "mat_yaocao" };
            string pick = extra[Random.Range(0, extra.Length)];
            int bonusAmount = pick.StartsWith("ammo_") ? 15 : 1;
            Game.Stash.TryAdd(pick, bonusAmount);
            var pickDef = Game.Items != null ? Game.Items.Get(pick) : null;
            string extraName = pickDef != null ? pickDef.name : pick;

            if (!gaveSomething)
                SetShopStatus("补发了绷带 x2 与 " + extraName + " x" + bonusAmount);
            else
                SetShopStatus("补发了缺的装备、绷带 x2 与 " + extraName + " x" + bonusAmount);
            Persist();
        }

        private void SetShopStatus(string s)
        {
            if (_shopStatus != null) _shopStatus.text = s;
            Refresh();
        }

        // ------------------------------------------------------------------ smith

        private void BuildSmith()
        {
            var sub = UiFactory.CreateText(_smithRoot, "Sub", "铁匠：修理、强化、改造武器", 19, new Color(0.62f, 0.66f, 0.6f), TextAnchor.MiddleLeft);
            UiFactory.Anchor(sub.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(52f, -74f), new Vector2(1000f, 26f));

            var back = UiFactory.CreateButton(_smithRoot, "Back", "< 返回", 22, () => ShowView(View.Hub));
            UiFactory.Anchor((RectTransform)back.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-100f, -18f), new Vector2(130f, 44f));

            {
                var smithPortrait = Resources.Load<Sprite>("Portrait/bust_smith");
                if (smithPortrait == null) smithPortrait = Resources.Load<Sprite>("Portrait/smith");
                if (smithPortrait == null)
                {
                    var art = Resources.LoadAll<Sprite>("Hub/sheet_smith");
                    if (art != null && art.Length > 0) smithPortrait = art[0];
                }
                BuildNpcPortrait(_smithRoot, smithPortrait, false);
            }

            SmithRow(0, "修理武器", "消耗 生铁 x2，把背包/仓库里的武器耐久补满", () => RepairWeapons());
            SmithRow(1, "强化武器 +1", "消耗 生铁 x3 + 火药 x1，当前武器伤害 +6（最高 +10 级）", EnhanceWeapon);
            SmithRow(2, "砍刀改造", "消耗 砍刀 x1 + 生铁 x4，改造为精钢砍刀", ConvertKnife);

            _smithStatus = UiFactory.CreateText(_smithRoot, "Status", "", 18, new Color(0.78f, 0.82f, 0.74f), TextAnchor.MiddleLeft);
            UiFactory.Anchor(_smithStatus.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(440f, 24f), new Vector2(1000f, 56f));
            _smithStatus.horizontalOverflow = HorizontalWrapMode.Wrap;

            BuildModSection();
        }

        private void SmithRow(int index, string title, string desc, System.Action action)
        {
            var row = UiFactory.CreateImage(_smithRoot, "Row" + index, UiFactory.WhiteSprite, new Color(0.16f, 0.17f, 0.18f, 0.9f));
            UiFactory.Anchor(row.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(440f, -150f - index * 116f), new Vector2(980f, 100f));

            var name = UiFactory.CreateText(row.transform, "Name", title, 24, new Color(0.9f, 0.86f, 0.72f), TextAnchor.MiddleLeft);
            UiFactory.Anchor(name.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -12f), new Vector2(600f, 30f));

            var d = UiFactory.CreateText(row.transform, "Desc", desc, 18, new Color(0.7f, 0.74f, 0.66f), TextAnchor.UpperLeft);
            UiFactory.Anchor(d.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -46f), new Vector2(700f, 46f));
            d.horizontalOverflow = HorizontalWrapMode.Wrap;

            var btn = UiFactory.CreateButton(row.transform, "Do", "执 行", 22, action);
            UiFactory.Anchor((RectTransform)btn.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-16f, 0f), new Vector2(150f, 56f));
        }

        /// <summary>Six weapon mods laid out in two columns; each can be installed or removed.</summary>
        /// <summary>Small segmented bar under a level, green as it fills (feedback #12 item 1).</summary>
        private Image[] BuildPips(Transform parent, string name, Vector2 pos, int count, float w, float h, float gap)
        {
            var row = UiFactory.CreateRect(parent, name);
            UiFactory.Anchor(row, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), pos,
                new Vector2(count * (w + gap), h));

            var pips = new Image[count];
            for (int i = 0; i < count; i++)
            {
                var pip = UiFactory.CreateImage(row, "P" + i, UiFactory.WhiteSprite, new Color(0.22f, 0.24f, 0.22f, 0.9f));
                UiFactory.Anchor(pip.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                    new Vector2(i * (w + gap), 0f), new Vector2(w, h));
                pips[i] = pip;
            }
            return pips;
        }

        private static void FillPips(Image[] pips, int filled)
        {
            if (pips == null) return;
            var on = new Color(0.45f, 1f, 0.42f, 1f);
            var off = new Color(0.2f, 0.22f, 0.2f, 0.9f);
            for (int i = 0; i < pips.Length; i++)
                if (pips[i] != null) pips[i].color = i < filled ? on : off;
        }

        private void BuildModSection()
        {
            var head = UiFactory.CreateText(_smithRoot, "ModHead", "武器改造（近战 / 远程各自独立，等级 1~10）", 21, new Color(0.9f, 0.84f, 0.66f), TextAnchor.MiddleLeft);
            UiFactory.Anchor(head.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(440f, -500f), new Vector2(900f, 28f));

            _modStates = new Text[WeaponMods.All.Length];
            _modBtns = new Button[WeaponMods.All.Length];
            _modPips = new Image[WeaponMods.All.Length][];

            for (int i = 0; i < WeaponMods.All.Length; i++)
            {
                int idx = i;
                var mod = WeaponMods.All[i];
                int col = i % 2;
                int rowIdx = i / 2;
                float x = 440f + col * 510f;
                float y = -540f - rowIdx * 104f;

                var row = UiFactory.CreateImage(_smithRoot, "Mod" + i, UiFactory.WhiteSprite, new Color(0.14f, 0.15f, 0.16f, 0.9f));
                UiFactory.Anchor(row.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, y), new Vector2(490f, 96f));

                var name = UiFactory.CreateText(row.transform, "Name", mod.name + (mod.melee ? "（近战）" : "（远程）"), 20, new Color(0.88f, 0.84f, 0.7f), TextAnchor.MiddleLeft);
                UiFactory.Anchor(name.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14f, -8f), new Vector2(250f, 26f));

                var desc = UiFactory.CreateText(row.transform, "Desc", "", 15, new Color(0.68f, 0.72f, 0.64f), TextAnchor.MiddleLeft);
                UiFactory.Anchor(desc.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14f, -36f), new Vector2(330f, 34f));
                desc.horizontalOverflow = HorizontalWrapMode.Wrap;

                var state = UiFactory.CreateText(row.transform, "State", "", 14, new Color(0.6f, 0.64f, 0.58f), TextAnchor.UpperRight);
                UiFactory.Anchor(state.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-110f, -6f), new Vector2(200f, 62f));
                _modStates[i] = state;

                var btn = UiFactory.CreateButton(row.transform, "Act", "强化", 17, () => UpgradeMod(idx));
                UiFactory.Anchor((RectTransform)btn.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-10f, 0f), new Vector2(96f, 46f));
                _modBtns[i] = btn;
                _modPips[i] = BuildPips(row.transform, "Pips", new Vector2(14f, -64f), WeaponMods.MaxLevel, 12f, 7f, 3f);
            }
        }

        private void UpgradeMod(int index)
        {
            var mod = WeaponMods.All[index];
            int lvl = WeaponMods.Level(mod.id);

            if (lvl <= 0 && !Milestones.Unlocked(mod.unlockKey))
            {
                var ms = Milestones.ByKey(mod.unlockKey);
                SetSmithStatus("尚未解锁：需要达成「" + ms.name + "」（" + ms.desc + "）");
                return;
            }
            if (lvl >= WeaponMods.MaxLevel)
            {
                SetSmithStatus(mod.name + " 已满级（Lv" + WeaponMods.MaxLevel + "）");
                return;
            }

            int needA = WeaponMods.CostA(mod);
            int needB = WeaponMods.CostB(mod);
            if (TotalCount(mod.costA) < needA || TotalCount(mod.costB) < needB)
            {
                string na = Game.Items != null && Game.Items.Get(mod.costA) != null ? Game.Items.Get(mod.costA).name : mod.costA;
                string nb = Game.Items != null && Game.Items.Get(mod.costB) != null ? Game.Items.Get(mod.costB).name : mod.costB;
                SetSmithStatus("材料不足：" + na + " " + TotalCount(mod.costA) + "/" + needA + "  " + nb + " " + TotalCount(mod.costB) + "/" + needB);
                return;
            }

            Consume(mod.costA, needA);
            Consume(mod.costB, needB);
            WeaponMods.SetLevel(mod.id, lvl + 1);
            Persist();
            SetSmithStatus(mod.name + " 提升至 Lv" + (lvl + 1) + " / " + WeaponMods.MaxLevel);
        }

        private void RefreshMods()
        {
            if (_modBtns == null) return;

            for (int i = 0; i < WeaponMods.All.Length; i++)
            {
                var mod = WeaponMods.All[i];
                int lvl = WeaponMods.Level(mod.id);
                bool unlocked = lvl > 0 || Milestones.Unlocked(mod.unlockKey);

                var label = _modBtns[i] != null ? _modBtns[i].GetComponentInChildren<Text>() : null;
                if (label != null)
                    label.text = lvl >= WeaponMods.MaxLevel ? "满级" : (unlocked ? "强化" : "未解锁");

                if (_modStates[i] != null)
                {
                    string na = Game.Items != null && Game.Items.Get(mod.costA) != null ? Game.Items.Get(mod.costA).name : mod.costA;
                    string nb = Game.Items != null && Game.Items.Get(mod.costB) != null ? Game.Items.Get(mod.costB).name : mod.costB;
                    FillPips(_modPips != null ? _modPips[i] : null, lvl);
                    if (lvl >= WeaponMods.MaxLevel)
                    {
                        _modStates[i].text = "Lv" + WeaponMods.MaxLevel + " 满级";
                        _modStates[i].color = new Color(0.7f, 0.9f, 0.6f);
                    }
                    else if (!unlocked)
                    {
                        _modStates[i].text = "需里程碑";
                        _modStates[i].color = new Color(0.75f, 0.5f, 0.4f);
                    }
                    else
                    {
                        // per-level delta + running total so the player sees what each rank buys
                        string delta = mod.perLevelLabel;
                        float total = mod.perLevel * lvl;
                        string totalTxt = total > 0.001f
                            ? "  累计 +" + total.ToString("0.00").TrimEnd('0').TrimEnd('.')
                            : "";
                        _modStates[i].text = "Lv" + lvl + " → " + (lvl + 1) + "\n" + delta + totalTxt
                            + "\n" + na + " x" + WeaponMods.CostA(mod) + "  " + nb + " x" + WeaponMods.CostB(mod);
                        _modStates[i].color = new Color(0.62f, 0.66f, 0.6f);
                    }
                }
            }
        }

        private ItemDef FirstWeapon(out InventoryModel owner, out int slot)
        {
            owner = null;
            slot = -1;
            InventoryModel[] models = new InventoryModel[] { Game.Inventory, Game.Stash };
            for (int m = 0; m < models.Length; m++)
            {
                var model = models[m];
                if (model == null) continue;
                for (int i = 0; i < InventoryModel.Capacity; i++)
                {
                    var s = model.Slots[i];
                    if (s.IsEmpty) continue;
                    var def = Game.Items != null ? Game.Items.Get(s.itemId) : null;
                    if (def != null && def.IsWeapon) { owner = model; slot = i; return def; }
                }
            }
            return null;
        }

        private void EnhanceWeapon()
        {
            InventoryModel owner;
            int slot;
            var def = FirstWeapon(out owner, out slot);
            if (def == null) { SetSmithStatus("没有可以强化的武器"); return; }

            int lvl = Enhancement.Level(def.id);
            if (lvl >= Enhancement.MaxLevel) { SetSmithStatus(def.name + " 已经强化到满级"); return; }
            int needIronS = Enhancement.DamageIronCost(lvl);
            int needPowderS = Enhancement.DamagePowderCost(lvl);
            int ironS = TotalCount("mat_shengtie");
            int powderS = TotalCount("ammo_huoyao");
            if (ironS < needIronS || powderS < needPowderS)
            {
                string miss = "";
                if (ironS < needIronS) miss += "生铁 " + ironS + "/" + needIronS + "  ";
                if (powderS < needPowderS) miss += "火药 " + powderS + "/" + needPowderS;
                SetSmithStatus("材料不足：" + miss.Trim());
                return;
            }
            Consume("mat_shengtie", needIronS);
            Consume("ammo_huoyao", needPowderS);
            Enhancement.SetLevel(def.id, lvl + 1);
            SetSmithStatus(def.name + " 强化至 +" + (lvl + 1) + "（伤害 +" + Enhancement.DamagePerLevel * (lvl + 1) + "）");
        }

        private void ConvertKnife()
        {
            if (TotalCount("wep_dao") < 1) { SetSmithStatus("没有砍刀可以改造"); return; }
            if (TotalCount("mat_shengtie") < 4) { SetSmithStatus("材料不足：需要 生铁 x4"); return; }
            Consume("wep_dao", 1);
            Consume("mat_shengtie", 4);
            Game.Stash.TryAdd("wep_dao_jing", 1);
            SetSmithStatus("砍刀已改造为精钢砍刀");
            Persist();
        }

        private void SetSmithStatus(string s)
        {
            if (_smithStatus != null) _smithStatus.text = s;
            Refresh();
        }

        // ------------------------------------------------------------------ shared actions

        private void RepairWeapons()
        {
            if (TotalCount("mat_shengtie") < 2)
            {
                if (_smithStatus != null) _smithStatus.text = "生铁不足（需要 2）";
                if (_bagHint != null) _bagHint.text = "生铁不足（需要 2）";
                return;
            }
            InventoryModel[] models = new InventoryModel[] { Game.Inventory, Game.Stash };
            for (int m = 0; m < models.Length; m++)
            {
                var model = models[m];
                if (model == null) continue;
                for (int i = 0; i < InventoryModel.Capacity; i++)
                {
                    var s = model.Slots[i];
                    if (s.IsEmpty) continue;
                    var def = Game.Items != null ? Game.Items.Get(s.itemId) : null;
                    if (def == null || !def.IsWeapon || def.durabilityMax <= 0) continue;
                    if (s.durability >= def.durabilityMax) continue;

                    Consume("mat_shengtie", 2);
                    s.durability = def.durabilityMax;
                    if (_smithStatus != null) _smithStatus.text = def.name + " 已修复";
                    if (_bagHint != null) _bagHint.text = def.name + " 已修复";
                    Persist();
                    return;
                }
            }
            if (_smithStatus != null) _smithStatus.text = "没有需要修理的武器";
            if (_bagHint != null) _bagHint.text = "没有需要修理的武器";
        }

        private void SortStash()
        {
            var stash = Game.Stash;
            if (stash == null) return;
            var snapshot = new List<InventorySlot>();
            for (int i = 0; i < InventoryModel.Capacity; i++)
            {
                var s = stash.Slots[i];
                if (!s.IsEmpty) snapshot.Add(new InventorySlot { itemId = s.itemId, count = s.count, durability = s.durability });
            }
            stash.Clear();
            for (int i = 0; i < snapshot.Count; i++)
                stash.TryAdd(snapshot[i].itemId, snapshot[i].count);
            Persist();
        }

        private void Deploy()
        {
            if (Game.Inventory == null || Game.Inventory.UsedSlots == 0)
            {
                if (_hubStatus != null) _hubStatus.text = "背包是空的：进「背包」把仓库物资点进随身背包再出战。";
                return;
            }
            SetOpen(false);
            if (Game.Manager != null) Game.Manager.StartRun(false, 0, Game.Manager.TotalWaves > 0 ? Game.Manager.TotalWaves : 10);
        }

        private void Persist()
        {
            if (Game.Save == null) return;
            if (Game.Stash != null) Game.Save.SaveStash(Game.Stash);
            if (Game.Inventory != null) Game.Save.SaveInventory(Game.Inventory);
            Refresh();
        }

        // ------------------------------------------------------------------ armory

        private sealed class ArmorRow
        {
            public Text state;
            public Text cost;
            public Image icon;
            public Image durability;
            public Button button;
        }

        private ArmorRow[] _armorRows;

        private void BuildArmory()
        {
            var sub = UiFactory.CreateText(_armoryRoot, "Sub", "装备栏：在铁匠铺用材料打造、强化与修理（耐久归零后失效）", 19, new Color(0.62f, 0.66f, 0.6f), TextAnchor.MiddleLeft);
            UiFactory.Anchor(sub.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(52f, -74f), new Vector2(1100f, 26f));

            var back = UiFactory.CreateButton(_armoryRoot, "Back", "< 返回", 22, () => ShowView(View.Hub));
            UiFactory.Anchor((RectTransform)back.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-100f, -18f), new Vector2(130f, 44f));

            _armorRows = new ArmorRow[Armors.All.Length];
            _armorPips = new Image[Armors.All.Length][];
            for (int i = 0; i < Armors.All.Length; i++)
            {
                int idx = i;
                var def = Armors.All[i];

                var row = UiFactory.CreateImage(_armoryRoot, "Armor" + i, UiFactory.WhiteSprite, new Color(0.15f, 0.16f, 0.17f, 0.92f));
                UiFactory.Anchor(row.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(120f, -140f - i * 170f), new Vector2(1300f, 150f));

                var icon = UiFactory.CreateImage(row.transform, "Icon", Resources.Load<Sprite>(def.icon), Color.white);
                UiFactory.Anchor(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(20f, 0f), new Vector2(110f, 110f));
                icon.preserveAspect = true;

                var name = UiFactory.CreateText(row.transform, "Name", def.name, 25, new Color(0.92f, 0.88f, 0.74f), TextAnchor.UpperLeft);
                UiFactory.Anchor(name.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(148f, -16f), new Vector2(560f, 30f));

                var desc = UiFactory.CreateText(row.transform, "Desc", def.desc, 17, new Color(0.7f, 0.74f, 0.66f), TextAnchor.UpperLeft);
                UiFactory.Anchor(desc.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(148f, -50f), new Vector2(640f, 46f));
                desc.horizontalOverflow = HorizontalWrapMode.Wrap;

                var state = UiFactory.CreateText(row.transform, "State", "", 18, new Color(0.74f, 0.8f, 0.72f), TextAnchor.UpperLeft);
                UiFactory.Anchor(state.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(148f, 14f), new Vector2(560f, 46f));

                var barBg = UiFactory.CreateImage(row.transform, "BarBg", UiFactory.WhiteSprite, new Color(0.08f, 0.08f, 0.09f, 0.9f));
                UiFactory.Anchor(barBg.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(720f, 26f), new Vector2(220f, 14f));
                var dur = UiFactory.CreateImage(barBg.transform, "Bar", UiFactory.WhiteSprite, new Color(0.55f, 0.75f, 0.55f, 1f));
                dur.rectTransform.anchorMin = new Vector2(0f, 0f);
                dur.rectTransform.anchorMax = new Vector2(1f, 1f);
                dur.rectTransform.offsetMin = Vector2.zero;
                dur.rectTransform.offsetMax = Vector2.zero;
                dur.type = Image.Type.Filled;
                dur.fillMethod = Image.FillMethod.Horizontal;
                dur.fillOrigin = 0;

                var cost = UiFactory.CreateText(row.transform, "Cost", "", 15, new Color(0.66f, 0.7f, 0.62f), TextAnchor.UpperLeft);
                UiFactory.Anchor(cost.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(720f, -56f), new Vector2(360f, 50f));
                cost.horizontalOverflow = HorizontalWrapMode.Wrap;

                var btn = UiFactory.CreateButton(row.transform, "Act", "打造", 20, () => OnArmorAction(idx));
                UiFactory.Anchor((RectTransform)btn.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-24f, 0f), new Vector2(170f, 56f));

                _armorPips[i] = BuildPips(row.transform, "Pips", new Vector2(148f, -118f), Armors.MaxLevel, 14f, 8f, 4f);
                _armorRows[i] = new ArmorRow { state = state, cost = cost, icon = icon, durability = dur, button = btn };
            }

            var hint = UiFactory.CreateText(_armoryRoot, "Hint", "穿戴中的装备会在受击时抵扣伤害并消耗耐久；耐久归零后不再提供防护。", 16, new Color(0.62f, 0.66f, 0.6f), TextAnchor.MiddleCenter);
            UiFactory.Anchor(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 34f), new Vector2(1300f, 40f));
        }

        private void OnArmorAction(int index)
        {
            var def = Armors.All[index];
            bool owned = Armors.Owned(def.id);

            if (!owned)
            {
                if (TotalCount(def.costA) < def.costACount || TotalCount(def.costB) < def.costBCount)
                {
                    SetArmorStatus(index, "材料不足");
                    return;
                }
                Consume(def.costA, def.costACount);
                Consume(def.costB, def.costBCount);
                Armors.Craft(def.id);
                SetArmorStatus(index, "打造完成");
                Persist();
                return;
            }

            if (Armors.Durability(def.id) <= 0)
            {
                int needA = 2, needB = 1;
                if (TotalCount(def.costA) < needA || TotalCount(def.costB) < needB)
                {
                    SetArmorStatus(index, "修理材料不足");
                    return;
                }
                Consume(def.costA, needA);
                Consume(def.costB, needB);
                Armors.Repair(def.id);
                SetArmorStatus(index, "已修理");
                Persist();
                return;
            }

            int lvl = Armors.Level(def.id);
            if (lvl >= Armors.MaxLevel)
            {
                SetArmorStatus(index, "已满级");
                return;
            }
            int needI = 4 + lvl * 3;
            int needJ = 2 + lvl;
            if (TotalCount(def.costA) < needI || TotalCount("mat_jiaodai") < Mathf.Min(2, needJ))
            {
                SetArmorStatus(index, "强化材料不足");
                return;
            }
            Consume(def.costA, needI);
            Consume("mat_jiaodai", Mathf.Min(2, needJ));
            Armors.Upgrade(def.id);
            SetArmorStatus(index, "强化至 Lv" + (lvl + 1));
            Persist();
        }

        private void SetArmorStatus(int index, string msg)
        {
            if (_armorRows == null || index < 0 || index >= _armorRows.Length) return;
            RefreshArmory();
            if (_armorRows[index].state != null) _armorRows[index].state.text = msg;
        }

        private void RefreshArmory()
        {
            if (_armorRows == null) return;
            for (int i = 0; i < Armors.All.Length; i++)
            {
                var def = Armors.All[i];
                var row = _armorRows[i];
                if (row == null) continue;

                bool owned = Armors.Owned(def.id);
                int lvl = Armors.Level(def.id);
                int dur = Armors.Durability(def.id);
                int max = Armors.DurabilityMax(def.id);

                if (row.icon != null) row.icon.color = owned ? Color.white : new Color(1f, 1f, 1f, 0.35f);

                if (row.state != null)
                {
                    if (!owned) row.state.text = "未拥有   减伤 " + def.defensePercent + "%";
                    else if (dur <= 0) row.state.text = "已损坏   耐久 0/" + max + "   Lv" + lvl;
                    else row.state.text = "减伤 " + Armors.DefensePercent(def.id) + "%   耐久 " + dur + "/" + max + "   Lv" + lvl;
                    row.state.color = !owned ? new Color(0.6f, 0.64f, 0.6f)
                        : (dur <= 0 ? new Color(0.85f, 0.5f, 0.42f) : new Color(0.74f, 0.8f, 0.72f));
                }

                FillPips(_armorPips != null ? _armorPips[i] : null, owned ? lvl : 0);

                if (row.durability != null)
                {
                    row.durability.fillAmount = max > 0 ? Mathf.Clamp01(dur / (float)max) : 0f;
                    row.durability.color = dur <= 0 ? new Color(0.8f, 0.35f, 0.3f, 1f) : new Color(0.55f, 0.75f, 0.55f, 1f);
                }

                if (row.cost != null)
                {
                    if (!owned)
                        row.cost.text = "打造：" + ItemName(def.costA) + " x" + def.costACount + " + " + ItemName(def.costB) + " x" + def.costBCount;
                    else if (dur <= 0)
                        row.cost.text = "修理：" + ItemName(def.costA) + " x2 + " + ItemName(def.costB) + " x1";
                    else
                        row.cost.text = "强化：" + ItemName(def.costA) + " x" + (4 + lvl * 3) + " + 胶带 x" + Mathf.Min(2, 2 + lvl);
                }

                var label = row.button != null ? row.button.GetComponentInChildren<Text>() : null;
                if (label != null)
                    label.text = !owned ? "打造" : (dur <= 0 ? "修理" : (lvl >= Armors.MaxLevel ? "满级" : "强化"));
            }
        }

        private string ItemName(string id)
        {
            var d = Game.Items != null ? Game.Items.Get(id) : null;
            return d != null ? d.name : id;
        }


        // ------------------------------------------------------------------ npc chatter

        private static readonly string[] MerchantLines = new string[]
        {
            "货都是真的，价格也是真的。这年头没人拿命开玩笑。",
            "昨天有个家伙拿半箱子弹换我一个罐头，你猜他今天还活着吗？",
            "别问我从哪儿弄来的，问了我就得涨价。",
            "铁和木头永远不嫌多。你手里的刀，早晚要换的。",
            "封锁线那边每天都有直升机飞，可从来没人下来过。",
            "我年轻的时候卖保险，现在卖命。其实差不多。",
            "你要是撑到第十波，记得回来——我给你留点好东西。",
            "感染者不吃生铁，可惜它们吃你。",
            "我这包比我还重，但背着它我才睡得着。",
            "有人管我叫奸商。我只管自己叫活着的人。",
            "火药潮了就别用了。炸不响，你还得赔我一条命。",
            "竞技场那边的广播塔一响，全城的怪物都往这儿走。",
            "别盯着我的货看太久，看久了你会想买。",
            "物资是死的，人是活的。可人死了，物资就归我了。",
            "你要是能通关，我请你喝一瓶真正的汽水——不掺水的那种。",
        };

        private static readonly string[] SmithLines = new string[]
        {
            "锤子落下去的时候，别眨眼。",
            "这刀的钢口，是从一辆报废卡车的弹簧上取的。",
            "武器会说话。磨损的声音就是它在喊疼。",
            "我修过最奇怪的东西是一支口琴。它主人再也没来取。",
            "你想让它更长、更重，还是更锋利？只能选一样。",
            "生铁不够就别拿来糊弄我。糊弄出来的东西会断在你手里。",
            "以前我在修理厂干活，现在也差不多，只是换成刀和枪。",
            "护甲要贴身。松一寸，就等于给别人开一道口子。",
            "敲了一辈子铁，耳朵早就听不见了。倒也清静。",
            "刀钝了可以磨，人钝了就只能喂感染者。",
            "每次锻造完，我都会在柄上刻一道。你数数就知道我救过多少人。",
            "别嫌它丑。战场上好看的东西一般都先断。",
            "庇护所的发电机一停，这炉子就凉了。所以要守住。",
            "你要是死在外面，别忘了把刀带回来——那刀比你有用。",
            "好了，火候到了。剩下的交给运气。",
        };

        private void ShowNpcLine(bool merchant, Text speech)
        {
            if (speech == null) return;
            var list = merchant ? MerchantLines : SmithLines;
            speech.text = list[Random.Range(0, list.Length)];
            speech.transform.parent.gameObject.SetActive(true);
            AudioManager.PlaySfx("sfx_click", 0.5f, 0.15f);
        }

        /// <summary>Large bust portrait on the left; clicking it pops a dialogue box under it.</summary>
        private void BuildNpcPortrait(RectTransform root, Sprite art, bool merchant)
        {
            if (art == null) return;

            var frame = UiFactory.CreateImage(root, "PortraitFrame", UiFactory.WhiteSprite, new Color(0.12f, 0.13f, 0.15f, 0.96f));
            UiFactory.Anchor(frame.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(40f, -110f), new Vector2(360f, 430f));

            var img = UiFactory.CreateImage(frame.transform, "Art", art, Color.white);
            UiFactory.Anchor(img.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(340f, 410f));
            img.preserveAspect = true;
            img.raycastTarget = true;

            // dialogue box, hidden until the portrait is clicked
            var box = UiFactory.CreateImage(root, "SpeechBox", UiFactory.PanelSprite, Color.white);
            if (UiFactory.PanelSprite == null) box.color = new Color(0.1f, 0.11f, 0.12f, 0.96f);
            box.type = Image.Type.Sliced;
            UiFactory.Anchor(box.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(40f, 60f), new Vector2(360f, 170f));

            var speech = UiFactory.CreateText(box.transform, "Speech", "", 19, new Color(0.88f, 0.84f, 0.7f), TextAnchor.UpperLeft);
            UiFactory.Anchor(speech.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(18f, -16f), new Vector2(324f, 138f));
            speech.horizontalOverflow = HorizontalWrapMode.Wrap;

            var hint = UiFactory.CreateText(box.transform, "Hint", "— 点击立绘继续聊天", 14, new Color(0.6f, 0.63f, 0.58f), TextAnchor.LowerRight);
            UiFactory.Anchor(hint.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-14f, 8f), new Vector2(240f, 20f));

            box.gameObject.SetActive(false);

            var btn = img.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => ShowNpcLine(merchant, speech));
        }

        // ------------------------------------------------------------------ milestones

        private void BuildMilestones()
        {
            var sub = UiFactory.CreateText(_msRoot, "Sub", "里程碑：达成后解锁更强的改造与商品", 19, new Color(0.62f, 0.66f, 0.6f), TextAnchor.MiddleLeft);
            UiFactory.Anchor(sub.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(52f, -74f), new Vector2(1000f, 26f));

            var back = UiFactory.CreateButton(_msRoot, "Back", "< 返回", 22, () => ShowView(View.Hub));
            UiFactory.Anchor((RectTransform)back.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-100f, -18f), new Vector2(130f, 44f));

            _msRows = new Text[Milestones.All.Length];
            for (int i = 0; i < Milestones.All.Length; i++)
            {
                var d = Milestones.All[i];
                // two columns so twelve milestones still fit on one screen
                int col = i % 2;
                int rowIdx = i / 2;
                float rx = 120f + col * 660f;
                float ry = -150f - rowIdx * 104f;

                var row = UiFactory.CreateImage(_msRoot, "Row" + i, UiFactory.WhiteSprite, new Color(0.16f, 0.17f, 0.18f, 0.9f));
                UiFactory.Anchor(row.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(rx, ry), new Vector2(630f, 92f));

                var name = UiFactory.CreateText(row.transform, "Name", d.name, 21, new Color(0.9f, 0.86f, 0.72f), TextAnchor.MiddleLeft);
                UiFactory.Anchor(name.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -8f), new Vector2(300f, 26f));

                var desc = UiFactory.CreateText(row.transform, "Desc", d.desc, 16, new Color(0.7f, 0.74f, 0.66f), TextAnchor.UpperLeft);
                UiFactory.Anchor(desc.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -40f), new Vector2(420f, 26f));

                var progress = UiFactory.CreateText(row.transform, "Progress", "", 19, new Color(0.78f, 0.82f, 0.7f), TextAnchor.MiddleRight);
                UiFactory.Anchor(progress.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-20f, 0f), new Vector2(240f, 32f));
                _msRows[i] = progress;
            }
        }

        private void RefreshMilestones()
        {
            if (_msRows == null) return;
            for (int i = 0; i < Milestones.All.Length; i++)
            {
                var d = Milestones.All[i];
                int v = Milestones.Value(d.stat);
                bool done = v >= d.target;
                if (_msRows[i] != null)
                {
                    _msRows[i].text = done ? ("已达成  " + Mathf.Min(v, d.target) + "/" + d.target)
                                           : (v + " / " + d.target);
                    _msRows[i].color = done ? new Color(0.68f, 0.9f, 0.55f) : new Color(0.78f, 0.82f, 0.7f);
                }
            }
        }

        // ------------------------------------------------------------------ clicks / tips

        /// <summary>Drag & drop: swap two cells, or move a stack across the two containers.</summary>
        private void OnDragDropped(int fromIndex, bool fromStash, int toIndex, bool toStash)
        {
            var from = fromStash ? Game.Stash : Game.Inventory;
            var to = toStash ? Game.Stash : Game.Inventory;
            if (from == null || to == null) return;
            if (fromIndex < 0 || fromIndex >= InventoryModel.Capacity) return;
            if (toIndex < 0 || toIndex >= InventoryModel.Capacity) return;

            var a = from.Slots[fromIndex];
            if (a.IsEmpty) return;

            if (fromStash == toStash)
            {
                // swap within the same container
                if (fromIndex == toIndex) return;
                var b = from.Slots[toIndex];
                var tmp = new InventorySlot { itemId = b.itemId, count = b.count, durability = b.durability };
                from.Slots[toIndex].itemId = a.itemId;
                from.Slots[toIndex].count = a.count;
                from.Slots[toIndex].durability = a.durability;
                from.Slots[fromIndex].itemId = tmp.itemId;
                from.Slots[fromIndex].count = tmp.count;
                from.Slots[fromIndex].durability = tmp.durability;
                from.RaiseChanged();
            }
            else
            {
                var b = to.Slots[toIndex];
                if (b.IsEmpty)
                {
                    if (Game.Manager != null) Game.Manager.TryTransfer(from, fromIndex, to);
                }
                else
                {
                    // swap across containers: move b back into a's cell
                    string aId = a.itemId; int aCount = a.count; int aDur = a.durability;
                    from.Slots[fromIndex].itemId = b.itemId;
                    from.Slots[fromIndex].count = b.count;
                    from.Slots[fromIndex].durability = b.durability;
                    to.Slots[toIndex].itemId = aId;
                    to.Slots[toIndex].count = aCount;
                    to.Slots[toIndex].durability = aDur;
                    from.RaiseChanged();
                    to.RaiseChanged();
                }
            }

            if (Game.Save != null)
            {
                if (Game.Stash != null) Game.Save.SaveStash(Game.Stash);
                if (Game.Inventory != null) Game.Save.SaveInventory(Game.Inventory);
            }
            if (Game.Equipment != null) Game.Equipment.Refresh();
            Refresh();
        }

        private void OnStashClick(int index)
        {
            if (Game.Manager != null) Game.Manager.TryTransfer(Game.Stash, index, Game.Inventory);
            Refresh();
        }

        private void OnBagClick(int index)
        {
            if (Game.Manager != null) Game.Manager.TryTransfer(Game.Inventory, index, Game.Stash);
            Refresh();
        }

        private void BuildTooltip()
        {
            var tipBg = UiFactory.CreateImage(_canvas.transform, "Tip", UiFactory.PanelSprite, Color.white);
            if (UiFactory.PanelSprite == null) tipBg.color = new Color(0.06f, 0.07f, 0.08f, 0.98f);
            tipBg.type = Image.Type.Sliced;
            tipBg.raycastTarget = false;
            _tooltip = tipBg.rectTransform;
            UiFactory.Anchor(_tooltip, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(380f, 230f));

            _tipTitle = UiFactory.CreateText(tipBg.transform, "T", "", 22, new Color(0.96f, 0.9f, 0.72f), TextAnchor.UpperLeft);
            UiFactory.Anchor(_tipTitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -12f), new Vector2(348f, 28f));

            _tipBody = UiFactory.CreateText(tipBg.transform, "B", "", 17, new Color(0.8f, 0.84f, 0.78f), TextAnchor.UpperLeft);
            UiFactory.Anchor(_tipBody.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -46f), new Vector2(348f, 172f));
            _tipBody.horizontalOverflow = HorizontalWrapMode.Wrap;
            _tooltip.gameObject.SetActive(false);
        }

        private void ShowTip(InventoryModel model, int index, RectTransform anchor)
        {
            _tipAnchor = anchor;
            if (model == null || _tooltip == null) return;
            var s = model.Slots[index];
            if (s.IsEmpty) { HideTip(); return; }
            var def = Game.Items != null ? Game.Items.Get(s.itemId) : null;
            if (def == null) { HideTip(); return; }
            int lvl = Enhancement.Level(def.id);
            int trait = Enhancement.TraitLevel(def.id);
            string suffix = lvl > 0 ? "  +" + lvl : "";
            if (trait > 0) suffix += "  ◆" + trait;
            _tipTitle.text = def.name + suffix;
            _tipBody.text = InventoryUI.DescribeItem(def, s);

            _tooltip.gameObject.SetActive(true);
            float h = Mathf.Clamp(_tipBody.preferredHeight + 84f, 110f, 460f);
            _tooltip.sizeDelta = new Vector2(380f, h);
            _tipBody.rectTransform.sizeDelta = new Vector2(348f, h - 74f);
        }

        private void HideTip() { if (_tooltip != null) _tooltip.gameObject.SetActive(false); }

        // ------------------------------------------------------------------ state

        private void ShowView(View v)
        {
            _view = v;
            if (_hubRoot != null) _hubRoot.gameObject.SetActive(v == View.Hub);
            if (_bagRoot != null) _bagRoot.gameObject.SetActive(v == View.Bag);
            if (_shopRoot != null) _shopRoot.gameObject.SetActive(v == View.Merchant);
            if (_smithRoot != null) _smithRoot.gameObject.SetActive(v == View.Smith);
            if (_msRoot != null) _msRoot.gameObject.SetActive(v == View.Milestones);
            if (_armoryRoot != null) _armoryRoot.gameObject.SetActive(v == View.Armory);
            HideTip();
            Refresh();

            if (_open)
            {
                string track = v == View.Merchant ? "music_merchant"
                             : (v == View.Smith ? "music_smith" : "music_shelter");
                AudioManager.PlayMusic(track, 0.8f);
            }
        }

        public void SetOpen(bool open)
        {
            _open = open;
            AudioManager.PlayMusic(open ? "music_shelter" : "music_menu", 0.8f);
            if (_canvas != null) _canvas.enabled = open;
            if (open) { ShowView(View.Hub); }
            else HideTip();
        }

        public void Refresh()
        {
            var stash = Game.Stash;
            var bag = Game.Inventory;
            if (stash == null || _stashBgs == null) return;

            FillGrid(stash, _stashBgs, _stashIcons, _stashCounts, _stashNames);
            if (bag != null) FillGrid(bag, _bagBgs, _bagIcons, _bagCounts, _bagNames);

            if (_stashHeader != null) _stashHeader.text = "仓库  " + stash.UsedSlots + "/30 · " + stash.TotalItemCount + " 件";
            if (_bagHeader != null && bag != null) _bagHeader.text = "随身背包  " + bag.UsedSlots + "/30 · " + bag.TotalItemCount + " 件";
            if (_bagHint != null && string.IsNullOrEmpty(_bagHint.text))
                _bagHint.text = "胜利后背包内的战利品会全部存入仓库；失败或中途退出则随身背包清空。";

            RefreshMilestones();
            RefreshMods();
            RefreshArmory();

            if (_hubStatus != null)
            {
                int bagCount = bag != null ? bag.UsedSlots : 0;
                bool empty = stash.TotalItemCount == 0 && (bag == null || bag.TotalItemCount == 0);
                if (empty)
                    _hubStatus.text = "仓库和背包都是空的 —— 去「商人」领取救济物资，或购买补给。";
                else
                    _hubStatus.text = "仓库 " + stash.UsedSlots + "/30 格 · 随身背包 " + bagCount + "/30 格\n"
                        + "出战时只带走背包里的物资。";
            }
        }

        private void FillGrid(InventoryModel model, Image[] bgs, Image[] icons, Text[] counts, Text[] names)
        {
            for (int i = 0; i < InventoryModel.Capacity; i++)
            {
                var s = model.Slots[i];
                bool has = !s.IsEmpty;
                var def = has && Game.Items != null ? Game.Items.Get(s.itemId) : null;

                if (icons[i] != null)
                {
                    icons[i].enabled = has;
                    icons[i].sprite = def != null ? Game.Items.GetIcon(def.icon) : null;
                }
                if (counts[i] != null) counts[i].text = has && s.count > 1 ? s.count.ToString() : "";
                if (names[i] != null)
                {
                    int lvl = def != null ? Enhancement.Level(def.id) : 0;
                    names[i].text = def != null ? (def.name + (lvl > 0 ? "+" + lvl : "")) : "";
                    names[i].color = has ? new Color(0.14f, 0.13f, 0.11f) : new Color(0f, 0f, 0f, 0f);
                }
                if (bgs[i] != null)
                    bgs[i].color = has ? new Color(0.86f, 0.85f, 0.78f, 1f) : new Color(0.26f, 0.27f, 0.29f, 0.85f);
            }
        }

        private void LateUpdate()
        {
            if (_tooltip == null || !_tooltip.gameObject.activeSelf) return;
            var root = (RectTransform)_canvas.transform;
            Vector2 local;
            Vector2 anchorLocal = Vector2.zero;
            bool have = false;
            if (_tipAnchor != null)
            {
                Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, _tipAnchor.position);
                have = RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, null, out anchorLocal);
            }
            if (!have)
                have = RectTransformUtility.ScreenPointToLocalPointInRectangle(root, Input.mousePosition, null, out anchorLocal);
            if (!have) return;

            float w = _tooltip.sizeDelta.x;
            float h = _tooltip.sizeDelta.y;
            float halfW = w * 0.5f;
            float halfH = h * 0.5f;
            var r = root.rect;
            float anchorHalfW = _tipAnchor != null ? _tipAnchor.rect.width * 0.5f : 0f;

            float x = anchorLocal.x + anchorHalfW + 14f + halfW;
            if (x + halfW > r.xMax - 10f) x = anchorLocal.x - anchorHalfW - 14f - halfW;
            x = Mathf.Clamp(x, r.xMin + 10f + halfW, r.xMax - 10f - halfW);

            float y = Mathf.Clamp(anchorLocal.y, r.yMin + 10f + halfH, r.yMax - 10f - halfH);
            _tooltip.anchoredPosition = new Vector2(x, y);
        }
    }
}
