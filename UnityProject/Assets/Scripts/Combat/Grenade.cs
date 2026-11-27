using System.Collections.Generic;
using UnityEngine;

namespace Ares
{
    /// <summary>
    /// Hand-thrown grenade (feedback #8/#10): Q launches it toward the aim point. It has a real
    /// collision radius, bounces off walls, lands, burns briefly and detonates for 70% of a fuel
    /// barrel's damage.
    /// </summary>
    public class Grenade : MonoBehaviour
    {
        public static readonly List<Grenade> Active = new List<Grenade>(4);

        public static float DamageScale = 0.7f;   // barrel 110 -> grenade 77
        public static float Radius = 2.8f;

        // 80% of the old throw distance (was 5.2 units -> 4.16)
        public const float ThrowDistance = 4.16f;
        private const float FlySpeed = 8.0f;
        private const float ThrowRadius = 0.18f;
        private const float BounceDamping = 0.72f;

        private Vector2 _pos;
        private Vector2 _vel;
        private float _fly;
        private float _flyTime;
        private float _fuse = 0.4f;
        private bool _landed;
        private bool _boom;
        private float _boomTimer;
        private SpriteRenderer _sr;
        private SpriteRenderer _fx;
        private Sprite[] _frames;
        private static Sprite[] _boomFrames;

        public static Sprite[] BoomFrames
        {
            get
            {
                if (_boomFrames != null) return _boomFrames;
                var all = Resources.LoadAll<Sprite>("Art7/Boom");
                if (all == null || all.Length == 0) return null;
                System.Array.Sort(all, (a, b) => string.CompareOrdinal(a.name, b.name));
                _boomFrames = all;
                return _boomFrames;
            }
        }

        public static void Throw(Vector2 from, Vector2 aimDir, float distance)
        {
            var root = Game.EffectRoot;
            if (root == null) return;

            var go = new GameObject("Grenade");
            go.transform.SetParent(root, false);
            var g = go.AddComponent<Grenade>();
            g.Setup(from, aimDir, distance);
        }

        private void OnEnable() { if (!Active.Contains(this)) Active.Add(this); }
        private void OnDisable() { Active.Remove(this); }

        private void Setup(Vector2 from, Vector2 dir, float distance)
        {
            _pos = from;
            Vector2 d = dir.sqrMagnitude > 0.001f ? dir.normalized : Vector2.up;
            _vel = d * FlySpeed;
            _fly = 0f;
            _flyTime = Mathf.Max(0.15f, distance / FlySpeed);
            _landed = false;
            _boom = false;

            _sr = gameObject.AddComponent<SpriteRenderer>();
            _sr.sortingOrder = 40;
            transform.localScale = Vector3.one * 0.22f;   // sprite is 2.3 world units otherwise

            var frames = Resources.LoadAll<Sprite>("Art7/Grenade");
            if (frames != null && frames.Length > 0)
            {
                System.Array.Sort(frames, (a, b) => string.CompareOrdinal(a.name, b.name));
                _frames = frames;
                _sr.sprite = frames[0];
            }
            transform.position = new Vector3(_pos.x, _pos.y, 0f);
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            if (_boom)
            {
                _boomTimer -= dt;
                float t = 1f - Mathf.Clamp01(_boomTimer / 0.85f);
                var frames = BoomFrames;
                if (_fx != null && frames != null)
                {
                    int idx = Mathf.Clamp(Mathf.FloorToInt(t * frames.Length), 0, frames.Length - 1);
                    _fx.sprite = frames[idx];
                    float s = Mathf.Lerp(3.0f, 4.6f, t);
                    _fx.transform.localScale = new Vector3(s, s, 1f);
                    var c = _fx.color;
                    c.a = t < 0.7f ? 1f : Mathf.Lerp(1f, 0f, (t - 0.7f) / 0.3f);
                    _fx.color = c;
                }
                if (_boomTimer <= 0f) Destroy(gameObject);
                return;
            }

            if (!_landed)
            {
                _fly += dt;
                StepFlight(dt);

                // visual hop on top of the flat trajectory
                float k = Mathf.Clamp01(_fly / _flyTime);
                Vector2 draw = _pos;
                draw.y += Mathf.Sin(k * Mathf.PI) * 0.55f;
                transform.position = new Vector3(draw.x, draw.y, 0f);
                transform.Rotate(0f, 0f, -720f * dt);

                if (_sr != null && _frames != null)
                    _sr.sprite = _frames[Mathf.Min(_frames.Length - 1, Mathf.FloorToInt(k * 4f))];

                if (_fly >= _flyTime)
                {
                    _landed = true;
                    transform.position = new Vector3(_pos.x, _pos.y, 0f);
                    transform.rotation = Quaternion.identity;
                    AudioManager.PlaySfx("sfx_hit", 0.45f, 0.15f);
                }
                return;
            }

            _fuse -= dt;
            if (_sr != null)
            {
                float blink = 0.5f + 0.5f * Mathf.Sin(Time.time * 40f);
                _sr.color = Color.Lerp(Color.white, new Color(1f, 0.4f, 0.3f), blink);
            }
            if (_fuse <= 0f) Detonate();
        }

