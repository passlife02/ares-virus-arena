using System.Collections.Generic;
using UnityEngine;

namespace Ares
{
    public enum MapVariant
    {
        Ruins = 0,
        CentreLake = 1,
        Archipelago = 2,
        Crossroads = 3,
        Ring = 4,
    }

    /// <summary>Shared water material (custom shader with UV wobble); created once.</summary>
    public static class WaterMaterial
    {
        private static Material _mat;

        public static Material Get()
        {
            if (_mat != null) return _mat;
            var sh = Shader.Find("Ares/WaterSurface");
            if (sh == null) return null;

            _mat = new Material(sh);
            var wt = Resources.Load<Sprite>("Map/water_tile");
            if (wt != null) _mat.mainTexture = wt.texture;
            var noise = Resources.Load<Texture2D>("Weather/water_noise");
            if (noise != null) _mat.SetTexture("_NoiseTex", noise);
            _mat.SetColor("_Color", new Color(0.92f, 0.97f, 1f, 1f));
            _mat.SetFloat("_Distort", 0.055f);
            _mat.SetFloat("_ScrollX", 0.008f);
            _mat.SetFloat("_ScrollY", 0.03f);
            _mat.SetFloat("_Shimmer", 0.95f);
            return _mat;
        }
    }

    /// <summary>
    /// Five arena layouts, built once at boot and toggled by activation. Switching therefore costs
    /// nothing but a nav rebake — no rebuilding, no deferred colliders, no stutter.
    /// </summary>
    public static class MapBuilder
    {
        public static readonly string[] Names = new string[]
        {
            "废弃街区", "中心湖", "群岛", "十字走廊", "环形堡垒",
        };

        private class Entry
        {
            public MapVariant Variant;
            public GameObject Root;
            public List<Rect> Walls = new List<Rect>();
            public Vector2 Min;
            public Vector2 Max;
            public Vector2 Start;
            public WaterSurface Water;
        }

        private static readonly List<Entry> _entries = new List<Entry>();
        private static Sprite _strip;
        private static Sprite _block;
        private static Sprite _water;
        private static bool _built;
        private static MapVariant _current = (MapVariant)(-1);
        private static List<Rect> _ruinsRecipe;

        public static bool Built { get { return _built; } }

        public static MapVariant Next(MapVariant current)
        {
            return (MapVariant)(((int)current + 1) % Names.Length);
        }

        // ------------------------------------------------------------------ build

        /// <summary>Builds every layout once. Call from Boot after the scene's walls exist.</summary>
        public static void BuildAll(GameManager mgr)
        {
            if (_built || mgr == null || mgr.Arena == null) return;

            CaptureSprites();
            _ruinsRecipe = new List<Rect>(mgr.Arena.Walls);

            var original = GameObject.Find("Walls");
            if (original != null) original.SetActive(false);   // replaced by the per-map roots

            var container = new GameObject("Maps");
            container.transform.SetParent(mgr.transform, false);

            for (int i = 0; i < Names.Length; i++)
            {
                var v = (MapVariant)i;
                var e = new Entry { Variant = v };
                e.Root = new GameObject("Map_" + Names[i]);
                e.Root.transform.SetParent(container.transform, false);

                List<Rect> rects = v == MapVariant.Ruins ? new List<Rect>(_ruinsRecipe) : BuildRects(v, out e.Min, out e.Max, out e.Start);
                if (v == MapVariant.Ruins)
                {
                    e.Min = new Vector2(-22f, -13f);
                    e.Max = new Vector2(22f, 13f);
                    e.Start = new Vector2(-0.5f, -0.5f);
                }

                e.Walls.AddRange(rects);

                var waterRects = new List<Rect>();
                if (v == MapVariant.CentreLake || v == MapVariant.Archipelago)
                {
                    int waterCount = v == MapVariant.CentreLake ? Mathf.Min(rects.Count, 8) : Mathf.Min(rects.Count, 4);
                    for (int w = 0; w < waterCount; w++) waterRects.Add(rects[w]);
                }
                e.Water = WaterSurface.Attach(e.Root.transform, waterRects);

                for (int r = 0; r < rects.Count; r++)
                {
                    bool isWater = waterRects.Contains(rects[r]);
                    bool skipArt = v == MapVariant.CentreLake && isWater;
                    BuildWall(e.Root.transform, rects[r], v, e.Water, skipArt, isWater);
                }

                if (v == MapVariant.CentreLake) BuildPond(e.Root.transform);

                e.Root.SetActive(false);
                _entries.Add(e);
            }

            _built = true;
        }

