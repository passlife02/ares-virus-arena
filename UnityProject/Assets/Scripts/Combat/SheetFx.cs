using UnityEngine;

namespace Ares
{
    /// <summary>
    /// Plays a pixelfx sprite sheet (imported as separate sprites) as a one-shot effect.
    /// The sheets come from the pixelfx generator: melee/sword-slash and melee/claw-swipe.
    /// </summary>
    public class SheetFx : MonoBehaviour
    {
        private Sprite[] _frames;
        private SpriteRenderer _sr;
        private float _fps;
        private float _t;
        private int _shown;

        public static void Play(string sheet, Vector2 pos, float scale, float angleDeg,
                                int sortingOrder = 62, float fps = 60f, Color? tint = null)
        {
            var root = Game.EffectRoot;
            if (root == null) return;

            var frames = Resources.LoadAll<Sprite>("Sheet/" + sheet);
            if (frames == null || frames.Length == 0) return;
            System.Array.Sort(frames, (a, b) => string.CompareOrdinal(a.name, b.name));

            var go = new GameObject("SheetFx_" + sheet);
            go.transform.SetParent(root, false);
            go.transform.position = new Vector3(pos.x, pos.y, 0f);
            go.transform.rotation = Quaternion.Euler(0f, 0f, angleDeg);
            go.transform.localScale = new Vector3(scale, scale, 1f);

            var fx = go.AddComponent<SheetFx>();
            fx._frames = frames;
            fx._fps = Mathf.Max(6f, fps);
            fx._sr = go.AddComponent<SpriteRenderer>();
            fx._sr.sortingOrder = sortingOrder;
            fx._sr.color = tint ?? Color.white;
            fx._sr.sprite = frames[0];
        }

        private void Update()
        {
            if (_frames == null || _frames.Length == 0) { Destroy(gameObject); return; }
            _t += Time.deltaTime;
            int idx = Mathf.FloorToInt(_t * _fps);
            if (idx >= _frames.Length) { Destroy(gameObject); return; }
            if (idx != _shown && _sr != null) { _shown = idx; _sr.sprite = _frames[idx]; }
        }
    }
}
