using System.Collections.Generic;
using UnityEngine;

namespace Ares
{
    /// <summary>
    /// Explosive fuel barrel (feedback #5 item 6): every wave drops a few on the floor, they
    /// take damage from any source, burn for a moment, then blow up for heavy area damage with
    /// the supplied explosion animation.
    /// </summary>
    public class Barrel : MonoBehaviour
    {
        public SpriteRenderer Renderer;

        public static readonly List<Barrel> Active = new List<Barrel>(8);

        private static Sprite[] _idle;
        private static Sprite[] _burn;
        private static Sprite[] _boom;
        private static bool _loaded;

        public float radius = 0.55f;
        public int hp = 30;
        public float blastRadius = 3.3f;
        public int blastDamage = 110;

        private float _ignite = -1f;      // <0 = intact
        private float _boomTimer = -1f;
        private float _animTime;
        private bool _exploded;
        private SpriteRenderer _fx;
        private Vector3 _baseScale = Vector3.one;

        public bool Armed { get { return _ignite >= 0f && !_exploded; } }
        public bool Dead { get { return _exploded && _boomTimer <= 0f; } }

        private static void LoadFrames()
        {
            if (_loaded) return;
            _loaded = true;
            var all = Resources.LoadAll<Sprite>("Hub/sheet_barrel");
            if (all == null || all.Length < 16) return;
            System.Array.Sort(all, (a, b) => string.CompareOrdinal(a.name, b.name));
            _idle = new Sprite[] { all[0], all[1], all[2], all[3] };
            _burn = new Sprite[] { all[4], all[5], all[6], all[7] };
            _boom = new Sprite[8];
            for (int i = 0; i < 8; i++) _boom[i] = all[8 + i];
        }

        private void OnEnable()
        {
            if (!Active.Contains(this)) Active.Add(this);
        }

        private void OnDisable()
        {
            Active.Remove(this);
        }

        public void Spawn(Vector2 pos)
        {
            LoadFrames();
            transform.position = new Vector3(pos.x, pos.y, 0f);
            transform.localScale = Vector3.one * 1.15f;
            _baseScale = transform.localScale;
            _ignite = -1f;
            _boomTimer = -1f;
            _exploded = false;
            _animTime = Random.Range(0f, 2f);
            hp = 30;

            if (Renderer != null)
            {
                Renderer.enabled = true;
                Renderer.color = Color.white;
                Renderer.sortingOrder = -200;
                if (_idle != null) Renderer.sprite = _idle[0];
            }
            if (_fx != null) _fx.enabled = false;
            gameObject.SetActive(true);
        }