        private static void BuildPond(Transform parent)
        {
            var sprite = Resources.Load<Sprite>("Map/lake_pond");
            if (sprite == null) return;
            var go = new GameObject("LakePond");
            go.transform.SetParent(parent, false);
            go.transform.position = Vector3.zero;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = -169;
            float scale = 15.4f / Mathf.Max(0.01f, sprite.bounds.size.x);
            go.transform.localScale = new Vector3(scale, scale, 1f);
        }

        // ------------------------------------------------------------------ switch

        /// <summary>Turns one map on and the rest off, then rebakes navigation.</summary>
        public static void Show(MapVariant variant, GameManager mgr)
        {
            if (mgr == null || mgr.Arena == null) return;
            if (!_built) BuildAll(mgr);

            Entry target = null;
            for (int i = 0; i < _entries.Count; i++)
            {
                var e = _entries[i];
                bool on = e.Variant == variant;
                if (e.Root.activeSelf != on) e.Root.SetActive(on);
                if (on) target = e;
            }
            if (target == null) return;

            if (_current == variant) return;
            _current = variant;

            var layout = mgr.Arena;
            layout.Walls.Clear();
            layout.Walls.AddRange(target.Walls);
            layout.BoundsMin = target.Min;
            layout.BoundsMax = target.Max;
            layout.PlayerStart = target.Start;

            WaterSurface.SetCurrent(target.Water);
            mgr.RebuildNavigation();
        }

        // ------------------------------------------------------------------ recipes

        private static List<Rect> BuildRects(MapVariant v, out Vector2 min, out Vector2 max, out Vector2 start)
        {
            var rects = new List<Rect>();
            min = new Vector2(-22f, -13f);
            max = new Vector2(22f, 13f);
            start = new Vector2(-0.5f, -0.5f);

            switch (v)
            {
                case MapVariant.CentreLake:
                {
                    const float lakeR = 6.8f;
                    int bands = 8;
                    float bandH = lakeR * 2f / bands;
                    for (int b = 0; b < bands; b++)
                    {
                        float y0 = -lakeR + b * bandH;
                        float yc = y0 + bandH * 0.5f;
                        float halfW = Mathf.Sqrt(Mathf.Max(0.01f, lakeR * lakeR - yc * yc));
                        rects.Add(new Rect(-halfW, y0, halfW * 2f, bandH));
                    }
                    rects.Add(new Rect(-18f, 7f, 3f, 1.2f));
                    rects.Add(new Rect(15f, 7f, 3f, 1.2f));
                    rects.Add(new Rect(-18f, -8.2f, 3f, 1.2f));
                    rects.Add(new Rect(15f, -8.2f, 3f, 1.2f));
                    start = new Vector2(0f, -10.5f);
                    break;
                }

                case MapVariant.Archipelago:
                {
                    min = new Vector2(-27f, -15f);
                    max = new Vector2(27f, 15f);
                    rects.Add(new Rect(-9.5f, -15f, 1.6f, 11f));
                    rects.Add(new Rect(-9.5f, 6.6f, 1.6f, 8.4f));
                    rects.Add(new Rect(8.4f, -15f, 1.6f, 9f));
                    rects.Add(new Rect(8.4f, 4.6f, 1.6f, 10.4f));
                    rects.Add(new Rect(-20f, 4f, 3f, 1.2f));
                    rects.Add(new Rect(16f, -6f, 3f, 1.2f));
                    rects.Add(new Rect(-2.5f, 2f, 2.4f, 1.2f));
                    start = Vector2.zero;
                    break;
                }

                case MapVariant.Crossroads:
                {
                    // central hall with a clear gap in the middle of every wall (four passages)
                    // top wall: two stubs with a 6 m gap
                    rects.Add(new Rect(-9f, 4f, 5.4f, 1.2f));
                    rects.Add(new Rect(3.6f, 4f, 5.4f, 1.2f));
                    // bottom wall
                    rects.Add(new Rect(-9f, -5.2f, 5.4f, 1.2f));
                    rects.Add(new Rect(3.6f, -5.2f, 5.4f, 1.2f));
                    // left wall
                    rects.Add(new Rect(-9f, -4f, 1.2f, 2.6f));
                    rects.Add(new Rect(-9f, 1.4f, 1.2f, 2.6f));
                    // right wall
                    rects.Add(new Rect(7.8f, -4f, 1.2f, 2.6f));
                    rects.Add(new Rect(7.8f, 1.4f, 1.2f, 2.6f));
                    // four corridor arms running out from the gaps
                    rects.Add(new Rect(-20f, -2.2f, 9f, 1.2f));
                    rects.Add(new Rect(-20f, 1.0f, 9f, 1.2f));
                    rects.Add(new Rect(11f, -2.2f, 9f, 1.2f));
                    rects.Add(new Rect(11f, 1.0f, 9f, 1.2f));
                    rects.Add(new Rect(-2.2f, -12.4f, 1.2f, 6.4f));
                    rects.Add(new Rect(1.0f, -12.4f, 1.2f, 6.4f));
                    rects.Add(new Rect(-2.2f, 6.0f, 1.2f, 6.4f));
                    rects.Add(new Rect(1.0f, 6.0f, 1.2f, 6.4f));
                    start = Vector2.zero;
                    break;
                }

                case MapVariant.Ring:
                {
                    rects.Add(new Rect(-12f, 3.2f, 8.6f, 1.2f));
                    rects.Add(new Rect(3.4f, 3.2f, 8.6f, 1.2f));
                    rects.Add(new Rect(-12f, -4.4f, 8.6f, 1.2f));
                    rects.Add(new Rect(3.4f, -4.4f, 8.6f, 1.2f));
                    rects.Add(new Rect(-12f, -4.4f, 1.2f, 3.4f));
                    rects.Add(new Rect(10.8f, -4.4f, 1.2f, 7.6f));
                    rects.Add(new Rect(-12f, 3.2f, 1.2f, 3.4f));
                    rects.Add(new Rect(-19f, 6f, 3f, 1.2f));
                    rects.Add(new Rect(16f, 6f, 3f, 1.2f));
                    rects.Add(new Rect(-19f, -7.2f, 3f, 1.2f));
                    rects.Add(new Rect(16f, -7.2f, 3f, 1.2f));
                    start = new Vector2(0f, -9.5f);
                    break;
                }
            }

            return rects;
        }

