using UnityEngine;

namespace Ares
{
    /// <summary>
    /// The enemy wind-up cone. Same sector sprite the player's melee uses, sized to the real
    /// attack reach, so the telegraph matches the hit instead of painting a big red circle.
    /// </summary>
    public class SectorTelegraph : MonoBehaviour
    {
        private SpriteRenderer _sr;
        private float _t;
        private float _dur = 0.5f;

        public static void Show(Transform owner, float reach, float duration, Color tint, float aimAngle)
        {
            var root = Game.EffectRoot;
            if (root == null || owner == null) return;

            var sprite = Resources.Load<Sprite>("Fx/melee_arc");
            if (sprite == null) return;

            var go = new GameObject("SectorTelegraph");
            go.transform.SetParent(root, false);
            go.transform.position = owner.position;
            go.transform.rotation = Quaternion.Euler(0f, 0f, aimAngle);

            var st = go.AddComponent<SectorTelegraph>();
            st._sr = go.AddComponent<SpriteRenderer>();
            st._sr.sprite = sprite;
            st._sr.sortingOrder = -174;
            st._dur = Mathf.Max(0.15f, duration);

            // the sprite's sector spans about 2.15x its radius, same convention as MeleeArcView
            float s = reach * 2.15f;
            go.transform.localScale = new Vector3(s, s, 1f);

            Color c = tint;
            c.a = 0.12f;
            st._sr.color = c;
        }

        private void Update()
        {
            _t += Time.deltaTime;
            float k = Mathf.Clamp01(_t / _dur);
            if (_sr == null) { Destroy(gameObject); return; }
            Color c = _sr.color;
            c.a = Mathf.Lerp(0.10f, 0.34f, k);   // fades up as the hit lands
            _sr.color = c;
            if (k >= 1f) Destroy(gameObject);
        }
    }
}
