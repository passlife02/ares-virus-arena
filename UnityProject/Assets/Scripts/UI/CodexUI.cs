using UnityEngine;
using UnityEngine.UI;

namespace Ares
{
    /// <summary>
    /// Monster codex (feedback #7 / #12): thumbnails on the left, an animated portrait on the
    /// right, and left/right arrows to page through the entries (including both bosses).
    /// </summary>
    public class CodexUI : MonoBehaviour
    {
        private Canvas _canvas;
        private RectTransform _panel;
        private Image _portrait;
        private Text _title;
        private Text _stats;
        private Text _desc;
        private Text _pageText;
        private int _selected;
        private Sprite[] _frames;
        private int _frameIndex;
        private float _frameTimer;

        private struct Entry
        {
            public string name;
            public string sheet;   // Resources path (may contain '/')
            public string stats;
            public string desc;
        }

        private static readonly Entry[] Entries = new Entry[]
        {
            new Entry {
                name = "触手怪",
                sheet = "Codex/chushou_sheet",
                stats = "生命 46   ·   移动 2.65   ·   伤害 10\n攻击前摇 0.45s   ·   攻击间隔 1.15s   ·   近战范围 1.05",
                desc = "最常见的感染者。会绕过障碍一路贴近，靠近后有明显的前摇（身体泛红），" +
                       "抓紧这段时间翻滚或者拉开距离。掉落木头、胶带、生铁，小概率掉落砍刀。"
            },
            new Entry {
                name = "呕吐者",
                sheet = "Codex/outuzhe_sheet",
                stats = "生命 34   ·   移动 1.90   ·   伤害 8\n攻击前摇 0.70s   ·   攻击间隔 2.00s   ·   射程 9.5",
                desc = "保持距离的远程单位。前摇时通体泛绿，随后吐出酸液弹丸；弹丸会被墙体挡住，" +
                       "所以贴墙绕柱是有效的应对。血量低、移动慢，优先击杀能减少弹幕压力。"
            },
            new Entry {
                name = "穿山甲 BOSS",
                sheet = "Art10/BossArmadillo",
                stats = "生命 ×3.5   ·   伤害 ×2.2   ·   体型 ×1.55\n翻滚冲撞：蹲伏 0.7s 后以约 3 倍速度突进 0.95s",
                desc = "四足甲兽。先蹲伏扬尘，再裹成一团高速滚向玩家，撞击造成 125% 伤害并击退；" +
                       "落地会砸出地震波与土石。血量越低翻滚越频繁（20 秒 → 5 秒）。"
            },
            new Entry {
                name = "蜘蛛 BOSS",
                sheet = "Art10/BossSpider",
                stats = "生命 ×3.5   ·   伤害 ×2.2   ·   体型 ×1.55\n吐网定身 1.6s   ·   一次产卵 3~5 颗",
                desc = "八足猎手。保持距离吐蛛丝，命中后玩家会被缠住 1.6 秒；还会产下蛛卵，" +
                       "10~14 秒后孵化出只有普通怪 30% 属性、体型 0.7 的小蜘蛛。产卵间隔随血量从 60 秒缩短到 30 秒。"
            },
        };

        public void Build()
        {
            _canvas = UiFactory.CreateCanvas("CodexCanvas", 35);
            _canvas.transform.SetParent(transform, false);

            var dim = UiFactory.CreateImage(_canvas.transform, "Dim", UiFactory.WhiteSprite, new Color(0f, 0f, 0f, 0.78f));
            UiFactory.SetFullStretch(dim.rectTransform);
            dim.raycastTarget = true;

            _panel = UiFactory.CreateRect(_canvas.transform, "Panel");
            UiFactory.Anchor(_panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1500f, 880f));

            var bg = UiFactory.CreateImage(_panel, "Bg", UiFactory.PanelSprite, Color.white);
            UiFactory.SetFullStretch(bg.rectTransform);
            if (UiFactory.PanelSprite == null) bg.color = new Color(0.08f, 0.09f, 0.1f, 0.98f);
            bg.type = Image.Type.Sliced;
            bg.raycastTarget = true;

            var title = UiFactory.CreateText(_panel, "Title", "感 染 体 图 鉴", 40, new Color(0.92f, 0.88f, 0.74f), TextAnchor.MiddleLeft);
            UiFactory.Anchor(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(48f, -26f), new Vector2(600f, 56f));

            var close = UiFactory.CreateButton(_panel, "Close", "X", 24, () => SetOpen(false));
            UiFactory.Anchor((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -22f), new Vector2(56f, 56f));

            var listRoot = UiFactory.CreateRect(_panel, "List");
            UiFactory.Anchor(listRoot, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(44f, -104f), new Vector2(360f, 640f));

