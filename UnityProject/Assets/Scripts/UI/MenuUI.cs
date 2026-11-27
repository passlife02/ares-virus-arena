using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Ares
{
    /// <summary>
    /// Main menu (character showcase + wave selection + stress dropdown), pause overlay and
    /// end-of-run screen.
    /// </summary>
    public class MenuUI : MonoBehaviour
    {
        private Canvas _canvas;
        private GameObject _mainGroup;
        private GameObject _pauseGroup;
        private GameObject _resultGroup;
        private Text _resultTitle;
        private Text _resultBody;
        private Text _saveText;

        private Text _waveValueText;
        private GameObject _stressDropdown;
        private RectTransform _portraitZone;
        private float _hoverT;
        private float _hoverTarget;

        private int _selectedWaves = 10;
        private static readonly int[] WaveOptions = new int[] { 1, 5, 10, 20, 30, 0 };

        public void Build()
        {
            _canvas = UiFactory.CreateCanvas("MenuCanvas", 30);
            _canvas.transform.SetParent(transform, false);

            BuildMain();
            BuildPause();
            BuildResult();
            ShowMain();
        }

        // ------------------------------------------------------------------ main

        private void BuildMain()
        {
            var root = UiFactory.CreateRect(_canvas.transform, "MainMenu");
            UiFactory.SetFullStretch(root);
            _mainGroup = root.gameObject;

            var bgSprite = UiFactory.LoadSprite("ui_beijing_ditu_zuoshangjiao");
            var bg = UiFactory.CreateImage(root, "Bg", bgSprite != null ? bgSprite : UiFactory.WhiteSprite,
                bgSprite != null ? new Color(0.5f, 0.53f, 0.58f, 1f) : new Color(0.08f, 0.09f, 0.1f, 1f));
            UiFactory.SetFullStretch(bg.rectTransform);

            var dim = UiFactory.CreateImage(root, "Dim", UiFactory.WhiteSprite, new Color(0.02f, 0.03f, 0.04f, 0.62f));
            UiFactory.SetFullStretch(dim.rectTransform);

            // left side: title + options
            var title = UiFactory.CreateText(root, "Title", "阿瑞斯病毒 · 竞技场", 76, new Color(0.9f, 0.28f, 0.22f), TextAnchor.MiddleLeft);
            UiFactory.Anchor(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(90f, -120f), new Vector2(1100f, 100f));
            var glow = title.gameObject.AddComponent<Shadow>();
            glow.effectColor = new Color(0f, 0f, 0f, 0.9f);
            glow.effectDistance = new Vector2(4f, -4f);

            var sub = UiFactory.CreateText(root, "Sub", "ARES VIRUS ARENA   ·   2D 俯视角生存射击", 24, new Color(0.78f, 0.8f, 0.76f), TextAnchor.MiddleLeft);
            UiFactory.Anchor(sub.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(92f, -214f), new Vector2(900f, 36f));

            // wave selector
            var waveLabel = UiFactory.CreateText(root, "WaveLabel", "本局波次", 24, new Color(0.85f, 0.87f, 0.8f), TextAnchor.MiddleLeft);
            UiFactory.Anchor(waveLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(92f, -288f), new Vector2(220f, 34f));

            _waveValueText = UiFactory.CreateText(root, "WaveValue", "10 波", 22, new Color(1f, 0.86f, 0.5f), TextAnchor.MiddleLeft);
            UiFactory.Anchor(_waveValueText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(300f, -288f), new Vector2(300f, 34f));

            float bx = 0f;
            for (int i = 0; i < WaveOptions.Length; i++)
            {
                int waves = WaveOptions[i];
                string label = waves == 0 ? "无尽" : waves.ToString();
                var btn = UiFactory.CreateButton(root, "Wave" + i, label, 22, () => SelectWaves(waves));
                UiFactory.Anchor((RectTransform)btn.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(92f + bx, -334f), new Vector2(96f, 54f));
                bx += 106f;
            }

            // map picker
            var mapLabel = UiFactory.CreateText(root, "MapLabel", "地  图", 24, new Color(0.85f, 0.87f, 0.8f), TextAnchor.MiddleLeft);
            UiFactory.Anchor(mapLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(92f, -604f), new Vector2(160f, 34f));

            _mapButtons = new Button[MapBuilder.Names.Length];
            float mx = 0f;
            for (int i = 0; i < MapBuilder.Names.Length; i++)
            {
                int index = i;
                var btn = UiFactory.CreateButton(root, "Map" + i, MapBuilder.Names[i], 20, () => SelectMap((MapVariant)index));
                UiFactory.Anchor((RectTransform)btn.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(214f + mx, -644f), new Vector2(158f, 52f));
                _mapButtons[i] = btn;
                mx += 166f;
            }

            // start
            var start = UiFactory.CreateButton(root, "Start", "开 始 游 戏", 32, () => { if (Game.Manager != null) Game.Manager.StartRun(false, 0, _selectedWaves); });
            UiFactory.Anchor((RectTransform)start.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(92f, -516f), new Vector2(420f, 70f));

            // stress dropdown
            var stressBtn = UiFactory.CreateButton(root, "Stress", "压力测试  ▾", 24, ToggleStressDropdown);
            UiFactory.Anchor((RectTransform)stressBtn.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(530f, -516f), new Vector2(260f, 70f));

            var dropdown = UiFactory.CreateImage(root, "StressDropdown", UiFactory.WhiteSprite, new Color(0.1f, 0.11f, 0.12f, 0.98f));
            UiFactory.Anchor(dropdown.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(530f, -486f), new Vector2(260f, 168f));
            dropdown.raycastTarget = true;
            _stressDropdown = dropdown.gameObject;

            for (int i = 0; i < 3; i++)
            {
                int count = Game.Cfg.stressCounts[Mathf.Min(i, Game.Cfg.stressCounts.Length - 1)];
                var b = UiFactory.CreateButton(dropdown.transform, "Stress" + count, count + " 敌人", 21,
                    () => { if (Game.Manager != null) Game.Manager.StartRun(true, count, 0); });
                UiFactory.Anchor((RectTransform)b.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(8f, -8f - i * 54f), new Vector2(244f, 48f));
            }
            _stressDropdown.SetActive(false);

            var codexBtn = UiFactory.CreateButton(root, "Codex", "怪物图鉴", 24, () => { if (Game.Manager != null && Game.Manager.Codex != null) Game.Manager.Codex.SetOpen(true); });
            UiFactory.Anchor((RectTransform)codexBtn.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(806f, -412f), new Vector2(180f, 70f));

            var loreBtn = UiFactory.CreateButton(root, "Lore", "世 界 观", 24, () => { if (Game.Manager != null && Game.Manager.Lore != null) Game.Manager.Lore.SetOpen(true); });
            UiFactory.Anchor((RectTransform)loreBtn.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(806f, -232f), new Vector2(180f, 54f));

            var shelterBtn = UiFactory.CreateButton(root, "Shelter", "庇  护  所", 26, () => { if (Game.Manager != null && Game.Manager.Shelter != null) Game.Manager.Shelter.SetOpen(true); });
            UiFactory.Anchor((RectTransform)shelterBtn.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(806f, -334f), new Vector2(180f, 54f));

            var quit = UiFactory.CreateButton(root, "Quit", "退出游戏", 24, () => { if (Game.Manager != null) Game.Manager.QuitGame(); });
            UiFactory.Anchor((RectTransform)quit.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(92f, 60f), new Vector2(240f, 58f));

            // right side: the character is rendered by the world camera; this invisible zone
            // just reacts to the mouse so hovering brightens the portrait (feedback #6).
            _portraitZone = UiFactory.CreateRect(root, "PortraitZone");
            UiFactory.Anchor(_portraitZone, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-40f, 0f), new Vector2(560f, 720f));
            var zoneImg = _portraitZone.gameObject.AddComponent<Image>();
            zoneImg.color = new Color(0f, 0f, 0f, 0.001f);
            zoneImg.raycastTarget = true;

            var portraitHint = UiFactory.CreateText(root, "PortraitHint", "鼠标移到立绘上可查看", 18, new Color(0.72f, 0.74f, 0.7f, 0.75f), TextAnchor.LowerRight);
            UiFactory.Anchor(portraitHint.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-60f, 40f), new Vector2(600f, 26f));

            var help = UiFactory.CreateText(root, "Help",
                "WASD 移动 · 鼠标瞄准 · 左键攻击 · 空格翻滚 · Q/右键 换武器 · F 拾取 · Tab 背包与制作 · Esc 暂停\n" +
                "每 10 波会出现一个 BOSS，击杀掉落更好；波次越高敌人越多越强，但难度会在第 30 波后封顶",
                19, new Color(0.72f, 0.74f, 0.7f), TextAnchor.LowerLeft);
            UiFactory.Anchor(help.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(92f, 20f), new Vector2(1400f, 60f));

            _saveText = UiFactory.CreateText(root, "Save", "", 15, new Color(0.55f, 0.6f, 0.55f), TextAnchor.LowerRight);
            UiFactory.Anchor(_saveText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-30f, -16f), new Vector2(900f, 24f));
        }

        private Button[] _mapButtons;
        private MapVariant _selectedMap = MapVariant.Ruins;

        private float _lastMapSwitch;

        private void SelectMap(MapVariant v)
        {
            // rebuilding walls + nav bake is heavy; ignore rapid-fire clicks
            if (Time.unscaledTime - _lastMapSwitch < 0.35f) return;
            _lastMapSwitch = Time.unscaledTime;

            _selectedMap = v;
            if (Game.Manager != null)
            {
                Game.Manager.SelectMap(v);
                Game.Manager.MapPicked = true;
            }
            RefreshMapButtons();
            AudioManager.PlaySfx("sfx_click", 0.5f, 0.1f);
        }

        private void RefreshMapButtons()
        {
            if (_mapButtons == null) return;
            for (int i = 0; i < _mapButtons.Length; i++)
            {
                if (_mapButtons[i] == null) continue;
                var img = _mapButtons[i].targetGraphic as UnityEngine.UI.Image;
                if (img != null)
                    img.color = (int)_selectedMap == i
                        ? new Color(0.55f, 0.47f, 0.26f, 1f)
                        : Color.white;
            }
        }

        private void SelectWaves(int waves)
        {
            _selectedWaves = waves;
            if (_waveValueText != null)
                _waveValueText.text = waves == 0 ? "无尽" : (waves + " 波");
        }

        private void ToggleStressDropdown()
        {
            if (_stressDropdown == null) return;
            _stressDropdown.SetActive(!_stressDropdown.activeSelf);
        }

        private void Update()
        {
            // portrait hover brightening
            var player = Game.Player;
            if (player == null) return;
            bool inMenu = Game.Manager != null && Game.Manager.Phase == GamePhase.MainMenu;
            bool hover = false;
            if (inMenu && _portraitZone != null && _mainGroup != null && _mainGroup.activeSelf)
            {
                hover = RectTransformUtility.RectangleContainsScreenPoint(_portraitZone, Input.mousePosition, null);
            }
            _hoverTarget = hover ? 1f : 0f;
            _hoverT = Mathf.MoveTowards(_hoverT, _hoverTarget, Time.unscaledDeltaTime * 4f);
            player.PortraitBrightness = inMenu ? Mathf.Lerp(0.76f, 1.38f, _hoverT) : 1f;
        }

        // ------------------------------------------------------------------ pause

        private void BuildPause()
        {
            var root = UiFactory.CreateRect(_canvas.transform, "PauseMenu");
            UiFactory.SetFullStretch(root);
            _pauseGroup = root.gameObject;

            var dim = UiFactory.CreateImage(root, "Dim", UiFactory.WhiteSprite, new Color(0.02f, 0.03f, 0.04f, 0.74f));
            UiFactory.SetFullStretch(dim.rectTransform);
            dim.raycastTarget = true;

            var title = UiFactory.CreateText(root, "Title", "暂  停", 62, new Color(0.92f, 0.9f, 0.82f), TextAnchor.MiddleCenter);
            UiFactory.Anchor(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 226f), new Vector2(800f, 90f));

            float y = 120f;
            PauseButton(root, "继续游戏", ref y, () => { if (Game.Manager != null) Game.Manager.Resume(); });
            PauseButton(root, "重新开始", ref y, () => { if (Game.Manager != null) Game.Manager.RestartRun(); });
            PauseButton(root, "返回主菜单", ref y, () => { if (Game.Manager != null) Game.Manager.ReturnToMenu(); });
            PauseButton(root, "退出游戏", ref y, () => { if (Game.Manager != null) Game.Manager.QuitGame(); });

            var tip = UiFactory.CreateText(root, "Tip", "按 Esc 继续   鼠标已解锁", 19, new Color(0.7f, 0.72f, 0.68f), TextAnchor.MiddleCenter);
            UiFactory.Anchor(tip.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(700f, 30f));
        }

        private void PauseButton(RectTransform parent, string label, ref float y, Action onClick)
        {
            var btn = UiFactory.CreateButton(parent, "Btn_" + label, label, 30, onClick);
            UiFactory.Anchor((RectTransform)btn.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(420f, 60f));
            y -= 74f;
        }

        // ------------------------------------------------------------------ result

        private void BuildResult()
        {
            var root = UiFactory.CreateRect(_canvas.transform, "ResultMenu");
            UiFactory.SetFullStretch(root);
            _resultGroup = root.gameObject;

            var dim = UiFactory.CreateImage(root, "Dim", UiFactory.WhiteSprite, new Color(0.02f, 0.02f, 0.03f, 0.84f));
            UiFactory.SetFullStretch(dim.rectTransform);
            dim.raycastTarget = true;

            _resultTitle = UiFactory.CreateText(root, "Title", "胜  利", 88, new Color(0.85f, 0.85f, 0.4f), TextAnchor.MiddleCenter);
            UiFactory.Anchor(_resultTitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 240f), new Vector2(1200f, 120f));

            _resultBody = UiFactory.CreateText(root, "Body", "", 28, new Color(0.85f, 0.87f, 0.82f), TextAnchor.MiddleCenter);
            UiFactory.Anchor(_resultBody.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 116f), new Vector2(1200f, 140f));

            float y = 0f;
            PauseButton(root, "重新开始", ref y, () => { if (Game.Manager != null) Game.Manager.RestartRun(); });
            PauseButton(root, "返回主菜单", ref y, () => { if (Game.Manager != null) Game.Manager.ReturnToMenu(); });
        }

        // ------------------------------------------------------------------ state

        public void ShowMain()
        {
            _mainGroup.SetActive(true);
            _pauseGroup.SetActive(false);
            _resultGroup.SetActive(false);
            if (_stressDropdown != null) _stressDropdown.SetActive(false);
            SelectWaves(_selectedWaves);
            if (_saveText != null && Game.Save != null)
            {
                int items = Game.Inventory != null ? Game.Inventory.TotalItemCount : 0;
                int slots = Game.Inventory != null ? Game.Inventory.UsedSlots : 0;
                _saveText.text = "背包已保存  " + slots + "/30 格 · " + items + " 件物品\n"
                    + (Game.Save.SqliteActive ? "SQLite：" : "JSON：") + Game.Save.DatabasePath;
            }
        }

        public void ShowNone()
        {
            _mainGroup.SetActive(false);
            _pauseGroup.SetActive(false);
            _resultGroup.SetActive(false);
        }

        public void ShowPause()
        {
            _mainGroup.SetActive(false);
            _pauseGroup.SetActive(true);
            _resultGroup.SetActive(false);
        }

        public void ShowResult(bool victory, int kills, int wave, int items)
        {
            _mainGroup.SetActive(false);
            _pauseGroup.SetActive(false);
            _resultGroup.SetActive(true);
            if (_resultTitle != null)
            {
                _resultTitle.text = victory ? "胜  利" : "被  感  染";
                _resultTitle.color = victory ? new Color(0.85f, 0.85f, 0.4f) : new Color(0.85f, 0.25f, 0.2f);
            }
            if (_resultBody != null)
                _resultBody.text = "击杀 " + kills + "     抵达波次 " + wave + "\n背包物品 " + items + " 件（已保存）";
        }
    }
}
