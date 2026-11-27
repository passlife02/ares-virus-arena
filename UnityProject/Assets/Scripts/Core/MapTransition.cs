using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Ares
{
    /// <summary>
    /// Fades the screen to black, swaps the map, drops the player on the new spawn point and shows
    /// the map name while it fades back in. Used instead of swapping maps live, which could trap
    /// the character inside freshly built colliders.
    /// </summary>
    public class MapTransition : MonoBehaviour
    {
        private Canvas _canvas;
        private Image _black;
        private Text _label;
        private Text _sub;

        public bool Busy { get; private set; }

        public void Build()
        {
            _canvas = UiFactory.CreateCanvas("MapTransitionCanvas", 60);
            _canvas.transform.SetParent(transform, false);

            _black = UiFactory.CreateImage(_canvas.transform, "Black", UiFactory.WhiteSprite, new Color(0f, 0f, 0f, 0f));
            UiFactory.SetFullStretch(_black.rectTransform);
            _black.raycastTarget = false;

            _label = UiFactory.CreateText(_canvas.transform, "MapName", "", 54,
                new Color(0.86f, 0.83f, 0.7f), TextAnchor.MiddleCenter);
            UiFactory.Anchor(_label.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(0f, 18f), new Vector2(900f, 80f));
            _label.color = new Color(0.86f, 0.83f, 0.7f, 0f);

            _sub = UiFactory.CreateText(_canvas.transform, "MapSub", "正在进入场地", 22,
                new Color(0.7f, 0.72f, 0.68f), TextAnchor.MiddleCenter);
            UiFactory.Anchor(_sub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(0f, -34f), new Vector2(900f, 32f));
            _sub.color = new Color(0.7f, 0.72f, 0.68f, 0f);
        }

        /// <summary>Fades out, swaps the map, fades back in with the name in the middle.</summary>
        public void Switch(MapVariant variant, GameManager mgr)
        {
            if (Busy) return;
            StartCoroutine(Run(variant, mgr));
        }

        private IEnumerator Run(MapVariant variant, GameManager mgr)
        {
            Busy = true;
            if (_black != null) _black.raycastTarget = true;

            // fade to black
            yield return Fade(0f, 1f, 0.30f);

            if (mgr != null)
            {
                MapBuilder.Show(variant, mgr);
                mgr.Map = variant;
                if (Game.Player != null && mgr.Arena != null)
                {
                    Game.Player.Spawn(mgr.Arena.PlayerStart);
                    Game.Player.SetFrozen(false);
                }
                if (Game.Camera != null && mgr.Arena != null)
                    Game.Camera.Configure(Game.Camera.GetComponent<Camera>(),
                        Game.Player != null ? Game.Player.transform : mgr.transform,
                        mgr.ArenaMin, mgr.ArenaMax, Game.Cfg.cameraSize);
                if (mgr.Minimap != null) mgr.Minimap.RebuildWalls();
            }

            if (_label != null) _label.text = MapBuilder.Names[(int)variant];

            // hold the name on black
            yield return Fade(1f, 1f, 0.55f, 0.05f);
            yield return new WaitForSecondsRealtime(0.35f);

            // fade back in
            yield return Fade(1f, 0f, 0.45f);

            if (_black != null) _black.raycastTarget = false;
            SetLabelAlpha(0f);
            Busy = false;
        }

        private IEnumerator Fade(float from, float to, float duration, float labelAlpha = -1f)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / Mathf.Max(0.01f, duration));
                if (_black != null)
                {
                    var c = _black.color;
                    c.a = Mathf.Lerp(from, to, k);
                    _black.color = c;
                }
                if (labelAlpha >= 0f)
                {
                    SetLabelAlpha(Mathf.Lerp(0f, labelAlpha, k));
                }
                else
                {
                    // the name rides the blackout: invisible when the screen is clear
                    float blackA = _black != null ? _black.color.a : 0f;
                    SetLabelAlpha(Mathf.Clamp01(blackA * blackA));
                }
                yield return null;
            }
        }

        private void SetLabelAlpha(float a)
        {
            if (_label != null) { var c = _label.color; c.a = a; _label.color = c; }
            if (_sub != null) { var c = _sub.color; c.a = a * 0.9f; _sub.color = c; }
        }
    }
}
