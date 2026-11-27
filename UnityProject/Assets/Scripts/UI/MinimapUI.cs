using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Ares
{
    /// <summary>
    /// Compass style minimap (feedback #9): the player is a fixed green dot at the centre,
    /// the map rotates with the aim direction, normal enemies are purple, bosses red and
    /// the arena walls are drawn as static blocks.
    /// </summary>
    public class MinimapUI : MonoBehaviour
    {
        private Canvas _canvas;
        private RectTransform _content;
        private Image _playerDot;
        private Image _heading;
        private Image _bias;
        private Text _label;

        private readonly List<Image> _dotPool = new List<Image>(80);
        private readonly List<Image> _wallImages = new List<Image>();
        private float _radiusPx = 130f;
        private float _rotation;
        private float _updateTimer;

        public void Build()
        {
            _canvas = UiFactory.CreateCanvas("MinimapCanvas", 15);
            _canvas.transform.SetParent(transform, false);

            var panel = UiFactory.CreateImage(_canvas.transform, "MinimapPanel", UiFactory.RingSprite, new Color(0.35f, 0.4f, 0.36f, 0.9f));
            UiFactory.Anchor(panel.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-30f, -30f), new Vector2(280f, 280f));

            var back = UiFactory.CreateImage(panel.transform, "Back", UiFactory.DotSprite, new Color(0.04f, 0.05f, 0.05f, 0.88f));
            UiFactory.Anchor(back.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(268f, 268f));

            _content = UiFactory.CreateRect(back.transform, "Content");
            UiFactory.Anchor(_content, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(268f, 268f));

            _bias = UiFactory.CreateImage(back.transform, "Bias", UiFactory.DotSprite, new Color(0f, 0f, 0f, 0f));
            UiFactory.Anchor(_bias.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(20f, 20f));

            _playerDot = UiFactory.CreateImage(back.transform, "Player", UiFactory.DotSprite, new Color(0.35f, 1f, 0.45f, 1f));
            UiFactory.Anchor(_playerDot.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(16f, 16f));
            var glow = _playerDot.gameObject.AddComponent<Outline>();
            glow.effectColor = new Color(0f, 0f, 0f, 0.85f);
            glow.effectDistance = new Vector2(1.5f, -1.5f);

            _heading = UiFactory.CreateImage(back.transform, "Heading", UiFactory.ArrowSprite, new Color(0.55f, 1f, 0.6f, 0.95f));
            UiFactory.Anchor(_heading.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.35f), Vector2.zero, new Vector2(14f, 14f));

            // north tick rotates with the map so "up" is always where the player is aiming
            _label = UiFactory.CreateText(back.transform, "Label", "N", 16, new Color(0.75f, 0.8f, 0.72f, 0.9f), TextAnchor.UpperCenter);
            UiFactory.Anchor(_label.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -6f), new Vector2(40f, 22f));

            BuildWalls();

            for (int i = 0; i < 70; i++)
            {
                var dot = UiFactory.CreateImage(_content, "Dot" + i, UiFactory.DotSprite, Color.white);
                UiFactory.Anchor(dot.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(9f, 9f));
                dot.gameObject.SetActive(false);
                _dotPool.Add(dot);
            }

            SetVisible(false);
        }

        /// <summary>Rebuilds the static wall overlay for the map that is currently active.</summary>
        public void RebuildWalls()
        {
            for (int i = 0; i < _wallImages.Count; i++)
                if (_wallImages[i] != null) Destroy(_wallImages[i].gameObject);
            _wallImages.Clear();
            BuildWalls();
        }

        /// <summary>Static walls are baked once per map; the map itself never moves.</summary>
        private void BuildWalls()
        {
            var layout = Game.Manager != null ? Game.Manager.Arena : null;
            if (layout == null) return;

            _radiusPx = 130f;
            float scale = _radiusPx / Mathf.Max(1f, Game.Cfg.minimapWorldRange);
            foreach (var r in layout.Walls)
            {
                var img = UiFactory.CreateImage(_content, "W", UiFactory.WhiteSprite, new Color(0.34f, 0.36f, 0.4f, 0.85f));
                Vector2 size = new Vector2(Mathf.Max(2f, r.width * scale), Mathf.Max(2f, r.height * scale));
                UiFactory.Anchor(img.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(r.center.x * scale, r.center.y * scale), size);
                _wallImages.Add(img);
            }
        }

        public void SetVisible(bool visible)
        {
            if (_canvas != null) _canvas.enabled = visible;
        }

        private void LateUpdate()
        {
            if (_canvas == null || !_canvas.enabled) return;
            var player = Game.Player;
            if (player == null) return;

            // North-up minimap: the map itself never rotates (a spinning map made players dizzy).
            // Direction is still readable from the heading arrow on the player dot.
            if (_content != null && _content.localRotation != Quaternion.identity)
                _content.localRotation = Quaternion.identity;
            if (_heading != null)
            {
                float aimAngle = Mathf.Atan2(player.AimDir.y, player.AimDir.x) * Mathf.Rad2Deg;
                _heading.rectTransform.localRotation = Quaternion.Euler(0f, 0f, aimAngle - 90f);
            }

            _updateTimer -= Time.unscaledDeltaTime;
            if (_updateTimer > 0f) return;
            _updateTimer = 0.06f;

            // the minimap is world-fixed: walls stay put, the player dot slides across it
            float scale2 = _radiusPx / Mathf.Max(1f, Game.Cfg.minimapWorldRange);
            Vector2 p = player.Position;

            if (_playerDot != null)
            {
                Vector2 dotPos = p * scale2;
                if (dotPos.magnitude > _radiusPx - 10f) dotPos = dotPos.normalized * (_radiusPx - 10f);
                _playerDot.rectTransform.anchoredPosition = dotPos;
            }
            if (_heading != null)
                _heading.rectTransform.anchoredPosition = _playerDot != null
                    ? _playerDot.rectTransform.anchoredPosition
                    : Vector2.zero;

            // walls are static; only their rotation changes (handled above)
            var sp = Game.Spawner;
            int used = 0;
            if (sp != null)
            {
                var alive = sp.Alive;
                for (int i = 0; i < alive.Count && used < _dotPool.Count; i++)
                {
                    var e = alive[i];
                    if (e == null || !e.IsAlive) continue;
                    Vector2 world = e.Position * scale2;
                    if (world.magnitude > _radiusPx - 8f) continue;   // outside the dial

                    var dot = _dotPool[used++];
                    dot.gameObject.SetActive(true);
                    dot.rectTransform.anchoredPosition = world;
                    bool boss = e.EnemyKind == EnemyKind.Boss;
                    dot.rectTransform.sizeDelta = boss ? new Vector2(15f, 15f) : new Vector2(9f, 9f);
                    dot.color = boss ? new Color(1f, 0.18f, 0.14f, 1f) : new Color(0.78f, 0.36f, 1f, 0.95f);
                }
            }
            for (int i = used; i < _dotPool.Count; i++)
                if (_dotPool[i].gameObject.activeSelf) _dotPool[i].gameObject.SetActive(false);

            if (_label != null) _label.rectTransform.localRotation = Quaternion.identity;
        }
    }
}
