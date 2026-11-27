using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Ares
{
    /// <summary>Code driven uGUI construction helpers, plus procedurally generated sprites
    /// (scope crosshair, dots, rings) so the HUD stays crisp at any resolution.</summary>
    public static class UiFactory
    {
        private static Font _font;
        private static Sprite _white;
        private static Sprite _scope;
        private static Sprite _dot;
        private static Sprite _ring;
        private static Sprite _arrow;
        private static Sprite _arc;

        public static Font UiFont
        {
            get
            {
                if (_font != null) return _font;
                try { _font = Font.CreateDynamicFontFromOSFont(new string[] { "Microsoft YaHei", "微软雅黑", "SimHei", "黑体", "Arial" }, 28); }
                catch (Exception) { _font = null; }
                if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return _font;
            }
        }

        private static Sprite _panel;
        private static bool _panelLoaded;

        /// <summary>Nine-sliced grunge frame used by the inventory / codex panels.</summary>
        public static Sprite PanelSprite
        {
            get
            {
                if (_panelLoaded) return _panel;
                _panelLoaded = true;
                _panel = Resources.Load<Sprite>("UI/ui_panel_round");
                if (_panel == null) _panel = Resources.Load<Sprite>("UI/ui_panel_grunge");
                return _panel;
            }
        }

        private static Sprite _btnSprite;
        private static bool _btnLoaded;

        public static Sprite ButtonSprite
        {
            get
            {
                if (_btnLoaded) return _btnSprite;
                _btnLoaded = true;
                _btnSprite = Resources.Load<Sprite>("UI/ui_btn_round");
                return _btnSprite;
            }
        }

        public static Sprite LoadSprite(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            return Resources.Load<Sprite>("UI/" + name);
        }

        // ---------------------------------------------------------------- procedural sprites

        private static Sprite FromTexture(Texture2D tex)
        {
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        }

        public static Sprite WhiteSprite
        {
            get
            {
                if (_white != null) return _white;
                var tex = new Texture2D(8, 8, TextureFormat.RGBA32, false);
                var px = new Color32[64];
                for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
                tex.SetPixels32(px);
                tex.Apply();
                tex.wrapMode = TextureWrapMode.Clamp;
                _white = FromTexture(tex);
                return _white;
            }
        }

        public static Sprite DotSprite
        {
            get
            {
                if (_dot != null) return _dot;
                const int n = 32;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
                var px = new Color32[n * n];
                float r = n * 0.5f - 1f;
                var c = new Vector2(n * 0.5f, n * 0.5f);
                for (int y = 0; y < n; y++)
                {
                    for (int x = 0; x < n; x++)
                    {
                        float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c);
                        float a = Mathf.Clamp01((r - d) * 1.6f);
                        px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
                    }
                }
                tex.SetPixels32(px);
                tex.Apply();
                _dot = FromTexture(tex);
                return _dot;
            }
        }

        public static Sprite RingSprite
        {
            get
            {
                if (_ring != null) return _ring;
                const int n = 96;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
                var px = new Color32[n * n];
                var c = new Vector2(n * 0.5f, n * 0.5f);
                float outer = n * 0.5f - 1f;
                float inner = outer - 3.5f;
                for (int y = 0; y < n; y++)
                {
                    for (int x = 0; x < n; x++)
                    {
                        float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c);
                        float a = Mathf.Clamp01(outer - d) * Mathf.Clamp01(d - inner);
                        px[y * n + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255));
                    }
                }
                tex.SetPixels32(px);
                tex.Apply();
                _ring = FromTexture(tex);
                return _ring;
            }
        }

        /// <summary>Scope style reticle: ring + four ticks + centre dot.</summary>
        public static Sprite ScopeSprite
        {
            get
            {
                if (_scope != null) return _scope;
                const int n = 128;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
                var px = new Color32[n * n];
                for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 0);
                var c = new Vector2(n * 0.5f, n * 0.5f);

                for (int y = 0; y < n; y++)
                {
                    for (int x = 0; x < n; x++)
                    {
                        Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                        float d = Vector2.Distance(p, c);
                        float a = 0f;

                        // outer ring
                        a = Mathf.Max(a, Mathf.Clamp01(31f - d) * Mathf.Clamp01(d - 28f));
                        // inner thin ring
                        a = Mathf.Max(a, Mathf.Clamp01(12.4f - d) * Mathf.Clamp01(d - 11.2f) * 0.8f);
                        // centre dot
                        a = Mathf.Max(a, Mathf.Clamp01(2.6f - d));

                        // four ticks from r=36 to r=46 along the axes
                        if (d > 35f && d < 47f)
                        {
                            if (Mathf.Abs(p.x - c.x) < 1.6f || Mathf.Abs(p.y - c.y) < 1.6f) a = 1f;
                        }
                        // gap at the very top / bottom of the ring so it reads as a scope
                        if (Mathf.Abs(p.y - c.y) < 2.2f && d > 26f && d < 33f) a = 0f;

                        px[y * n + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255));
                    }
                }
                tex.SetPixels32(px);
                tex.Apply();
                _scope = FromTexture(tex);
                return _scope;
            }
        }

        /// <summary>Filled wedge (-55..+55 degrees around +X) used as the melee reach indicator.</summary>
        public static Sprite ArcSprite
        {
            get
            {
                if (_arc != null) return _arc;
                const int n = 256;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
                var px = new Color32[n * n];
                var c = new Vector2(n * 0.5f, n * 0.5f);
                float maxR = n * 0.5f - 1f;
                for (int y = 0; y < n; y++)
                {
                    for (int x = 0; x < n; x++)
                    {
                        Vector2 p = new Vector2(x + 0.5f, y + 0.5f) - c;
                        float d = p.magnitude;
                        float ang = Mathf.Atan2(p.y, p.x) * Mathf.Rad2Deg;
                        float a = 0f;
                        if (Mathf.Abs(ang) < 55f && d < maxR)
                        {
                            // soft inner + outer falloff
                            a = Mathf.Clamp01((maxR - d) / (maxR * 0.16f));
                            a *= Mathf.Clamp01((55f - Mathf.Abs(ang)) / 7f);
                            a *= 0.9f;
                        }
                        px[y * n + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255));
                    }
                }
                tex.SetPixels32(px);
                tex.Apply();
                _arc = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
                return _arc;
            }
        }

        public static Sprite ArrowSprite
        {
            get
            {
                if (_arrow != null) return _arrow;
                const int n = 48;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                        tex.SetPixel(x, y, new Color(1, 1, 1, 0));
                for (int y = 0; y < n; y++)
                {
                    int half = Mathf.RoundToInt((1f - y / (float)n) * (n * 0.42f));
                    for (int x = 0; x < n; x++)
                    {
                        int cx = n / 2;
                        if (Mathf.Abs(x - cx) <= half) tex.SetPixel(x, y, Color.white);
                    }
                }
                tex.Apply();
                _arrow = FromTexture(tex);
                return _arrow;
            }
        }

        // ---------------------------------------------------------------- construction

        public static Canvas CreateCanvas(string name, int sortOrder)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortOrder;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            UnityEngine.Object.DontDestroyOnLoad(go);
        }

        public static RectTransform CreateRect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static RectTransform Anchor(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 size)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            return rt;
        }

        public static Image CreateImage(Transform parent, string name, Sprite sprite, Color color)
        {
            var rt = CreateRect(parent, name);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            if (sprite != null) img.type = Image.Type.Simple;
            return img;
        }

        public static Text CreateText(Transform parent, string name, string content, int size, Color color, TextAnchor anchor)
        {
            var rt = CreateRect(parent, name);
            var txt = rt.gameObject.AddComponent<Text>();
            txt.font = UiFont;
            txt.text = content;
            txt.fontSize = size;
            txt.color = color;
            txt.alignment = anchor;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            txt.raycastTarget = false;
            txt.supportRichText = true;
            return txt;
        }

        public static Button CreateButton(Transform parent, string name, string label, int fontSize, Action onClick)
        {
            var rt = CreateRect(parent, name);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = ButtonSprite;
            img.type = Image.Type.Sliced;
            img.color = new Color(0.2f, 0.22f, 0.23f, 0.95f);
            img.raycastTarget = true;

            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.None;

            var fx = rt.gameObject.AddComponent<UiPressFx>();
            fx.Target = img;
            fx.ScaleRoot = rt;
            fx.Normal = new Color(0.2f, 0.22f, 0.23f, 0.95f);
            fx.Hover = new Color(0.36f, 0.43f, 0.3f, 1f);
            fx.Pressed = new Color(0.14f, 0.17f, 0.1f, 1f);
            fx.Flash = new Color(0.66f, 0.72f, 0.5f, 1f);

            var txt = CreateText(rt, "Label", label, fontSize, new Color(0.93f, 0.94f, 0.87f), TextAnchor.MiddleCenter);
            SetFullStretch(txt.rectTransform);
            btn.onClick.AddListener(() => AudioManager.PlaySfx("sfx_click", 0.55f, 0.05f));
            if (onClick != null) btn.onClick.AddListener(() => onClick());
            return btn;
        }

        public static void SetFullStretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
