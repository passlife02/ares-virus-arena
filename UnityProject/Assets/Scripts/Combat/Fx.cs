using UnityEngine;

namespace Ares
{
    /// <summary>
    /// Lightweight one-shot sprite effects (feedback #10). Textures are ink-style drawings in
    /// Resources/Fx so the melee hits read clearly without a particle system.
    /// </summary>
    public static class Fx
    {
        private static Sprite[] _cache;
        private static string[] _names;

        private static bool Loaded()
        {
            if (_cache != null) return true;
            var all = Resources.LoadAll<Sprite>("Fx");
            if (all == null || all.Length == 0) return false;
            _cache = all;
            _names = new string[all.Length];
            for (int i = 0; i < all.Length; i++) _names[i] = all[i].name;
            return true;
        }

        public static Sprite Get(string name)
        {
            if (!Loaded()) return null;
            for (int i = 0; i < _names.Length; i++)
                if (_names[i] == name) return _cache[i];
            return null;
        }

        /// <summary>Spawns a short-lived quad that grows/fades. Unscaled time so pauses still draw.</summary>
        public static void Play(string name, Vector2 pos, float size, float duration, float angleDeg = 0f, Color? tint = null, int sortingOrder = 55)
        {
            var sprite = Get(name);
            if (sprite == null) return;
            var root = Game.EffectRoot;
            if (root == null) return;

            var go = new GameObject("Fx_" + name);
            go.transform.SetParent(root, false);
            go.transform.position = new Vector3(pos.x, pos.y, 0f);
            go.transform.rotation = Quaternion.Euler(0f, 0f, angleDeg);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = sortingOrder;
            sr.color = tint ?? Color.white;

            var anim = go.AddComponent<FxSprite>();
            anim.Life = duration;
            anim.StartScale = size;
            anim.EndScale = size * 1.18f;
        }
    }

    public class FxSprite : MonoBehaviour
    {
        public float Life = 0.2f;
        public float StartScale = 1f;
        public float EndScale = 1.2f;

        private float _t;
        private SpriteRenderer _sr;

        private void Awake() { _sr = GetComponent<SpriteRenderer>(); }

        private void Update()
        {
            _t += Time.deltaTime;
            float k = Mathf.Clamp01(_t / Mathf.Max(0.01f, Life));
            float s = Mathf.Lerp(StartScale, EndScale, k);
            transform.localScale = new Vector3(s, s, 1f);

            if (_sr != null)
            {
                var c = _sr.color;
                c.a = Mathf.Clamp01(1f - k * k);
                _sr.color = c;
            }
            if (_t >= Life) Destroy(gameObject);
        }
    }
}
