using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Ares
{
    /// <summary>
    /// In-run HUD: two-layer health bar, smoothly filling dodge meter, weapon/ammo readout,
    /// wave countdown, boss bar, pickup prompt, crit damage numbers and the scope reticle.
    /// </summary>
    public class HudUI : MonoBehaviour
    {
        private Canvas _canvas;
        private RectTransform _rootRect;

        private Image _hpRed;
        private Image _hpGreen;
        private Text _hpText;
        private Image _dodgeFill;
        private Text _dodgeText;
        private float _dodgeDisplay = 1f;

        private Text _killText;
        private Text _waveText;
        private Text _countdownText;
        private Text _promptText;
        private Text _bannerText;
        private Text _statsText;
        private Text _hintText;

        private Image _weaponIcon;
        private Text _weaponName;
        private Text _weaponInfo;
        private Image _durabilityFill;
        private Image _durabilityBg;

        private Image _bossBarBg;
        private Image _bossBarFill;
        private Text _bossLabel;

        private Image _crosshair;
        private Image _crosshairGlow;
        private Image _vignette;

        private float _bannerTimer;
        private float _vignetteTimer;
        private float _bloodAlpha;
        private bool _bloodOn;
        private float _promptTimer;
        private float _crosshairPulse;
        private int _frameCount;
        private float _fpsTimer;
        private float _fps;

        private class FloatItem
        {
            public Text text;
            public Vector3 world;
            public Vector2 vel;
            public float life;
            public float maxLife;
            public Color color;
            public float scale;
        }

        private readonly List<FloatItem> _floats = new List<FloatItem>(48);
        private Transform _floatRoot;

        public float Fps { get { return _fps; } }

        public void Build()
        {
            UiFactory.EnsureEventSystem();
            _canvas = UiFactory.CreateCanvas("HUDCanvas", 10);
            _canvas.transform.SetParent(transform, false);

            // post-apocalyptic grade: cold tint + vignette, drawn under the HUD widgets
            {
                var gradeSprite = Resources.Load<Sprite>("Weather/grade_vignette");
                if (gradeSprite != null)
                {
                    var grade = UiFactory.CreateImage(_canvas.transform, "GradeVignette", gradeSprite, Color.white);
                    UiFactory.SetFullStretch(grade.rectTransform);
                    grade.raycastTarget = false;
                    grade.preserveAspect = false;
                }

                var tint = UiFactory.CreateImage(_canvas.transform, "ColdTint", UiFactory.WhiteSprite,
                    new Color(0.46f, 0.56f, 0.70f, 0.07f));
                UiFactory.SetFullStretch(tint.rectTransform);
                tint.raycastTarget = false;
            }

            var bars = gameObject.AddComponent<EnemyBars>();
            bars.Build();
            Game.EnemyBars = bars;
            _rootRect = (RectTransform)_canvas.transform;

            // ---------------- health: red backing + green fill ----------------
            var hpBg = UiFactory.CreateImage(_canvas.transform, "HpBg", UiFactory.WhiteSprite, new Color(0.03f, 0.03f, 0.04f, 0.85f));
            UiFactory.Anchor(hpBg.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -26f), new Vector2(470f, 38f));

            _hpRed = UiFactory.CreateImage(hpBg.transform, "HpRed", UiFactory.WhiteSprite, new Color(0.72f, 0.12f, 0.11f, 1f));
            var rr = _hpRed.rectTransform;
            rr.anchorMin = Vector2.zero; rr.anchorMax = Vector2.one;
            rr.offsetMin = new Vector2(3f, 3f); rr.offsetMax = new Vector2(-3f, -3f);
            _hpRed.type = Image.Type.Filled;
            _hpRed.fillMethod = Image.FillMethod.Horizontal;

            _hpGreen = UiFactory.CreateImage(hpBg.transform, "HpGreen", UiFactory.WhiteSprite, new Color(0.4f, 0.78f, 0.34f, 1f));
            var gr = _hpGreen.rectTransform;
            gr.anchorMin = Vector2.zero; gr.anchorMax = Vector2.one;
            gr.offsetMin = new Vector2(3f, 3f); gr.offsetMax = new Vector2(-3f, -3f);
            _hpGreen.type = Image.Type.Filled;
            _hpGreen.fillMethod = Image.FillMethod.Horizontal;

            _hpText = UiFactory.CreateText(hpBg.transform, "HpText", "120 / 120", 21, Color.white, TextAnchor.MiddleCenter);
            UiFactory.SetFullStretch(_hpText.rectTransform);
            var hsh = _hpText.gameObject.AddComponent<Shadow>();
            hsh.effectColor = new Color(0f, 0f, 0f, 0.85f);
            hsh.effectDistance = new Vector2(1.5f, -1.5f);

            // ---------------- dodge ----------------
            var dodgeBg = UiFactory.CreateImage(_canvas.transform, "DodgeBg", UiFactory.WhiteSprite, new Color(0.03f, 0.03f, 0.04f, 0.8f));
            UiFactory.Anchor(dodgeBg.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -70f), new Vector2(470f, 14f));

            _dodgeFill = UiFactory.CreateImage(dodgeBg.transform, "DodgeFill", UiFactory.WhiteSprite, new Color(0.36f, 0.66f, 1f, 1f));
            var df = _dodgeFill.rectTransform;
            df.anchorMin = Vector2.zero; df.anchorMax = Vector2.one;
            df.offsetMin = new Vector2(2f, 2f); df.offsetMax = new Vector2(-2f, -2f);
            _dodgeFill.type = Image.Type.Filled;
            _dodgeFill.fillMethod = Image.FillMethod.Horizontal;

            _dodgeText = UiFactory.CreateText(_canvas.transform, "DodgeText", "闪避", 16, new Color(0.78f, 0.86f, 0.95f), TextAnchor.MiddleLeft);
            UiFactory.Anchor(_dodgeText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(510f, -66f), new Vector2(200f, 22f));

            // ---------------- weapon panel (bottom right) ----------------
            var wpanel = UiFactory.CreateImage(_canvas.transform, "WeaponPanel", UiFactory.WhiteSprite, new Color(0.04f, 0.05f, 0.05f, 0.72f));
            UiFactory.Anchor(wpanel.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-30f, 96f), new Vector2(360f, 92f));

            _weaponIcon = UiFactory.CreateImage(wpanel.transform, "Icon", null, Color.white);
            UiFactory.Anchor(_weaponIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(10f, 0f), new Vector2(72f, 72f));
            _weaponIcon.preserveAspect = true;

            _weaponName = UiFactory.CreateText(wpanel.transform, "Name", "—", 22, new Color(0.95f, 0.92f, 0.8f), TextAnchor.MiddleLeft);
            UiFactory.Anchor(_weaponName.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(90f, -12f), new Vector2(250f, 28f));

            _weaponInfo = UiFactory.CreateText(wpanel.transform, "Info", "", 18, new Color(0.8f, 0.84f, 0.78f), TextAnchor.MiddleLeft);
            UiFactory.Anchor(_weaponInfo.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(90f, -42f), new Vector2(250f, 24f));

            _durabilityBg = UiFactory.CreateImage(wpanel.transform, "DurBg", UiFactory.WhiteSprite, new Color(0.02f, 0.02f, 0.03f, 0.9f));
            UiFactory.Anchor(_durabilityBg.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(90f, 12f), new Vector2(250f, 10f));

            _durabilityFill = UiFactory.CreateImage(_durabilityBg.transform, "DurFill", UiFactory.WhiteSprite, new Color(0.9f, 0.75f, 0.3f, 1f));
            var dfr = _durabilityFill.rectTransform;
            dfr.anchorMin = Vector2.zero; dfr.anchorMax = Vector2.one;
            dfr.offsetMin = new Vector2(1f, 1f); dfr.offsetMax = new Vector2(-1f, -1f);
            _durabilityFill.type = Image.Type.Filled;
            _durabilityFill.fillMethod = Image.FillMethod.Horizontal;

            // ---------------- top centre: wave + countdown ----------------
            _waveText = UiFactory.CreateText(_canvas.transform, "WaveText", "", 26, new Color(0.85f, 0.9f, 0.82f), TextAnchor.UpperCenter);
            UiFactory.Anchor(_waveText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(700f, 34f));

            _countdownText = UiFactory.CreateText(_canvas.transform, "CountdownText", "", 22, new Color(1f, 0.78f, 0.4f), TextAnchor.UpperCenter);
            UiFactory.Anchor(_countdownText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(700f, 30f));

            // ---------------- boss bar ----------------
            _bossBarBg = UiFactory.CreateImage(_canvas.transform, "BossBg", UiFactory.WhiteSprite, new Color(0.03f, 0.02f, 0.02f, 0.85f));
            UiFactory.Anchor(_bossBarBg.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -104f), new Vector2(760f, 26f));

            _bossBarFill = UiFactory.CreateImage(_bossBarBg.transform, "BossFill", UiFactory.WhiteSprite, new Color(0.85f, 0.16f, 0.12f, 1f));
            var bf = _bossBarFill.rectTransform;
            bf.anchorMin = Vector2.zero; bf.anchorMax = Vector2.one;
            bf.offsetMin = new Vector2(3f, 3f); bf.offsetMax = new Vector2(-3f, -3f);
            _bossBarFill.type = Image.Type.Filled;
            _bossBarFill.fillMethod = Image.FillMethod.Horizontal;

            _bossLabel = UiFactory.CreateText(_bossBarBg.transform, "BossLabel", "BOSS", 16, new Color(1f, 0.9f, 0.8f), TextAnchor.MiddleCenter);
            UiFactory.SetFullStretch(_bossLabel.rectTransform);

            _bossBarBg.gameObject.SetActive(false);

            // ---------------- misc ----------------
            _killText = UiFactory.CreateText(_canvas.transform, "KillText", "击杀 0", 26, new Color(0.95f, 0.9f, 0.75f), TextAnchor.UpperRight);
            UiFactory.Anchor(_killText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-30f, -300f), new Vector2(320f, 34f));

            _statsText = UiFactory.CreateText(_canvas.transform, "StatsText", "", 17, new Color(0.68f, 0.92f, 0.72f), TextAnchor.LowerLeft);
            UiFactory.Anchor(_statsText.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(30f, 22f), new Vector2(760f, 110f));

            _hintText = UiFactory.CreateText(_canvas.transform, "HintText",
                "WASD 移动  左键攻击  空格翻滚  右键换武器  Q 手榴弹  E 木栅栏  F 拾取  Tab 背包  Esc 暂停", 17,
                new Color(0.8f, 0.82f, 0.78f, 0.85f), TextAnchor.LowerCenter);
            UiFactory.Anchor(_hintText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(1400f, 26f));

            _promptText = UiFactory.CreateText(_canvas.transform, "PromptText", "", 22, new Color(1f, 0.95f, 0.7f), TextAnchor.LowerCenter);
            UiFactory.Anchor(_promptText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 128f), new Vector2(900f, 34f));

            _bannerText = UiFactory.CreateText(_canvas.transform, "BannerText", "", 54, new Color(1f, 0.85f, 0.55f), TextAnchor.MiddleCenter);
            UiFactory.Anchor(_bannerText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 220f), new Vector2(1200f, 80f));
            _bannerText.color = new Color(1f, 0.85f, 0.55f, 0f);

            // ---------------- reticle ----------------
            _crosshairGlow = UiFactory.CreateImage(_canvas.transform, "CrosshairGlow", UiFactory.RingSprite, new Color(1f, 1f, 1f, 0.16f));
            UiFactory.Anchor(_crosshairGlow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(74f, 74f));

            _crosshair = UiFactory.CreateImage(_canvas.transform, "Crosshair", UiFactory.ScopeSprite, new Color(0.05f, 0.05f, 0.06f, 0.95f));
            UiFactory.Anchor(_crosshair.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(96f, 96f));

            _vignette = UiFactory.CreateImage(_canvas.transform, "Vignette", UiFactory.WhiteSprite, new Color(0.7f, 0f, 0f, 0f));
            UiFactory.SetFullStretch(_vignette.rectTransform);


            var fr = UiFactory.CreateRect(_canvas.transform, "FloatingText");
            UiFactory.SetFullStretch(fr);
            _floatRoot = fr;

            for (int i = 0; i < 40; i++)
            {
                var t = UiFactory.CreateText(_floatRoot, "Float" + i, "", 24, Color.white, TextAnchor.MiddleCenter);
                UiFactory.Anchor(t.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(160f, 34f));
                var sh = t.gameObject.AddComponent<Shadow>();
                sh.effectColor = new Color(0f, 0f, 0f, 0.9f);
                sh.effectDistance = new Vector2(1.5f, -1.5f);
                t.gameObject.SetActive(false);
                _floats.Add(new FloatItem { text = t });
            }
        }

        public void SetVisible(bool visible)
        {
            if (_canvas != null) _canvas.enabled = visible;
        }

        public bool IsVisible { get { return _canvas != null && _canvas.enabled; } }

        private void Update()
        {
            float udt = Time.unscaledDeltaTime;
            _frameCount++;
            _fpsTimer += udt;
            if (_fpsTimer >= 0.4f)
            {
                _fps = _frameCount / _fpsTimer;
                _frameCount = 0;
                _fpsTimer = 0f;
            }

            var player = Game.Player;
            var manager = Game.Manager;

            if (player != null && manager != null && manager.Phase != GamePhase.MainMenu)
            {
                float hp01 = player.MaxHp > 0 ? (float)player.Hp / player.MaxHp : 0f;
                if (_hpGreen != null) _hpGreen.fillAmount = hp01;
                if (_hpRed != null) _hpRed.fillAmount = 1f;
                if (_hpText != null) _hpText.text = player.Hp + " / " + player.MaxHp;

                _dodgeDisplay = Mathf.MoveTowards(_dodgeDisplay, player.RollCooldown01, udt * 1.6f);
                if (_dodgeFill != null)
                {
                    _dodgeFill.fillAmount = _dodgeDisplay;
                    _dodgeFill.color = _dodgeDisplay >= 0.999f ? new Color(0.4f, 0.78f, 1f) : new Color(0.28f, 0.36f, 0.46f);
                }
                if (_dodgeText != null) _dodgeText.text = _dodgeDisplay >= 0.999f ? "闪避 就绪" : "闪避 " + Mathf.RoundToInt(_dodgeDisplay * 100f) + "%";
            }

            UpdateWeaponPanel();
            UpdateWaveInfo();
            UpdateBossBar();
            UpdateCrosshair();
            UpdateBanner();
            UpdateVignette();
            UpdatePrompt();
            UpdateFloats();

            if (_statsText != null)
            {
                if (Game.Spawner != null && manager != null && manager.Phase == GamePhase.Playing)
                {
                    _statsText.text = string.Format("FPS {0:0}   敌人 {1}   ⁠子弹 {2}/{3}   累计 {4}",
                        _fps, Game.Spawner.LiveCount,
                        Game.Combat != null ? Game.Combat.ActivePlayerBullets : 0,
                        Game.Combat != null ? Game.Combat.ActiveEnemyBullets : 0,
                        Game.Spawner.TotalSpawned);
                }
                else _statsText.text = "";
            }
        }

        private void UpdateWeaponPanel()
        {
            if (_weaponName == null) return;
            var eq = Game.Equipment;
            var def = eq != null ? eq.ActiveDef : null;

            if (def == null)
            {
                _weaponName.text = "赤手空拳";
                _weaponInfo.text = "Tab 打开背包制作武器";
                if (_weaponIcon != null) _weaponIcon.enabled = false;
                if (_durabilityBg != null) _durabilityBg.gameObject.SetActive(false);
                return;
            }

            if (_weaponIcon != null)
            {
                _weaponIcon.enabled = true;
                _weaponIcon.sprite = Game.Items != null ? Game.Items.GetIcon(def.icon) : null;
            }
            _weaponName.text = def.name;

            if (def.IsRanged)
            {
                int ammo = 0;
                var ammoDef = Game.Items != null ? Game.Items.Get(def.ammoItemId) : null;
                if (Game.Inventory != null && !string.IsNullOrEmpty(def.ammoItemId))
                    ammo = Game.Inventory.CountOf(def.ammoItemId);
                _weaponInfo.text = "伤害 " + def.damage + "  ·  间隔 " + def.attackInterval.ToString("0.00") + "s  ·  弹药 " + ammo;
                if (_durabilityBg != null) _durabilityBg.gameObject.SetActive(false);
            }
            else
            {
                var slot = eq.ActiveSlot;
                int dur = slot != null ? slot.durability : 0;
                int max = Mathf.Max(1, def.durabilityMax);
                _weaponInfo.text = "伤害 " + def.damage + "  ·  间隔 " + def.attackInterval.ToString("0.00") + "s  ·  耐久 " + dur + "/" + max;
                if (_durabilityBg != null)
                {
                    _durabilityBg.gameObject.SetActive(max > 1);
                    _durabilityFill.fillAmount = Mathf.Clamp01((float)dur / max);
                    _durabilityFill.color = dur <= max * 0.25f ? new Color(0.9f, 0.35f, 0.25f) : new Color(0.9f, 0.75f, 0.3f);
                }
            }
        }

        private void UpdateWaveInfo()
        {
            var sp = Game.Spawner;
            var manager = Game.Manager;
            if (_waveText == null || sp == null || manager == null) return;

            if (manager.Phase != GamePhase.Playing) { _waveText.text = ""; if (_countdownText != null) _countdownText.text = ""; return; }

            if (sp.StressMode)
                _waveText.text = "压力测试 · " + sp.LiveCount + " 敌人";
            else if (sp.TotalWaves > 0)
                _waveText.text = "第 " + Mathf.Max(1, sp.Wave) + " / " + sp.TotalWaves + " 波";
            else
                _waveText.text = "第 " + Mathf.Max(1, sp.Wave) + " 波 · 无尽";

            if (_countdownText != null)
            {
                if (sp.AwaitingClear) _countdownText.text = "清空场上敌人即可通关";
                else if (sp.StressMode) _countdownText.text = "";
                else if (!sp.HasNextWave) _countdownText.text = "<color=#ff6a55>最后一波 · 清空即可通关</color>";
                else
                {
                    float t = sp.NextWaveIn;
                    _countdownText.text = t > 0.15f ? ("下一波 " + t.ToString("0.0") + " s") : "";
                }
            }

            if (_killText != null) _killText.text = "击杀 " + manager.Kills;
        }

        private void UpdateBossBar()
        {
            var sp = Game.Spawner;
            if (_bossBarBg == null) return;
            if (sp == null || !sp.BossAlive || Game.Manager == null || Game.Manager.Phase != GamePhase.Playing)
            {
                if (_bossBarBg.gameObject.activeSelf) _bossBarBg.gameObject.SetActive(false);
                return;
            }
            if (!_bossBarBg.gameObject.activeSelf) _bossBarBg.gameObject.SetActive(true);

            EnemyBase boss = null;
            var alive = sp.Alive;
            for (int i = 0; i < alive.Count; i++)
            {
                var e = alive[i];
                if (e != null && e.IsAlive && e.EnemyKind == EnemyKind.Boss) { boss = e; break; }
            }
            if (boss != null)
            {
                _bossBarFill.fillAmount = boss.Hp01;
                _bossLabel.text = "BOSS  " + Mathf.CeilToInt(boss.Hp01 * boss.MaxHp) + " / " + boss.MaxHp;
            }
        }

        private void UpdateCrosshair()
        {
            if (_crosshair == null) return;
            bool show = Game.Manager != null && Game.Manager.Phase == GamePhase.Playing
                        && !(Game.Manager.Inventory != null && Game.Manager.Inventory.IsOpen);
            if (!show)
            {
                if (_crosshair.enabled)
                {
                    _crosshair.enabled = false;
                    if (_crosshairGlow != null) _crosshairGlow.enabled = false;
                }
                return;
            }
            if (!_crosshair.enabled)
            {
                _crosshair.enabled = true;
                if (_crosshairGlow != null) _crosshairGlow.enabled = true;
            }

            Vector2 local;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_rootRect, Input.mousePosition, null, out local))
            {
                _crosshair.rectTransform.anchoredPosition = local;
                if (_crosshairGlow != null) _crosshairGlow.rectTransform.anchoredPosition = local;
            }

            bool onTarget = false;
            var cam = Game.Camera != null ? Game.Camera.Cam : null;
            if (cam != null && Game.Spawner != null)
            {
                Vector3 wp = cam.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, -cam.transform.position.z));
                onTarget = Game.Spawner.FindEnemyUnderCursor(wp, 0.95f) != null;
            }

            _crosshairPulse = Mathf.Max(0f, _crosshairPulse - Time.unscaledDeltaTime * 6f);

            // Melee has no "scope", so the reticle switches to a light ring that stays readable
            // on the dark floor (a black reticle read as "missing" for melee).
            var eq = Game.Equipment;
            bool melee = eq != null && eq.ActiveDef != null && eq.ActiveDef.IsMelee;

            // melee has no scope at all (feedback #13 item 3)
            if (_crosshair != null && _crosshair.enabled == melee) _crosshair.enabled = !melee;
            if (melee) return;

            Color target;
            target = onTarget ? new Color(1f, 0.16f, 0.13f, 1f) : new Color(0.06f, 0.06f, 0.07f, 0.95f);
            _crosshair.color = Color.Lerp(_crosshair.color, target, 18f * Time.unscaledDeltaTime);

            float s = (melee ? 74f : 96f) + _crosshairPulse * 14f;
            _crosshair.rectTransform.sizeDelta = new Vector2(s, s);
            if (_crosshairGlow != null)
            {
                _crosshairGlow.color = new Color(1f, onTarget ? 0.5f : 1f, onTarget ? 0.45f : 1f, onTarget ? 0.35f : 0.16f);
                _crosshairGlow.rectTransform.sizeDelta = new Vector2(74f + _crosshairPulse * 10f, 74f + _crosshairPulse * 10f);
            }
            _crosshair.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.unscaledTime * 1.7f) * 4f);
        }

        private void UpdateBanner()
        {
            if (_bannerText == null) return;
            if (_bannerTimer > 0f)
            {
                _bannerTimer -= Time.unscaledDeltaTime;
                _bannerText.color = new Color(1f, 0.85f, 0.55f, Mathf.Clamp01(_bannerTimer / 0.7f));
            }
        }

        private void UpdateVignette()
        {
            if (_vignette == null) return;
            float target = 0f;
            if (_vignetteTimer > 0f)
            {
                _vignetteTimer -= Time.unscaledDeltaTime;
                target = Mathf.Clamp01(_vignetteTimer / 0.4f) * 0.32f;
            }
            var p = Game.Player;
            if (p != null && p.HurtFlash01 > 0.01f) target = Mathf.Max(target, p.HurtFlash01 * 0.26f);

            if (_bossFlash > 0f)
            {
                _bossFlash -= Time.unscaledDeltaTime;
                float pulse = Mathf.Abs(Mathf.Sin(Time.unscaledTime * 5f));
                target = Mathf.Max(target, pulse * 0.22f * Mathf.Clamp01(_bossFlash));
                if (_vignette != null) { _vignette.color = new Color(0.75f, 0.05f, 0.03f, target); return; }
            }
            _vignette.color = new Color(0.7f, 0f, 0f, target);
        }

        private void UpdatePrompt()
        {
            if (_promptText == null) return;
            if (_promptTimer > 0f)
            {
                _promptTimer -= Time.unscaledDeltaTime;
                if (_promptTimer <= 0f) _promptText.text = "";
                return;
            }
            if (Game.Manager == null || Game.Manager.Phase != GamePhase.Playing || Game.Player == null || Game.Items == null)
            {
                if (_promptText.text.Length > 0) _promptText.text = "";
                return;
            }

            var near = Game.Items.NearestPickupAny(Game.Player.Position, Game.Cfg.pickupRadius);
            if (near == null)
            {
                if (_promptText.text.Length > 0) _promptText.text = "";
                return;
            }
            bool room = Game.Inventory != null && Game.Inventory.CanAdd(near.ItemId, near.Count);
            if (room)
            {
                _promptText.color = new Color(1f, 0.95f, 0.7f);
                _promptText.text = "[F] 拾取  " + near.Def.name + (near.Count > 1 ? " x" + near.Count : "");
            }
            else
            {
                _promptText.color = new Color(1f, 0.45f, 0.4f);
                _promptText.text = "背包已满，无法拾取 " + near.Def.name;
            }
        }

        private void UpdateFloats()
        {
            var cam = Game.Camera != null ? Game.Camera.Cam : null;
            if (cam == null) return;
            for (int i = 0; i < _floats.Count; i++)
            {
                var f = _floats[i];
                if (f.life <= 0f) continue;
                f.life -= Time.deltaTime;
                f.world += (Vector3)(f.vel * Time.deltaTime);
                f.vel += new Vector2(0f, -3.4f) * Time.deltaTime;

                Vector3 sp = cam.WorldToScreenPoint(f.world);
                Vector2 local;
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_rootRect, sp, null, out local))
                    f.text.rectTransform.anchoredPosition = local;

                float t = 1f - Mathf.Clamp01(f.life / Mathf.Max(0.01f, f.maxLife));
                float pop = t < 0.15f ? Mathf.Lerp(1.55f, 1f, t / 0.15f) : 1f;
                f.text.rectTransform.localScale = Vector3.one * (f.scale * pop);
                Color c = f.color;
                c.a = Mathf.Clamp01(f.life / 0.35f);
                f.text.color = c;
                if (f.life <= 0f) f.text.gameObject.SetActive(false);
            }
        }

        // ---------------------------------------------------------------- public API

        public void ShowBanner(string text)
        {
            if (_bannerText == null) return;
            _bannerText.text = text;
            _bannerTimer = 1.7f;
        }

        public void FlashDamage() { _vignetteTimer = 0.4f; }


        private float _bossFlash;
        /// <summary>Red pulse when a boss wave starts, for extra tension (feedback #6).</summary>
        public void FlashBossWarning() { _bossFlash = 1.6f; }

        public void PulseCrosshair() { _crosshairPulse = 1f; }

        public void ShowPrompt(string text, float duration)
        {
            if (_promptText == null) return;
            _promptText.text = text;
            _promptTimer = duration;
        }

        public const int DmgNormal = 0;
        public const int DmgCrit = 1;
        public const int DmgExplosion = 2;

        public void ShowDamage(Vector2 worldPos, int amount, bool crit)
        {
            ShowDamageStyled(worldPos, amount, crit ? DmgCrit : DmgNormal);
        }

        /// <summary>White for normal hits, red with a lightning mark for crits, orange burst for explosions.</summary>
        public void ShowDamageStyled(Vector2 worldPos, int amount, int style)
        {
            bool crit = style == DmgCrit;
            bool boom = style == DmgExplosion;
            if (boom) crit = false;

            FloatItem slot = null;
            for (int i = 0; i < _floats.Count; i++)
                if (_floats[i].life <= 0f) { slot = _floats[i]; break; }
            if (slot == null) return;

            slot.world = new Vector3(worldPos.x + Random.Range(-0.16f, 0.16f), worldPos.y, 0f);
            slot.vel = new Vector2(Random.Range(-0.5f, 0.5f), 2.6f);
            if (crit)
            {
                slot.maxLife = 1.0f;
                slot.color = new Color(1f, 0.22f, 0.18f);
                slot.scale = 1.55f;
                slot.text.text = "<b>" + amount + "</b> <color=#ffd24a>\u26a1</color>";
                slot.text.fontSize = 36;
            }
            else if (boom)
            {
                slot.maxLife = 0.95f;
                slot.color = new Color(1f, 0.62f, 0.18f);
                slot.scale = 1.35f;
                slot.text.text = "<color=#ffcf6a>\u2726</color> " + amount;
                slot.text.fontSize = 32;
            }
            else
            {
                slot.maxLife = 0.72f;
                slot.color = Color.white;
                slot.scale = 1f;
                slot.text.text = amount.ToString();
                slot.text.fontSize = 25;
            }
            slot.life = slot.maxLife;
            slot.text.color = slot.color;
            slot.text.gameObject.SetActive(true);
        }
    }
}