            for (int i = 0; i < Entries.Length; i++)
            {
                int idx = i;
                var btn = UiFactory.CreateButton(listRoot, "Entry" + i, Entries[i].name, 24, () => Select(idx));
                UiFactory.Anchor((RectTransform)btn.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -i * 92f), new Vector2(340f, 76f));
            }

            var frame = UiFactory.CreateImage(_panel, "PortraitFrame", UiFactory.WhiteSprite, new Color(0.169f, 0.184f, 0.224f, 1f));
            UiFactory.Anchor(frame.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(430f, -104f), new Vector2(380f, 380f));

            _portrait = UiFactory.CreateImage(frame.transform, "Portrait", null, Color.white);
            UiFactory.Anchor(_portrait.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(372f, 372f));
            _portrait.preserveAspect = true;

            var prev = UiFactory.CreateButton(_panel, "Prev", "<", 26, () => Cycle(-1));
            UiFactory.Anchor((RectTransform)prev.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(432f, -500f), new Vector2(84f, 56f));

            var next = UiFactory.CreateButton(_panel, "Next", ">", 26, () => Cycle(1));
            UiFactory.Anchor((RectTransform)next.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(724f, -500f), new Vector2(84f, 56f));

            _pageText = UiFactory.CreateText(_panel, "Page", "", 20, new Color(0.72f, 0.76f, 0.7f), TextAnchor.MiddleCenter);
            UiFactory.Anchor(_pageText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(432f, -560f), new Vector2(376f, 30f));

            _title = UiFactory.CreateText(_panel, "Monster", "", 34, new Color(0.95f, 0.86f, 0.62f), TextAnchor.UpperLeft);
            UiFactory.Anchor(_title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(846f, -104f), new Vector2(600f, 44f));

            _stats = UiFactory.CreateText(_panel, "Stats", "", 20, new Color(0.82f, 0.86f, 0.8f), TextAnchor.UpperLeft);
            UiFactory.Anchor(_stats.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(846f, -156f), new Vector2(600f, 90f));

            _desc = UiFactory.CreateText(_panel, "Desc", "", 19, new Color(0.7f, 0.74f, 0.7f), TextAnchor.UpperLeft);
            UiFactory.Anchor(_desc.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(846f, -270f), new Vector2(600f, 320f));
            _desc.horizontalOverflow = HorizontalWrapMode.Wrap;

            var hint = UiFactory.CreateText(_panel, "Hint", "左右按钮翻页    Esc 关闭    数据取自 Items / Drops 配置", 17, new Color(0.55f, 0.58f, 0.55f), TextAnchor.LowerLeft);
            UiFactory.Anchor(hint.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(48f, 22f), new Vector2(900f, 26f));

            Select(0);
            SetOpen(false);
        }

        private void Cycle(int delta)
        {
            int n = Entries.Length;
            int next = ((_selected + delta) % n + n) % n;
            Select(next);
        }

        private void Select(int index)
        {
            _selected = Mathf.Clamp(index, 0, Entries.Length - 1);
            var e = Entries[_selected];

            _frames = null;
            _frameIndex = 0;
            _frameTimer = 0f;

            if (!string.IsNullOrEmpty(e.sheet))
            {
                var loaded = Resources.LoadAll<Sprite>(e.sheet);
                if (loaded != null && loaded.Length > 0)
                {
                    System.Array.Sort(loaded, (a, b) => string.CompareOrdinal(a.name, b.name));
                    _frames = loaded;
                }
            }

            if (_portrait != null)
            {
                _portrait.sprite = _frames != null && _frames.Length > 0 ? _frames[0] : null;
                _portrait.enabled = _portrait.sprite != null;
            }
            if (_title != null) _title.text = e.name;
            if (_stats != null) _stats.text = e.stats;
            if (_desc != null) _desc.text = e.desc;
            if (_pageText != null) _pageText.text = (_selected + 1) + " / " + Entries.Length;
        }

        private void Update()
        {
            if (_frames == null || _frames.Length == 0 || _portrait == null) return;
            if (!IsOpen) return;
            _frameTimer += Time.unscaledDeltaTime;
            if (_frameTimer < 0.083f) return;
            _frameTimer = 0f;
            _frameIndex = (_frameIndex + 1) % _frames.Length;
            _portrait.sprite = _frames[_frameIndex];
        }

        public void SetOpen(bool open)
        {
            if (_canvas != null) _canvas.enabled = open;
            if (open) Select(_selected);
        }

        public bool IsOpen { get { return _canvas != null && _canvas.enabled; } }

        public void Toggle() { SetOpen(!IsOpen); }
    }
}