        /// <summary>Any damage source can set it off; it burns briefly so the player can back off.</summary>
        public void TakeDamage(int amount, Vector2 from)
        {
            if (_exploded || _ignite >= 0f) return;
            hp -= amount;
            if (hp <= 0f || amount >= 30)
            {
                _ignite = 0.55f;
                _animTime = 0f;
            }
            else
            {
                _animTime = 0.12f;   // little spark flash
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            if (_boomTimer > 0f)
            {
                _boomTimer -= dt;
                float t = 1f - Mathf.Clamp01(_boomTimer / 0.75f);
                if (_fx != null && _boom != null)
                {
                    int idx = Mathf.Clamp(Mathf.FloorToInt(t * _boom.Length), 0, _boom.Length - 1);
                    _fx.sprite = _boom[idx];
                    float s = Mathf.Lerp(2.6f, 4.4f, t);
                    _fx.transform.localScale = new Vector3(s, s, 1f);
                    var c = _fx.color;
                    c.a = t < 0.55f ? 1f : Mathf.Lerp(1f, 0f, (t - 0.55f) / 0.45f);
                    _fx.color = c;
                }
                if (_boomTimer <= 0f) Despawn();
                return;
            }

            if (_ignite >= 0f)
            {
                _ignite -= dt;
                _animTime += dt;
                if (Renderer != null && _burn != null)
                {
                    int idx = Mathf.Clamp(Mathf.FloorToInt(_animTime * 10f), 0, _burn.Length - 1);
                    Renderer.sprite = _burn[idx];
                    // urgent red flicker while it is about to blow
                    float k = 0.5f + 0.5f * Mathf.Sin(Time.time * 38f);
                    Renderer.color = Color.Lerp(Color.white, new Color(1f, 0.55f, 0.35f), k * 0.7f);
                }
                if (_ignite <= 0f) Explode();
                return;
            }

            // idle flicker
            if (Renderer != null && _idle != null)
            {
                _animTime += dt;
                int idx = Mathf.FloorToInt(_animTime * 3f) % _idle.Length;
                Renderer.sprite = _idle[idx];
                Renderer.color = Color.white;
            }
        }

        private void Explode()
        {
            _exploded = true;
            _boomTimer = 0.75f;

            if (Renderer != null) Renderer.enabled = false;
            if (_fx == null)
            {
                var go = new GameObject("BoomFx");
                go.transform.SetParent(transform, false);
                _fx = go.AddComponent<SpriteRenderer>();
            }
            _fx.enabled = true;
            _fx.sortingOrder = 60;
            _fx.color = Color.white;
            if (_boom != null) _fx.sprite = _boom[0];

            Vector2 center = transform.position;

            // area damage with falloff
            var sp = Game.Spawner;
            if (sp != null)
            {
                for (int i = sp.AliveCount - 1; i >= 0; i--)
                {
                    var e = sp.Alive[i];
                    if (e == null || !e.IsAlive) continue;
                    float d = Vector2.Distance(e.Position, center);
                    if (d > blastRadius) continue;
                    float falloff = Mathf.Lerp(1f, 0.45f, d / blastRadius);
                    int dmg = Mathf.RoundToInt(blastDamage * falloff);
                    var info = DamageInfo.Simple(dmg, center, 3.2f);
                    info.crit = true;
                    e.TakeDamage(info);
                    e.PlayHitPop((e.Position - center).normalized, 1.6f);
                    if (Game.Hud != null) Game.Hud.ShowDamageStyled(e.Position + Vector2.up * 0.5f, dmg, HudUI.DmgExplosion);
                }
            }

            // the blast also hurts the player, at 70% of the enemy damage
            var player = Game.Player;
            if (player != null && player.IsAlive)
            {
                float pd = Vector2.Distance(player.Position, center);
                if (pd <= blastRadius)
                {
                    int self = Mathf.RoundToInt(blastDamage * Mathf.Lerp(1f, 0.45f, pd / blastRadius) * 0.7f);
                    player.TakeDamage(DamageInfo.Simple(Mathf.Max(1, self), center, 3f));
                }
            }

            // chain reaction
            for (int i = 0; i < Active.Count; i++)
            {
                var other = Active[i];
                if (other == null || other == this || other._exploded) continue;
                if (Vector2.Distance(other.transform.position, center) <= blastRadius * 0.9f)
                    other.TakeDamage(999, center);
            }

            AudioManager.PlaySfx("sfx_explosion", 1f, 0.06f);
            if (Game.Camera != null) Game.Camera.AddShake(0.62f);
            if (Game.Hud != null) { Game.Hud.FlashDamage(); Game.Hud.ShowPrompt("汽油桶爆炸！", 0.8f); }
        }

        private void Despawn()
        {
            gameObject.SetActive(false);
        }

        public static Barrel FindNear(Vector2 p, float r)
        {
            for (int i = 0; i < Active.Count; i++)
            {
                var b = Active[i];
                if (b == null || !b.gameObject.activeSelf || b._exploded) continue;
                float rr = r + b.radius;
                if ((Vector2.Distance(b.transform.position, p)) <= rr) return b;
            }
            return null;
        }

        public static void ClearAll()
        {
            for (int i = Active.Count - 1; i >= 0; i--)
                if (Active[i] != null) Active[i].gameObject.SetActive(false);
            Active.Clear();
        }

        // ------------------------------------------------------------------ spawning

        /// <summary>Drops barrels for a wave: a few more each wave, never more than five alive.</summary>
        public static void SpawnForWave(int wave, Transform poolRoot)
        {
            if (poolRoot == null) return;
            int want = Mathf.Clamp(1 + wave / 3, 1, 5);
            int alive = 0;
            for (int i = 0; i < Active.Count; i++)
                if (Active[i] != null && Active[i].gameObject.activeSelf && !Active[i]._exploded) alive++;
            want = Mathf.Min(want, 5 - alive);
            if (want <= 0) return;

            var frames = Resources.LoadAll<Sprite>("Hub/sheet_barrel");
            if (frames == null || frames.Length == 0) return;

            for (int i = 0; i < want; i++)
            {
                Vector2 pos;
                if (!TryFindSpot(out pos)) continue;

                var go = new GameObject("Barrel");
                go.transform.SetParent(poolRoot, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = frames[0];
                sr.sortingOrder = -200;
                var b = go.AddComponent<Barrel>();
                b.Renderer = sr;
                b.Spawn(pos);
            }
        }

        private static bool TryFindSpot(out Vector2 pos)
        {
            pos = Vector2.zero;
            var sp = Game.Spawner;
            if (sp == null) return false;

            for (int attempt = 0; attempt < 30; attempt++)
            {
                Vector2 candidate;
                if (!sp.TryPickFreePoint(out candidate, 2.6f, 0.6f)) return false;
                bool occupied = false;
                for (int i = 0; i < Active.Count; i++)
                {
                    if (Active[i] == null || !Active[i].gameObject.activeSelf) continue;
                    if (Vector2.Distance(Active[i].transform.position, candidate) < 1.8f) { occupied = true; break; }
                }
                if (occupied) continue;
                pos = candidate;
                return true;
            }
            return false;
        }
    }
}
