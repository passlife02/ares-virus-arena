using UnityEngine;

namespace Ares
{
    /// <summary>
    /// The small "about to hit you" pip above an enemy. It fades in from a pale tint to a deep red
    /// as the wind-up completes, so the urgency reads without painting a big ring on the floor.
    /// </summary>
    public class WarnPip : MonoBehaviour
    {
        private SpriteRenderer _sr;
        private float _t;
        private float _dur = 0.5f;

        public static void Spawn(Vector2 pos, float duration, float size)
        {
            var root = Game.EffectRoot;
            if (root == null) return;

            var sprite = Resources.Load<Sprite>("Fx/fx_warn");
            if (sprite == null) return;

            var go = new GameObject("WarnPip");
            go.transform.SetParent(root, false);
            go.transform.position = new Vector3(pos.x, pos.y, 0f);

            var pip = go.AddComponent<WarnPip>();
            pip._sr = go.AddComponent<SpriteRenderer>();
            pip._sr.sprite = sprite;
            pip._sr.sortingOrder = 44;
            pip._dur = Mathf.Max(0.15f, duration);
            pip._size = size;
            pip.Apply(0f);
        }

        private float _size = 0.55f;

        private void Apply(float k)
        {
            if (_sr == null) return;
            // pale pink → deep red, and a small pop at the start
            Color c = Color.Lerp(new Color(1f, 0.78f, 0.74f, 0.25f), new Color(1f, 0.16f, 0.12f, 1f), k);
            _sr.color = c;
            float pop = 1f + 0.12f * Mathf.Sin(k * Mathf.PI);
            transform.localScale = new Vector3(_size * pop, _size * pop, 1f);
        }

        private void Update()
        {
            _t += Time.deltaTime;
            float k = Mathf.Clamp01(_t / _dur);
            Apply(k);
            if (k >= 1f) Destroy(gameObject);
        }
    }
}
