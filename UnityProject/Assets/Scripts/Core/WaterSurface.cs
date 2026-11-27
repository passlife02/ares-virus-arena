using System.Collections.Generic;
using UnityEngine;

namespace Ares
{
    /// <summary>
    /// Lake surface: murky water tile that drifts, expanding ripple rings at random points, and a
    /// few pieces of floating debris. All positions are sampled inside the water rects only.
    /// </summary>
    public class WaterSurface : MonoBehaviour
    {
        private readonly List<SpriteRenderer> _tiles = new List<SpriteRenderer>();
        private readonly List<Rect> _rects = new List<Rect>();
        private readonly List<SpriteRenderer> _debris = new List<SpriteRenderer>();
        private Sprite _ringSprite;
        private Sprite[] _debrisSprites;
        private float _rippleTimer = 0.3f;
        private float _drift;

        public static WaterSurface Current { get; private set; }

        public static void SetCurrent(WaterSurface ws) { Current = ws; }

        public static WaterSurface Attach(Transform parent, List<Rect> waterRects)
        {
            if (parent == null) return null;
            var go = new GameObject("WaterSurface");
            go.transform.SetParent(parent, false);
            var ws = go.AddComponent<WaterSurface>();
            Current = ws;
            ws._rects.AddRange(waterRects);
            return ws;
        }

        public void Register(SpriteRenderer sr)
        {
            if (sr == null || _tiles.Contains(sr)) return;
            _tiles.Add(sr);
        }

        private void Start()
        {
            _ringSprite = Resources.Load<Sprite>("Weather/water_ripple");
            _debrisSprites = new Sprite[]
            {
                Resources.Load<Sprite>("Weather/debris_plank"),
                Resources.Load<Sprite>("Weather/debris_can"),
                Resources.Load<Sprite>("Weather/debris_rag"),
            };
            SpawnDebris();
        }

        /// <summary>A random point inside one of the water bodies (used by rain splashes).</summary>
        public bool TryRandomSurfacePoint(out Vector2 point)
        {
            point = Vector2.zero;
            if (_rects.Count == 0) return false;
            var r = _rects[Random.Range(0, _rects.Count)];
            if (r.width < 0.6f || r.height < 0.6f) return false;
            point = new Vector2(Random.Range(r.xMin + 0.3f, r.xMax - 0.3f),
                                Random.Range(r.yMin + 0.3f, r.yMax - 0.3f));
            return true;
        }

        private void SpawnDebris()
        {
            if (_debrisSprites == null || _debrisSprites.Length == 0) return;
            for (int i = 0; i < 7; i++)
            {
                Vector2 p;
                if (!TryRandomSurfacePoint(out p)) break;
                var sp = _debrisSprites[Random.Range(0, _debrisSprites.Length)];
                if (sp == null) continue;

                var go = new GameObject("Debris");
                go.transform.SetParent(transform, false);
                go.transform.position = new Vector3(p.x, p.y, 0f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sp;
                sr.color = new Color(1f, 1f, 1f, 0.75f);
                sr.sortingOrder = -167;
                go.transform.localScale = Vector3.one * Random.Range(0.55f, 0.9f);
                go.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
                _debris.Add(sr);
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            // slow drift so the murky tile does not look frozen
            _drift += dt;
            for (int i = 0; i < _tiles.Count; i++)
            {
                var sr = _tiles[i];
                if (sr == null) continue;
                var mat = sr.material;
                if (mat == null || mat.mainTexture == null) continue;
                var off = mat.mainTextureOffset;
                off.y += dt * 0.022f;
                off.x += dt * 0.007f;
                mat.mainTextureOffset = off;
            }

            // debris bobs and slides
            for (int i = 0; i < _debris.Count; i++)
            {
                var sr = _debris[i];
                if (sr == null) continue;
                var t = sr.transform;
                t.position += new Vector3(Mathf.Sin(_drift * 0.3f + i) * 0.04f * dt,
                                          Mathf.Cos(_drift * 0.24f + i * 1.7f) * 0.03f * dt, 0f);
                t.rotation = Quaternion.Euler(0f, 0f, Mathf.Sin(_drift * 0.5f + i) * 8f +
                    t.rotation.eulerAngles.z * 0f);
            }

            if (_rects.Count == 0 || _ringSprite == null) return;

            _rippleTimer -= dt;
            if (_rippleTimer > 0f) return;
            // irregular spacing so the rings never form a grid
            _rippleTimer = Random.Range(0.18f, 0.55f);
            SpanRipple();
        }

        private void SpanRipple()
        {
            Vector2 p;
            if (!TryRandomSurfacePoint(out p)) return;

            var go = new GameObject("Ripple");
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(p.x, p.y, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _ringSprite;
            sr.color = new Color(0.86f, 0.93f, 0.96f, 0.5f);
            sr.sortingOrder = -166;
            go.transform.localScale = Vector3.one * 0.05f;
            go.AddComponent<RippleFade>();
        }

        private class RippleFade : MonoBehaviour
        {
            private const float Life = 1.35f;
            private float _t;
            private SpriteRenderer _sr;
            private float _max = 1.0f;

            private void Awake()
            {
                _sr = GetComponent<SpriteRenderer>();
                _max = Random.Range(0.7f, 1.25f);
            }

            private void Update()
            {
                _t += Time.deltaTime;
                float k = Mathf.Clamp01(_t / Life);
                float ease = 1f - Mathf.Pow(1f - k, 2.2f);          // fast out, slow settle
                transform.localScale = Vector3.one * Mathf.Lerp(0.05f, _max, ease);
                if (_sr != null)
                {
                    var c = _sr.color;
                    c.a = Mathf.Pow(1f - k, 1.6f) * 0.45f;
                    _sr.color = c;
                }
                if (k >= 1f) Destroy(gameObject);
            }
        }
    }
}
