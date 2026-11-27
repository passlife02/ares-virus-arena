using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Ares
{
    /// <summary>
    /// Small health bars floating over damaged enemies (feedback #13 item 1). Capped at a fixed
    /// number of rows and only shown for enemies that are actually hurt, so a 1000-enemy stress
    /// run costs nothing.
    /// </summary>
    public class EnemyBars : MonoBehaviour
    {
        private const int MaxBars = 24;

        private Canvas _canvas;
        private RectTransform _root;
        private Image[] _fills;
        private Image[] _backs;
        private RectTransform[] _rects;
        private readonly List<EnemyBase> _shown = new List<EnemyBase>(MaxBars);

        public void Build()
        {
            _canvas = UiFactory.CreateCanvas("EnemyBarsCanvas", 9);
            _canvas.transform.SetParent(transform, false);
            _root = (RectTransform)_canvas.transform;

            _fills = new Image[MaxBars];
            _backs = new Image[MaxBars];
            _rects = new RectTransform[MaxBars];

            for (int i = 0; i < MaxBars; i++)
            {
                var back = UiFactory.CreateImage(_root, "Bar" + i, UiFactory.WhiteSprite, new Color(0.05f, 0.05f, 0.06f, 0.75f));
                UiFactory.Anchor(back.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(56f, 7f));
                back.raycastTarget = false;

                var fill = UiFactory.CreateImage(back.transform, "Fill", UiFactory.WhiteSprite, new Color(0.85f, 0.28f, 0.24f, 1f));
                fill.rectTransform.anchorMin = new Vector2(0f, 0f);
                fill.rectTransform.anchorMax = new Vector2(1f, 1f);
                fill.rectTransform.offsetMin = new Vector2(1f, 1f);
                fill.rectTransform.offsetMax = new Vector2(-1f, -1f);
                fill.type = Image.Type.Filled;
                fill.fillMethod = Image.FillMethod.Horizontal;
                fill.fillOrigin = 0;
                fill.raycastTarget = false;

                _backs[i] = back;
                _fills[i] = fill;
                _rects[i] = back.rectTransform;
                back.gameObject.SetActive(false);
            }
        }

        public void SetVisible(bool on)
        {
            if (_canvas != null) _canvas.enabled = on;
        }

        private void LateUpdate()
        {
            if (_fills == null || _canvas == null || !_canvas.enabled) return;

            var sp = Game.Spawner;
            var cam = Game.Camera != null ? Game.Camera.Cam : null;
            if (sp == null || cam == null) return;

            int used = 0;
            for (int i = 0; i < sp.AliveCount && used < MaxBars; i++)
            {
                var e = sp.Alive[i];
                if (e == null || !e.IsAlive) continue;
                if (e.Hp01 >= 0.999f) continue;          // only show once it has been hurt

                Vector3 world = e.transform.position + Vector3.up * 0.85f;
                Vector3 screen = cam.WorldToScreenPoint(world);
                if (screen.z < 0f) continue;
                if (screen.x < -40f || screen.y < -40f || screen.x > Screen.width + 40f || screen.y > Screen.height + 40f) continue;

                Vector2 local;
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screen, null, out local)) continue;

                _rects[used].anchoredPosition = local;
                _rects[used].gameObject.SetActive(true);
                _fills[used].fillAmount = e.Hp01;

                // bosses keep a wider, golden bar
                bool boss = e.EnemyKind == EnemyKind.Boss;
                _rects[used].sizeDelta = boss ? new Vector2(96f, 10f) : new Vector2(56f, 7f);
                _fills[used].color = boss
                    ? new Color(1f, 0.72f, 0.25f, 1f)
                    : Color.Lerp(new Color(0.9f, 0.2f, 0.18f), new Color(0.95f, 0.55f, 0.2f), e.Hp01);
                used++;
            }

            for (int i = used; i < MaxBars; i++)
                if (_rects[i].gameObject.activeSelf) _rects[i].gameObject.SetActive(false);
        }
    }
}