        /// <summary>Moves with a swept circle and reflects off walls / the arena border.</summary>
        private void StepFlight(float dt)
        {
            float dist = _vel.magnitude * dt;
            if (dist < 1e-5f) return;

            Vector2 dir = _vel / _vel.magnitude;
            int mask = Motion.WallMask;
            RaycastHit2D hit = Physics2D.CircleCast(_pos, ThrowRadius, dir, dist, mask);

            if (hit.collider != null)
            {
                _pos = hit.point + hit.normal * (ThrowRadius + 0.02f);
                _vel = Vector2.Reflect(_vel, hit.normal) * BounceDamping;

                if (Game.Combat != null)
                    Game.Combat.SpawnImpact(hit.point, new Color(1f, 0.8f, 0.4f), 0.45f);
                AudioManager.PlaySfx("sfx_hit", 0.4f, 0.25f);
            }
            else
            {
                _pos += dir * dist;
            }

            // arena border behaves like a wall
            var m = Game.Manager;
            if (m != null)
            {
                Vector2 min = m.ArenaMin + Vector2.one * ThrowRadius;
                Vector2 max = m.ArenaMax - Vector2.one * ThrowRadius;
                if (_pos.x < min.x) { _pos.x = min.x; _vel.x = Mathf.Abs(_vel.x) * BounceDamping; }
                if (_pos.x > max.x) { _pos.x = max.x; _vel.x = -Mathf.Abs(_vel.x) * BounceDamping; }
                if (_pos.y < min.y) { _pos.y = min.y; _vel.y = Mathf.Abs(_vel.y) * BounceDamping; }
                if (_pos.y > max.y) { _pos.y = max.y; _vel.y = -Mathf.Abs(_vel.y) * BounceDamping; }
            }
        }

        private void Detonate()
        {
            _boom = true;
            _boomTimer = 0.85f;
            if (_sr != null) _sr.enabled = false;

            var fxGo = new GameObject("BoomFx");
            fxGo.transform.SetParent(transform, false);
            _fx = fxGo.AddComponent<SpriteRenderer>();
            _fx.sortingOrder = 70;
            var frames = BoomFrames;
            if (frames != null) _fx.sprite = frames[0];

            Vector2 center = transform.position;
            int dmg = Mathf.RoundToInt(110f * DamageScale);

            var sp = Game.Spawner;
            if (sp != null)
            {
                for (int i = sp.AliveCount - 1; i >= 0; i--)
                {
                    var e = sp.Alive[i];
                    if (e == null || !e.IsAlive) continue;
                    float d = Vector2.Distance(e.Position, center);
                    if (d > Radius) continue;
                    int amount = Mathf.RoundToInt(dmg * Mathf.Lerp(1f, 0.45f, d / Radius));
                    var info = DamageInfo.Simple(amount, center, 2.6f);
                    info.crit = true;
                    e.TakeDamage(info);
                    e.PlayHitPop((e.Position - center).normalized, 1.5f);
                    if (Game.Hud != null) Game.Hud.ShowDamageStyled(e.Position + Vector2.up * 0.5f, amount, HudUI.DmgExplosion);
                }
            }

            // grenades hurt the thrower as well, at 70% of the enemy damage
            var player = Game.Player;
            if (player != null && player.IsAlive)
            {
                float pd = Vector2.Distance(player.Position, center);
                if (pd <= Radius)
                {
                    int self = Mathf.RoundToInt(dmg * Mathf.Lerp(1f, 0.45f, pd / Radius) * 0.7f);
                    player.TakeDamage(DamageInfo.Simple(Mathf.Max(1, self), center, 3f));
                }
            }

            for (int i = 0; i < Barrel.Active.Count; i++)
            {
                var b = Barrel.Active[i];
                if (b == null || !b.gameObject.activeSelf) continue;
                if (Vector2.Distance(b.transform.position, center) <= Radius) b.TakeDamage(999, center);
            }

            AudioManager.PlaySfx("sfx_explosion", 0.9f, 0.08f);
            if (Game.Camera != null) Game.Camera.AddShake(0.5f);
            if (Game.Hud != null) Game.Hud.FlashDamage();
        }
    }
}