        // ------------------------------------------------------------------ pieces

        private static void CaptureSprites()
        {
            if (_strip != null) return;
            var walls = GameObject.Find("Walls");
            if (walls == null) return;
            foreach (var sr in walls.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (sr.sprite == null) continue;
                if (sr.name.StartsWith("WallShadow")) continue;
                if (_strip == null) { _strip = sr.sprite; continue; }
                if (_block == null && sr.sprite != _strip) { _block = sr.sprite; break; }
            }
        }

        private static void BuildWall(Transform parent, Rect r, MapVariant variant, WaterSurface surface,
                                      bool skipArt, bool water)
        {
            float cx = r.x + r.width * 0.5f;
            float cy = r.y + r.height * 0.5f;

            int wallLayer = LayerMask.NameToLayer("Wall");
            if (wallLayer < 0) wallLayer = 0;

            var colGo = new GameObject("WallCol");
            colGo.layer = wallLayer;
            colGo.transform.SetParent(parent, false);
            colGo.transform.position = new Vector3(cx, cy, 0f);
            var box = colGo.AddComponent<BoxCollider2D>();
            box.size = new Vector2(r.width, r.height);

            if (skipArt) return;

            if (water && _water == null) _water = Resources.Load<Sprite>("Map/water_tile");

            var sh = new GameObject("WallShadow");
            sh.transform.SetParent(parent, false);
            var shr = sh.AddComponent<SpriteRenderer>();
            shr.sprite = _block;
            shr.drawMode = SpriteDrawMode.Sliced;
            shr.size = new Vector2(r.width + 0.18f, r.height + 0.18f);
            shr.color = new Color(0f, 0f, 0f, 0.32f);
            shr.sortingOrder = -680;
            sh.transform.position = new Vector3(cx, cy - 0.12f, 0f);

            const float chunk = 1.9f;
            int nx = Mathf.Max(1, Mathf.RoundToInt(r.width / chunk));
            int ny = Mathf.Max(1, Mathf.RoundToInt(r.height / chunk));
            float cw = r.width / nx;
            float chh = r.height / ny;

            for (int iy = 0; iy < ny; iy++)
            {
                for (int ix = 0; ix < nx; ix++)
                {
                    var piece = new GameObject("WallArt");
                    piece.transform.SetParent(parent, false);
                    var sr2 = piece.AddComponent<SpriteRenderer>();
                    Sprite sp = water && _water != null ? _water : (_strip != null ? _strip : _block);
                    sr2.sprite = sp;
                    if (water && _water != null) sr2.sharedMaterial = WaterMaterial.Get();
                    sr2.drawMode = SpriteDrawMode.Tiled;
                    sr2.size = new Vector2(cw + 0.05f, chh + 0.05f);
                    sr2.sortingOrder = -670;
                    float shade = 0.66f + 0.06f * ((ix * 7 + iy * 3) % 3);
                    sr2.color = water
                        ? new Color(0.52f * shade + 0.18f, 0.62f * shade + 0.2f, 0.74f * shade + 0.24f, 1f)
                        : new Color(shade, shade * 1.02f, shade * 1.06f, 1f);
                    piece.transform.position = new Vector3(r.x + cw * (ix + 0.5f), r.y + chh * (iy + 0.5f), 0f);
                    if (water && surface != null) surface.Register(sr2);
                }
            }
        }
    }
}
