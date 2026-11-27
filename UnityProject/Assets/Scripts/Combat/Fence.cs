using System.Collections.Generic;
using UnityEngine;

namespace Ares
{
    /// <summary>
    /// Barricade (feedback #8): crafted from wood, dropped with E, blocks movement on the wall
    /// layer and is clawed apart by nearby infected.
    /// </summary>
    public class Fence : MonoBehaviour
    {
        public static readonly List<Fence> Active = new List<Fence>(8);

        public int maxHp = 150;
        public float contactDps = 24f;
        public float blockRadius = 0.55f;

        private int _hp;
        private SpriteRenderer _sr;
        private CircleCollider2D _col;
        private float _fade;
        private bool _broken;
        private float _thumpTimer;

        public bool IsBroken { get { return _broken; } }

        private void OnEnable() { if (!Active.Contains(this)) Active.Add(this); }
        private void OnDisable() { Active.Remove(this); }

        public static bool Place(Vector2 pos)
        {
            int mask = Motion.WallMask;
            if (Physics2D.OverlapCircle(pos, 0.5f, mask) != null) return false;

            var root = Game.BarrelRoot;
            if (root == null) return false;

            var go = new GameObject("Fence");
            go.transform.SetParent(root, false);
            go.transform.position = new Vector3(pos.x, pos.y, 0f);

            int wallLayer = LayerMask.NameToLayer("Wall");
            if (wallLayer >= 0) go.layer = wallLayer;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = -150;
            var frames = Resources.LoadAll<Sprite>("Art7/Fence");
            if (frames != null && frames.Length > 0)
            {
                System.Array.Sort(frames, (a, b) => string.CompareOrdinal(a.name, b.name));
                sr.sprite = frames[0];
            }
            go.transform.localScale = new Vector3(0.85f, 0.85f, 1f);

            var col = go.AddComponent<CircleCollider2D>();
            col.radius = 0.55f / 0.85f;
            col.isTrigger = false;

            var f = go.AddComponent<Fence>();
            f._sr = sr;
            f._col = col;
            f._hp = f.maxHp;
            AudioManager.PlaySfx("sfx_click", 0.7f, 0.15f);
            return true;
        }

        private void Update()
        {
            if (_broken)
            {
                _fade -= Time.deltaTime;
                if (_sr != null)
                {
                    var c = _sr.color;
                    c.a = Mathf.Clamp01(_fade);
                    _sr.color = c;
                }
                if (_fade <= 0f) Destroy(gameObject);
                return;
            }

            var sp = Game.Spawner;
            if (sp == null) return;

            int attackers = 0;
            for (int i = 0; i < sp.AliveCount; i++)
            {
                var e = sp.Alive[i];
                if (e == null || !e.IsAlive) continue;
                if ((e.Position - (Vector2)transform.position).sqrMagnitude <= 1.5f * 1.5f) attackers++;
            }

            if (attackers > 0)
            {
                _hp -= Mathf.RoundToInt(contactDps * attackers * Time.deltaTime);
                _thumpTimer -= Time.deltaTime;
                if (_thumpTimer <= 0f)
                {
                    _thumpTimer = 0.55f;
                    AudioManager.PlaySfx("sfx_hit", 0.35f, 0.2f);
                    if (_sr != null) _sr.color = new Color(1f, 0.85f, 0.75f);
                }
                else if (_sr != null)
                {
                    _sr.color = Color.Lerp(_sr.color, Color.white, Time.deltaTime * 6f);
                }

                if (_hp <= 0) Break();
            }
            else if (_sr != null && _sr.color != Color.white)
            {
                _sr.color = Color.Lerp(_sr.color, Color.white, Time.deltaTime * 6f);
            }
        }

        private void Break()
        {
            _broken = true;
            _fade = 1.2f;
            if (_col != null) _col.enabled = false;

            var frames = Resources.LoadAll<Sprite>("Art7/Fence");
            if (frames != null && frames.Length > 0 && _sr != null)
            {
                System.Array.Sort(frames, (a, b) => string.CompareOrdinal(a.name, b.name));
                _sr.sprite = frames[frames.Length - 1];   // broken remnants
                _sr.sortingOrder = -140;
            }
            AudioManager.PlaySfx("sfx_hit", 0.6f, 0.1f);
            for (int i = 0; i < 3; i++)
                if (Game.Combat != null) Game.Combat.SpawnImpact(transform.position, new Color(0.6f, 0.45f, 0.28f), 0.6f);
        }

        public static void ClearAll()
        {
            for (int i = Active.Count - 1; i >= 0; i--)
                if (Active[i] != null) Destroy(Active[i].gameObject);
            Active.Clear();
        }
    }
}
