using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Ares
{
    /// <summary>
    /// Opening motion comic. Seven storyboard panels are stitched into a comic page that the camera
    /// pans across, so the reveal flows instead of cutting like slides. Ends on a black card with the
    /// emblem and a slow-blinking prompt.
    /// ESC / Space / Enter skips.
    /// </summary>
    public class Trailer : MonoBehaviour
    {
        private Canvas _canvas;
        private RectTransform _frame;      // moves/squeezes like a comic frame
        private RectTransform _stage;      // clip
        private Image _art;
        private Image _halftone;
        private Image _speed;
        private Image _splat;
        private Image _flash;
        private Image _hero;
        private Image _monsterA;
        private Image _monsterB;
        private Image _captionBox;
        private Text _caption;
        private Text _punch;
        private Text _tag;
        private Image _endPanel;
        private Image _logo;
        private Text _endText;
        private bool _skip;
        private bool _done;

        public bool Finished { get { return _done; } }

        private class Beat
        {
            public string Art;          // single panel
            public string[] Page;       // 1-3 panels shown as a strip and panned
            public string Caption;
            public string Punch;
            public string Tag;
            public float Hold;
            public float Zoom = 1.10f;
            public Vector2 Pan;
            public bool Halftone = true;
            public bool Speed;
            public bool Splat;
            public bool Hero;
            public bool Monster;
            public bool Flash;
            public bool EndCard;
        }

        private readonly List<Beat> _beats = new List<Beat>
        {
            // ---- 世界观
            new Beat { Page = new[] { "Trailer/panel_1", "Trailer/panel_2", "Trailer/panel_6" },
                       Caption = "病毒爆发后的第七个月", Hold = 7.0f, Zoom = 1.0f },
            new Beat { Art = "Trailer/panel_2", Caption = "城市沦陷，通讯中断", Hold = 5.0f, Zoom = 1.16f, Pan = new Vector2(0f, 0.02f) },
            new Beat { Art = "Trailer/panel_4", Caption = "官方把这叫做「生物灾变」", Punch = "嘶——", Hold = 5.0f, Zoom = 1.14f, Splat = true },

            // ---- 剧情
            new Beat { Page = new[] { "Trailer/panel_3", "Trailer/panel_7", "Trailer/panel_5" },
                       Caption = "撤离的直升机，一架也没有回来", Hold = 7.0f, Zoom = 1.0f },
            new Beat { Art = "Trailer/panel_3", Caption = "广播里只剩一句循环播放的指令", Hold = 5.0f, Zoom = 1.15f, Pan = new Vector2(-0.03f, 0f) },
            new Beat { Art = "Trailer/panel_7", Caption = "「活下去，等到天亮」", Hold = 5.0f, Zoom = 1.12f, Flash = true },

            // ---- 人物
            new Beat { Art = "Trailer/panel_1", Caption = "你是 07 号幸存者", Tag = "竞技场里，只剩你和一个背包", Hold = 5.5f, Zoom = 1.08f, Hero = true, Flash = true },
            new Beat { Art = "Trailer/panel_6", Caption = "庇护所里还有商人、铁匠", Tag = "他们卖你的东西，也卖你一条命", Hold = 5.0f, Zoom = 1.10f },

            // ---- 怪物与战斗
            new Beat { Art = "Trailer/panel_7", Caption = "还有它们", Punch = "砰！", Hold = 5.5f, Zoom = 1.20f, Monster = true, Speed = true, Splat = true },
            new Beat { Art = "Trailer/panel_5", Caption = "一刀，一枪，撑过三十波", Hold = 5.5f, Zoom = 1.18f, Speed = true, Flash = true },
            new Beat { Art = "Trailer/panel_3", Caption = "活到天亮", Hold = 4.0f, Zoom = 1.12f },

            // ---- 标题卡
            new Beat { EndCard = true, Hold = 0f },
        };

        // ------------------------------------------------------------------ build

        public void Build()
        {
            _canvas = UiFactory.CreateCanvas("TrailerCanvas", 80);
            _canvas.transform.SetParent(transform, false);

            var bg = UiFactory.CreateImage(_canvas.transform, "Bg", UiFactory.WhiteSprite, new Color(0.03f, 0.03f, 0.04f, 1f));
            UiFactory.SetFullStretch(bg.rectTransform);
            bg.raycastTarget = false;

            _stage = UiFactory.CreateRect(_canvas.transform, "Stage");
            UiFactory.Anchor(_stage, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            _stage.anchorMin = new Vector2(0.03f, 0.05f);
            _stage.anchorMax = new Vector2(0.97f, 0.95f);
            _stage.offsetMin = Vector2.zero;
            _stage.offsetMax = Vector2.zero;
            _stage.gameObject.AddComponent<RectMask2D>();

            _frame = UiFactory.CreateRect(_stage, "Frame");
            UiFactory.SetFullStretch(_frame);

            _art = UiFactory.CreateImage(_frame, "Art", UiFactory.WhiteSprite, Color.white);
            UiFactory.SetFullStretch(_art.rectTransform);
            _art.raycastTarget = false;

            _halftone = UiFactory.CreateImage(_frame, "Halftone", Resources.Load<Sprite>("Trailer/halftone"),
                new Color(1f, 1f, 1f, 0.26f));
            UiFactory.SetFullStretch(_halftone.rectTransform);
            _halftone.type = Image.Type.Tiled;
            _halftone.raycastTarget = false;

            _speed = UiFactory.CreateImage(_frame, "Speed", Resources.Load<Sprite>("Trailer/speedlines"), new Color(1f, 1f, 1f, 0f));
            UiFactory.Anchor(_speed.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1400f, 1400f));
            _speed.raycastTarget = false;

            _splat = UiFactory.CreateImage(_frame, "Splat", Resources.Load<Sprite>("Trailer/ink_splat"), new Color(1f, 1f, 1f, 0f));
            UiFactory.Anchor(_splat.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520f, 520f));
            _splat.raycastTarget = false;

            _hero = UiFactory.CreateImage(_frame, "Hero", Resources.Load<Sprite>("Trailer/trailer_hero"), Color.white);
            UiFactory.Anchor(_hero.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(320f, -40f), new Vector2(360f, 470f));
            _hero.preserveAspect = true;
            _hero.raycastTarget = false;
            _hero.gameObject.SetActive(false);

            // two infected lunging in from the edges for the combat beats
            _monsterA = MakeMonster("Trailer/mon_1", new Vector2(-260f, -60f));
            _monsterB = MakeMonster("Trailer/mon_2", new Vector2(280f, -40f));

            MakeBar(_canvas.transform, "BarT", new Vector2(0.03f, 0.95f), new Vector2(0.97f, 1f));
            MakeBar(_canvas.transform, "BarB", new Vector2(0.03f, 0f), new Vector2(0.97f, 0.05f));
            MakeBar(_canvas.transform, "BarL", new Vector2(0f, 0f), new Vector2(0.03f, 1f));
            MakeBar(_canvas.transform, "BarR", new Vector2(0.97f, 0f), new Vector2(1f, 1f));

            _captionBox = UiFactory.CreateImage(_canvas.transform, "CaptionBox", UiFactory.WhiteSprite, new Color(0.04f, 0.04f, 0.05f, 0.92f));
            UiFactory.Anchor(_captionBox.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(96f, 104f), new Vector2(760f, 66f));
            _captionBox.raycastTarget = false;

            _caption = UiFactory.CreateText(_captionBox.transform, "Caption", "", 30, new Color(0.95f, 0.93f, 0.88f), TextAnchor.MiddleLeft);
            UiFactory.Anchor(_caption.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(22f, 0f), new Vector2(720f, 40f));

            _tag = UiFactory.CreateText(_canvas.transform, "Tag", "", 20, new Color(0.72f, 0.74f, 0.7f), TextAnchor.MiddleLeft);
            UiFactory.Anchor(_tag.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(100f, 62f), new Vector2(820f, 30f));

            _punch = UiFactory.CreateText(_canvas.transform, "Punch", "", 92, new Color(0.94f, 0.9f, 0.84f), TextAnchor.MiddleCenter);
            UiFactory.Anchor(_punch.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(900f, 140f));
            var outline = _punch.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.62f, 0.08f, 0.1f, 0.95f);
            outline.effectDistance = new Vector2(4f, -4f);

            var escHint = UiFactory.CreateText(_canvas.transform, "EscHint", "按 ESC 跳过宣传片", 18,
                new Color(0.78f, 0.8f, 0.78f, 0.85f), TextAnchor.UpperLeft);
            UiFactory.Anchor(escHint.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(26f, -20f), new Vector2(420f, 26f));

            BuildEndCard();

            _flash = UiFactory.CreateImage(_canvas.transform, "Flash", UiFactory.WhiteSprite, new Color(1f, 1f, 1f, 0f));
            UiFactory.SetFullStretch(_flash.rectTransform);
            _flash.raycastTarget = false;
        }

        private Image MakeMonster(string path, Vector2 pos)
        {
            var img = UiFactory.CreateImage(_frame, "Monster", Resources.Load<Sprite>(path), Color.white);
            UiFactory.Anchor(img.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(420f, 420f));
            img.preserveAspect = true;
            img.raycastTarget = false;
            img.gameObject.SetActive(false);
            return img;
        }

        private void BuildEndCard()
        {
            _endPanel = UiFactory.CreateImage(_canvas.transform, "EndPanel", UiFactory.WhiteSprite, new Color(0.02f, 0.02f, 0.03f, 0f));
            UiFactory.SetFullStretch(_endPanel.rectTransform);
            _endPanel.raycastTarget = false;

            _logo = UiFactory.CreateImage(_endPanel.transform, "Logo", Resources.Load<Sprite>("Trailer/logo_mark"), new Color(1f, 1f, 1f, 0f));
            UiFactory.Anchor(_logo.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(300f, 300f));
            _logo.preserveAspect = true;
            _logo.raycastTarget = false;

            _endText = UiFactory.CreateText(_endPanel.transform, "EndText", "按任意按钮开始游戏", 26,
                new Color(0.86f, 0.84f, 0.78f), TextAnchor.MiddleCenter);
            UiFactory.Anchor(_endText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -170f), new Vector2(800f, 40f));
            _endText.color = new Color(0.86f, 0.84f, 0.78f, 0f);

            var title = UiFactory.CreateText(_endPanel.transform, "EndTitle", "阿瑞斯病毒 · 竞技场", 34,
                new Color(0.9f, 0.88f, 0.82f), TextAnchor.MiddleCenter);
            UiFactory.Anchor(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -110f), new Vector2(900f, 50f));
            title.color = new Color(0.9f, 0.88f, 0.82f, 0f);
            _endTitle = title;
        }

        private Text _endTitle;

        private void MakeBar(Transform parent, string name, Vector2 min, Vector2 max)
        {
            var bar = UiFactory.CreateImage(parent, name, UiFactory.WhiteSprite, new Color(0.04f, 0.04f, 0.05f, 1f));
            UiFactory.Anchor(bar.rectTransform, min, max, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            bar.rectTransform.offsetMin = Vector2.zero;
            bar.rectTransform.offsetMax = Vector2.zero;
            bar.raycastTarget = false;
        }

        // ------------------------------------------------------------------ play

        public IEnumerator Play()
        {
            _skip = false;
            _done = false;

            for (int i = 0; i < _beats.Count && !_skip; i++)
            {
                var b = _beats[i];

                if (b.EndCard)
                {
                    yield return PlayEndCard();
                    break;
                }

                ClearPage();
                _frame.localScale = Vector3.one;
                _frame.anchoredPosition = Vector2.zero;

                bool page = b.Page != null && b.Page.Length > 0;
                if (page) BuildPage(b.Page);
                else
                {
                    Sprite s = Resources.Load<Sprite>(b.Art);
                    if (s != null) { _art.sprite = s; _art.color = Color.white; _art.rectTransform.localScale = Vector3.one; }
                }

                _halftone.enabled = b.Halftone;
                _caption.text = b.Caption;
                _tag.text = b.Tag;
                _punch.text = b.Punch;
                _speed.color = new Color(1f, 1f, 1f, b.Speed ? 0.20f : 0f);
                _splat.color = new Color(1f, 1f, 1f, b.Splat ? 0.7f : 0f);
                _hero.gameObject.SetActive(b.Hero);
                _monsterA.gameObject.SetActive(b.Monster);
                _monsterB.gameObject.SetActive(b.Monster);

                if (b.Flash) yield return FlashShot();
                if (!string.IsNullOrEmpty(b.Punch)) StartCoroutine(PunchIn());
                if (!string.IsNullOrEmpty(b.Caption)) StartCoroutine(CaptionIn(0.35f));

                float t = 0f;
                float dur = Mathf.Max(0.5f, b.Hold);
                while (t < dur && !_skip)
                {
                    t += Time.unscaledDeltaTime;
                    float k = Mathf.Clamp01(t / dur);
                    float ease = 1f - Mathf.Pow(1f - k, 1.6f);

                    if (page)
                    {
                        float z = Mathf.Lerp(1f, 1.06f, ease);
                        _frame.localScale = new Vector3(z, z, 1f);
                    }
                    else
                    {
                        float zoom = Mathf.Lerp(1f, b.Zoom, ease);
                        _art.rectTransform.localScale = new Vector3(zoom, zoom, 1f);
                        _art.rectTransform.anchoredPosition = new Vector2(b.Pan.x * 140f * ease, b.Pan.y * 140f * ease);
                    }

                    if (_hero.gameObject.activeSelf)
                    {
                        var hp = _hero.rectTransform.anchoredPosition;
                        hp.x = Mathf.Lerp(560f, 320f, Mathf.Clamp01(k * 2.5f));
                        _hero.rectTransform.anchoredPosition = hp;
                    }
                    if (_monsterA.gameObject.activeSelf)
                    {
                        float lunge = Mathf.Sin(Mathf.Clamp01(k) * Mathf.PI * 1.4f);
                        _monsterA.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(-460f, -150f, lunge), -60f);
                        _monsterB.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(470f, 160f, lunge * 0.9f), -40f);
                    }
                    yield return null;
                }

                _frame.anchoredPosition = Vector2.zero;
            }

            if (!_skip) yield return FlashToBlack();
            _done = true;
            _canvas.gameObject.SetActive(false);
        }

        /// <summary>Comic page: the panels sit side by side in the frame and the frame slowly pushes in.</summary>
        private void BuildPage(string[] ids)
        {
            for (int c = _art.transform.childCount - 1; c >= 0; c--)
            {
                var child = _art.transform.GetChild(c).gameObject;
                if (child.name.StartsWith("PageSlot")) Destroy(child);
            }
            _art.enabled = false;

            int n = Mathf.Max(1, ids.Length);
            for (int i = 0; i < n; i++)
            {
                Sprite sp = Resources.Load<Sprite>(ids[i]);
                if (sp == null) continue;
                var img = UiFactory.CreateImage(_frame, "PageSlot" + i, sp, Color.white);
                var rt = img.rectTransform;
                rt.anchorMin = new Vector2(i / (float)n, 0f);
                rt.anchorMax = new Vector2((i + 1) / (float)n, 1f);
                rt.offsetMin = new Vector2(2f, 0f);
                rt.offsetMax = new Vector2(-2f, 0f);
                img.raycastTarget = false;
            }
        }

        private void ClearPage()
        {
            for (int c = _frame.childCount - 1; c >= 0; c--)
            {
                var child = _frame.GetChild(c).gameObject;
                if (child.name.StartsWith("PageSlot")) Destroy(child);
            }
            if (_art != null) _art.enabled = true;
        }

        private IEnumerator CaptionIn(float dur)
        {
            var rt = _captionBox.rectTransform;
            float t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / dur);
                var c = _captionBox.color; c.a = 0.92f * k; _captionBox.color = c;
                var ct = _caption.color; ct.a = k; _caption.color = ct;
                var tg = _tag.color; tg.a = k * 0.9f; _tag.color = tg;
                rt.anchoredPosition = new Vector2(96f - 36f * (1f - k), 104f);
                yield return null;
            }
        }

        private IEnumerator PunchIn()
        {
            var rt = _punch.rectTransform;
            float t = 0f;
            while (t < 0.26f)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / 0.26f);
                float s = Mathf.Lerp(1.85f, 1f, k * k);
                rt.localScale = new Vector3(s, s, 1f);
                var c = _punch.color; c.a = k; _punch.color = c;
                yield return null;
            }
        }

        private IEnumerator FlashShot()
        {
            float t = 0f;
            while (t < 0.16f)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / 0.16f);
                _flash.color = new Color(1f, 0.94f, 0.92f, Mathf.Lerp(0.5f, 0f, k));
                yield return null;
            }
            _flash.color = new Color(1f, 1f, 1f, 0f);
        }

        private IEnumerator FlashToBlack()
        {
            float t = 0f;
            while (t < 0.6f)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / 0.6f);
                _flash.color = new Color(0f, 0f, 0f, k);
                yield return null;
            }
            _flash.color = new Color(0f, 0f, 0f, 1f);
        }

        /// <summary>Black card: emblem fades in, then a slow dim prompt blinks until a key is pressed.</summary>
        private IEnumerator PlayEndCard()
        {
            _stage.gameObject.SetActive(false);
            _caption.text = ""; _tag.text = ""; _punch.text = "";
            _captionBox.color = new Color(0.04f, 0.04f, 0.05f, 0f);

            var panelColor = _endPanel.color;

            // black
            float t = 0f;
            while (t < 0.8f)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / 0.8f);
                panelColor.a = k;
                _endPanel.color = panelColor;
                _flash.color = new Color(0f, 0f, 0f, 1f - k);
                yield return null;
            }
            _flash.color = new Color(0f, 0f, 0f, 0f);

            // emblem + title fade in
            t = 0f;
            while (t < 1.2f)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / 1.2f);
                var lc = _logo.color; lc.a = k; _logo.color = lc;
                var tc = _endTitle.color; tc.a = k; _endTitle.color = tc;
                yield return null;
            }

            // slow, dim blink (~1.6 s cycle) until the player presses something
            float blink = 0f;
            while (true)
            {
                blink += Time.unscaledDeltaTime;
                float a = 0.35f + 0.5f * Mathf.Abs(Mathf.Sin(blink / 1.6f * Mathf.PI));
                var ec = _endText.color; ec.a = a; _endText.color = ec;

                if (Input.anyKeyDown || Input.GetMouseButtonDown(0)) break;
                yield return null;
            }

            // quick fade out
            t = 0f;
            while (t < 0.4f)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / 0.4f);
                var lc = _logo.color; lc.a = 1f - k; _logo.color = lc;
                var tc = _endTitle.color; tc.a = 1f - k; _endTitle.color = tc;
                var ec = _endText.color; ec.a = 0f; _endText.color = ec;
                panelColor.a = 1f - k; _endPanel.color = panelColor;
                yield return null;
            }
        }

        private void Update()
        {
            if (_done) return;
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
                _skip = true;
        }
    }
}
