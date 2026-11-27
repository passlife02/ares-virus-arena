using UnityEngine;

namespace Ares
{
    /// <summary>
    /// Pooled projectile with a swept-circle wall check and a coloured tracer trail, so the
    /// shot is readable at a glance (feedback: "子弹飞出轨迹不明显").
    /// </summary>
    public class Projectile : MonoBehaviour
    {
        public SpriteRenderer Renderer;

        private CombatSystem _system;
        private Vector2 _pos;
        private Vector2 _dir;
        private float _speed;
        private float _life;
        private float _radius;
        private DamageInfo _damage;
        private bool _fromPlayer;
        private bool _active;
        private TrailRenderer _trail;
        private Color _trailColor = Color.white;

        public bool FromPlayer { get { return _fromPlayer; } }
        public bool Active { get { return _active; } }

        private void EnsureTrail()
        {
            if (_trail != null) return;
            _trail = gameObject.GetComponent<TrailRenderer>();
            if (_trail == null) _trail = gameObject.AddComponent<TrailRenderer>();
            _trail.material = CombatSystem.TrailMaterial;
            _trail.time = 0.24f;
            _trail.minVertexDistance = 0.05f;
            _trail.numCapVertices = 3;
            _trail.textureMode = LineTextureMode.Stretch;
            _trail.widthMultiplier = 0.26f;
            _trail.widthCurve = AnimationCurve.EaseInOut(0f, 0.55f, 1f, 1f);
        }

        public void Launch(CombatSystem system, Vector2 pos, Vector2 dir, float speed, float life,
                           float radius, DamageInfo damage, bool fromPlayer, Color trailColor)
        {
            _system = system;
            _pos = pos;
            _dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.right;
            _speed = speed;
            _life = life;
            _radius = radius;
            _damage = damage;
            _fromPlayer = fromPlayer;
            _active = true;
            _trailColor = trailColor;

            transform.position = new Vector3(_pos.x, _pos.y, 0f);
            float ang = Mathf.Atan2(_dir.y, _dir.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, ang);
            if (Renderer != null)
            {
                Renderer.enabled = true;
                Renderer.color = trailColor.a > 0.01f ? Color.Lerp(Color.white, trailColor, 0.45f) : Color.white;
            }

            EnsureTrail();
            if (_trail != null)
            {
                _trail.enabled = true;
                _trail.Clear();
                _trail.startColor = trailColor;
                _trail.endColor = new Color(trailColor.r, trailColor.g, trailColor.b, 0f);
                _trail.widthMultiplier = fromPlayer ? 0.30f : 0.22f;
            }
        }

        private void Update()
        {
            if (!_active) return;
            float dt = Time.deltaTime;
            _life -= dt;
            if (_life <= 0f)
            {
                _system.ReleaseProjectile(this, false, _pos, Vector2.zero);
                return;
            }

            const int steps = 2;
            float stepDist = _speed * dt / steps;
            for (int s = 0; s < steps; s++)
            {
                RaycastHit2D wall = Physics2D.CircleCast(_pos, _radius, _dir, stepDist, Motion.WallMask);
                if (wall.collider != null && wall.distance <= stepDist)
                {
                    Vector2 hitPos = _pos + _dir * Mathf.Max(0f, wall.distance - 0.01f);
                    _system.ReleaseProjectile(this, true, hitPos, wall.normal);
                    return;
                }

                Vector2 next = _pos + _dir * stepDist;

                if (_fromPlayer)
                {
                    var barrel = Barrel.FindNear(next, _radius + 0.35f);
                    if (barrel != null)
                    {
                        barrel.TakeDamage(_damage.amount, _pos);
                        _system.ReleaseProjectile(this, true, next, -_dir);
                        return;
                    }

                    // eggs are shootable too, and stop the bullet like a body would
                    {
                        var eggs = UnityEngine.Object.FindObjectsOfType<Ares.SpiderEgg>();
                        for (int gi = 0; gi < eggs.Length; gi++)
                        {
                            var egg = eggs[gi];
                            if (egg == null) continue;
                            if (UnityEngine.Vector2.Distance(egg.transform.position, next) > _radius + 0.55f) continue;
                            egg.TakeHit(Mathf.Max(1, _damage.amount));
                            _system.ReleaseProjectile(this, true, next, -_dir);
                            return;
                        }
                    }

                    EnemyBase target = _system.FindEnemyHit(next, _radius + 0.42f);
                    if (target != null)
                    {
                        var info = _damage;
                        info.fromPosition = _pos;
                        target.TakeDamage(info);

                        if (info.explosive)
                            _system.ExplodeAt(next, info.explodeRadius, Mathf.RoundToInt(info.amount * info.explodeMul), target);

                        if (info.pierce > 0)
                        {
                            info.pierce--;
                            _damage = info;       // keep flying, fewer targets left
                            _system.SpawnImpact(next, new Color(1f, 0.75f, 0.35f), 0.5f);
                        }
                        else
                        {
                            _system.ReleaseProjectile(this, true, next, -_dir);
                            return;
                        }
                    }
                }
                else
                {
                    var player = Game.Player;
                    if (player != null && player.IsAlive)
                    {
                        float rr = _radius + Game.Cfg.playerRadius;
                        if ((player.Position - next).sqrMagnitude <= rr * rr)
                        {
                            var info = _damage;
                            info.fromPosition = _pos;
                            player.TakeDamage(info);
                            _system.ReleaseProjectile(this, true, next, -_dir);
                            return;
                        }
                    }
                }

                _pos = next;
                if (!Game.Manager.IsInsideArena(_pos))
                {
                    _system.ReleaseProjectile(this, true, _pos, -_dir);
                    return;
                }
            }

            transform.position = new Vector3(_pos.x, _pos.y, 0f);
            float ang = Mathf.Atan2(_dir.y, _dir.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, ang);
        }

        public void ClearTrail()
        {
            if (_trail != null)
            {
                _trail.Clear();
                _trail.enabled = false;
            }
        }
    }
}
