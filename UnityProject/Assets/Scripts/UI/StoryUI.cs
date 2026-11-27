using UnityEngine;
using UnityEngine.UI;

namespace Ares
{
    /// <summary>
    /// Short, fast, skippable intro shown once before the first run (feedback #6: "可以播放
    /// 阿瑞斯病毒的战斗剧情，切换一定要快"). Three cards, 1.8s each, any click skips.
    /// </summary>
    public class StoryUI : MonoBehaviour
    {
        private Canvas _canvas;
        private Text _chapter;
        private Text _line;
        private Text _hint;
        private Image _fade;
        private Image _vignette;

        private System.Action _onDone;
        private float _timer;
        private int _index;
        private bool _playing;
        private float _shakeTimer;

        private static readonly string[] Chapters = new string[]
        {
            "第 七 天",
            "无 线 电 静 默",
            "今 晚 之 后",
        };

        private static readonly string[] Lines = new string[]
        {
            "隔离带已经封锁第三天。\n物资见底，弹药只剩半个弹夹。",
            "无线电里只有杂音。\n感染者把街区变成了巢穴，而它们正在靠近。",
            "撑到天亮，把能带走的东西都带走。\n—— 活下去。",
        };

        public bool IsPlaying { get { return _playing; } }

        public void Build()
        {
            _canvas = UiFactory.CreateCanvas("StoryCanvas", 40);
            _canvas.transform.SetParent(transform, false);

            _fade = UiFactory.CreateImage(_canvas.transform, "Fade", UiFactory.WhiteSprite, new Color(0f, 0f, 0f, 0f));
            UiFactory.SetFullStretch(_fade.rectTransform);

            _vignette = UiFactory.CreateImage(_canvas.transform, "Vignette", UiFactory.WhiteSprite, new Color(0.5f, 0f, 0f, 0f));
            UiFactory.SetFullStretch(_vignette.rectTransform);

            _chapter = UiFactory.CreateText(_canvas.transform, "Chapter", "", 64, new Color(0.88f, 0.82f, 0.66f), TextAnchor.MiddleLeft);
            UiFactory.Anchor(_chapter.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-80f, 150f), new Vector2(1200f, 90f));

            var bar = UiFactory.CreateImage(_canvas.transform, "Bar", UiFactory.WhiteSprite, new Color(0.75f, 0.24f, 0.18f, 0.9f));
            UiFactory.Anchor(bar.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-80f, 96f), new Vector2(180f, 4f));

            _line = UiFactory.CreateText(_canvas.transform, "Line", "", 30, new Color(0.82f, 0.84f, 0.78f), TextAnchor.MiddleLeft);
            UiFactory.Anchor(_line.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-80f, 20f), new Vector2(1200f, 140f));

            _hint = UiFactory.CreateText(_canvas.transform, "Hint", "点击跳过", 17, new Color(0.6f, 0.62f, 0.58f), TextAnchor.LowerRight);
            UiFactory.Anchor(_hint.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-50f, 40f), new Vector2(300f, 26f));

            SetVisible(false);
        }

        private void SetVisible(bool on)
        {
            if (_canvas != null) _canvas.enabled = on;
        }

        public void Play(System.Action onDone)
        {
            _onDone = onDone;
            _index = 0;
            _timer = 0f;
            _playing = true;
            SetVisible(true);
            ShowCard(0);
        }

        private void ShowCard(int i)
        {
            _index = Mathf.Clamp(i, 0, Chapters.Length - 1);
            if (_chapter != null) _chapter.text = Chapters[_index];
            if (_line != null) _line.text = Lines[_index];
            _timer = 0f;
            if (_canvas != null) _canvas.enabled = true;
        }

        private void Update()
        {
            if (!_playing) return;
            float dt = Time.unscaledDeltaTime;
            _timer += dt;

            // fast fades: 0.22s in, hold, 0.22s out
            float a = Mathf.Clamp01(_timer / 0.22f);
            if (_timer > 1.5f) a = Mathf.Clamp01(1f - (_timer - 1.5f) / 0.22f);
            if (_chapter != null) _chapter.color = new Color(0.88f, 0.82f, 0.66f, a);
            if (_line != null) _line.color = new Color(0.82f, 0.84f, 0.78f, a);

            if (_index == Chapters.Length - 1)
            {
                _shakeTimer += dt;
                if (Game.Camera != null && _shakeTimer > 0.25f)
                {
                    Game.Camera.AddShake(0.18f);
                    _shakeTimer = 0f;
                }
                if (_vignette != null) _vignette.color = new Color(0.45f, 0f, 0f, Mathf.Min(0.16f, _timer * 0.08f));
            }

            bool clicked = Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Escape);
            bool cardDone = _timer >= 1.75f;

            if (clicked)
            {
                // first click skips the card, a second click ends the intro
                if (_index >= Chapters.Length - 1) { Finish(); return; }
                ShowCard(_index + 1);
                return;
            }
            if (cardDone)
            {
                if (_index >= Chapters.Length - 1) { Finish(); return; }
                ShowCard(_index + 1);
            }
        }

        private void Finish()
        {
            _playing = false;
            SetVisible(false);
            if (_vignette != null) _vignette.color = new Color(0.45f, 0f, 0f, 0f);
            var cb = _onDone;
            _onDone = null;
            if (cb != null) cb();
        }
    }
}
