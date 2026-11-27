using UnityEngine;

namespace Ares
{
    /// <summary>
    /// Shared enemy brain: flow-field steering with local wall avoidance, boid separation,
    /// stuck recovery, damage/death and a corpse fade that matches the feedback (#12: ~1s).
    /// </summary>
    public abstract class EnemyBase : MonoBehaviour, IDamageable
    {
        [Header("Wired by the prefab builder")]
        public EnemyKind Kind;
        public SpriteRenderer Body;
        public Animator Animator;
        public Transform VisualRoot;
        public SpriteRenderer BossRing;
        public bool SimpleVisual;

        protected Vector2 _pos;
        protected Vector2 _vel;
        protected Vector2 _desiredDir;
        protected Vector2 _separation;
        protected float _speed;
        protected float _radius;
        protected int _hp;
        protected int _maxHp;
        protected int _damage;
        protected bool _alive = true;
        protected float _hurtTimer;
        protected float _thinkTimer;
        protected float _deadTimer;
        protected float _spawnAnimTimer;
        protected string _stateName = "";
        protected float _facing = 1f;
        protected bool IsBoss;
        protected float _sizeScale = 1f;
        protected float _popTimer;
        protected Vector2 _popOffset;
        private Vector3 _visualBasePos;
        private bool _visualBaseCaptured;
        private Vector3 _visualBaseScale = Vector3.one;
        protected float BodyBaseScale = 1f;

        private float _stuckTimer;

        // hit lift: the body pops up a few pixels and settles back (impact feel)
        private float _lift;
        private float _liftVel;

        // long-wander watch: a monster that circles the same patch is walked through the
        // arena centre so it re-acquires a clean line to the player instead of grinding a corner
        private Vector2 _roamAnchor;
        private float _roamSample;
        private float _roamStill;
        private float _redirectTimer;
        private float _detourTimer;
        private float _followTimer;
        private Vector2 _followTarget;
        private Vector2 _detourDir;
        private Color[] _baseColors;
        private SpriteRenderer[] _cachedRenderers;
        private float _ringPhase;

        public bool IsAlive { get { return _alive; } }
        public Vector2 Position { get { return _pos; } }
        public float Radius { get { return _radius; } }
        public EnemyKind EnemyKind { get { return Kind; } }
        public float Hp01 { get { return _maxHp <= 0 ? 0f : Mathf.Clamp01((float)_hp / _maxHp); } }
        public int MaxHp { get { return _maxHp; } }

        // ------------------------------------------------------------------ setup

        public virtual void Setup(Vector2 position, float hpScale, float dmgScale, float sizeScale)
        {
            var cfg = Game.Cfg;
            _pos = position;
            _vel = Vector2.zero;
            _separation = Vector2.zero;
            _deadTimer = 0f;
            _hurtTimer = 0f;
            _alive = true;
            _spawnAnimTimer = 0.3f;
            _thinkTimer = Random.Range(0f, 0.12f);
            _stuckTimer = 0f;
            _roamAnchor = _pos;
            _roamStill = 0f;
            _redirectTimer = 0f;
            _detourTimer = 0f;
            _popTimer = 0f;
            _popOffset = Vector2.zero;
            _visualBaseCaptured = false;
            _sizeScale = sizeScale;

            transform.position = new Vector3(_pos.x, _pos.y, 0f);
            transform.localScale = Vector3.one;
            gameObject.SetActive(true);

            if (Body != null && BodyBaseScale <= 0.001f) BodyBaseScale = Mathf.Max(0.01f, Mathf.Abs(Body.transform.localScale.x));
            if (VisualRoot != null && !_visualBaseCaptured)
            {
                _visualBaseScale = VisualRoot.localScale;
                _visualBaseCaptured = true;
            }
            if (_cachedRenderers == null || _cachedRenderers.Length == 0)
                _cachedRenderers = GetComponentsInChildren<SpriteRenderer>(true);
            if (_baseColors == null || _baseColors.Length != _cachedRenderers.Length)
            {
                _baseColors = new Color[_cachedRenderers.Length];
                for (int i = 0; i < _cachedRenderers.Length; i++)
                    _baseColors[i] = _cachedRenderers[i] != null ? _cachedRenderers[i].color : Color.white;
            }
            for (int i = 0; i < _cachedRenderers.Length; i++)
            {
                if (_cachedRenderers[i] == null) continue;
                _cachedRenderers[i].color = _baseColors[i];
                _cachedRenderers[i].enabled = true;
            }

            if (BossRing != null) BossRing.enabled = IsBoss;
            _hp = _maxHp;
            _facing = 1f;
        }

        // ------------------------------------------------------------------ loop

        protected virtual void Update()
        {
            float dt = Time.deltaTime;

            if (!_alive)
            {
                _deadTimer -= dt;
                FadeCorpse();
                if (_deadTimer <= 0f) Despawn();
                return;
            }

            if (_spawnAnimTimer > 0f) _spawnAnimTimer -= dt;
            if (_hurtTimer > 0f) _hurtTimer -= dt;

            bool combatAllowed = Game.CanPlay;
            if (!combatAllowed)
            {
                _vel = Vector2.zero;
                return;
            }

            _thinkTimer -= dt;
            if (_thinkTimer <= 0f)
            {
                _thinkTimer = Game.Spawner.ThinkInterval;
                Think(dt);
            }

            Steer(dt);
            UpdateVisual();
            UpdateBossRing();
        }

        protected abstract void Think(float dt);

        private void TickRoamWatch(float dt, ref Vector2 want)
        {
            if (_redirectTimer > 0f)
            {
                var mgr = Game.Manager;
                Vector2 centre = mgr != null
                    ? (mgr.ArenaMin + mgr.ArenaMax) * 0.5f
                    : Vector2.zero;

                _redirectTimer -= dt;
                Vector2 toCentre = centre - _pos;
                if (toCentre.sqrMagnitude < 1.6f || _redirectTimer <= 0f)
                {
                    _redirectTimer = 0f;
                    _roamStill = 0f;
                    _roamAnchor = _pos;
                }
                else
                {
                    Vector2 dir = toCentre.normalized;
                    want = Motion.WallAvoidance(_pos, dir, _radius, Motion.WallMask);
                    return;
                }
            }

            _roamSample += dt;
            if (_roamSample < 0.5f) return;
            _roamSample = 0f;

            if (Vector2.Distance(_pos, _roamAnchor) < 0.9f)
                _roamStill += 0.5f;
            else
            {
                _roamAnchor = _pos;
                _roamStill = 0f;
            }

            if (_roamStill >= 3.5f)
            {
                _redirectTimer = 3.5f;
                _roamStill = 0f;
            }
        }

        protected void Steer(float dt)
        {
            // Blend seek with the boid push: the stronger the crowd, the less we push in.
            Vector2 seek = _desiredDir;
            float sepMag = _separation.magnitude;
            if (sepMag > 0.9f) seek *= Mathf.Clamp01(1f - (sepMag - 0.9f) * 0.45f);

            Vector2 want = seek + _separation;
            if (want.sqrMagnitude > 0.0001f) want.Normalize();

            // Detour when wedged against geometry for too long.
            if (_detourTimer > 0f)
            {
                _detourTimer -= dt;
                want = _detourDir;
            }

            int mask = Motion.WallMask;
            if (want.sqrMagnitude > 0.0001f && _detourTimer <= 0f)
                want = Motion.WallAvoidance(_pos, want, _radius, mask);

            TickRoamWatch(dt, ref want);

            Vector2 targetVel = want * _speed;
            _vel = Vector2.MoveTowards(_vel, targetVel, 22f * dt);

            Vector2 delta = _vel * dt;
            bool hitWall;
            Vector2 next = Motion.MoveCircle(_pos, delta, _radius, mask, out hitWall);
            if (hitWall)
            {
                Vector2 actual = next - _pos;
                if (actual.sqrMagnitude > 1e-6f)
                {
                    float along = Vector2.Dot(_vel, actual.normalized);
                    if (along < 0f) _vel -= actual.normalized * along;
                }
                _stuckTimer += dt;
            }
            else
            {
                _stuckTimer = Mathf.Max(0f, _stuckTimer - dt * 2f);
            }
            _pos = next;
            _pos = Motion.Depenetrate(_pos, _radius, mask);

            // Stuck recovery: sidestep in a random tangent direction for a moment.
            if (_stuckTimer > 0.45f && _detourTimer <= 0f)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                _detourDir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                _detourTimer = 0.35f;
                _stuckTimer = 0f;
            }

            // If something keeps blocking us for ~2 seconds, follow a neighbour that is moving
            // freely instead of grinding against the same corner (feedback #9 item 2).
            if (_stuckTimer > 2f && _followTimer <= 0f)
            {
                var mate = FindFreeNeighbour();
                if (mate != null)
                {
                    _followTimer = 1.6f;
                    _followTarget = mate.Position;
                    _stuckTimer = 0f;
                }
            }

            var m = Game.Manager;
            if (_followTimer > 0f) _followTimer -= dt;
            if (m != null)
            {
                _pos.x = Mathf.Clamp(_pos.x, m.ArenaMin.x + _radius, m.ArenaMax.x - _radius);
                _pos.y = Mathf.Clamp(_pos.y, m.ArenaMin.y + _radius, m.ArenaMax.y - _radius);
            }
            transform.position = new Vector3(_pos.x, _pos.y, 0f);
        }

        protected void ComputeSeparation()
        {
            _separation = Vector2.zero;
            var spawner = Game.Spawner;
            if (spawner == null) return;
            var neighbours = spawner.NeighbourBuffer;
            spawner.Hash.Query(_pos, neighbours);

            float wanted = _radius * 3.1f;
            int count = 0;
            for (int i = 0; i < neighbours.Count; i++)
            {
                var other = neighbours[i];
                if (other == null || other == this || !other.IsAlive) continue;
                Vector2 d = _pos - other.Position;
                float distSq = d.sqrMagnitude;
                if (distSq > wanted * wanted) continue;
                if (distSq < 1e-6f)
                {
                    float a = (GetInstanceID() % 628) * 0.01f;
                    _separation += new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.6f;
                    count++;
                    continue;
                }
                float dist = Mathf.Sqrt(distSq);
                _separation += (d / dist) * ((wanted - dist) / wanted);
                count++;
                if (count >= 8) break;
            }
            if (count > 0) _separation *= 2.6f;
        }

        protected Vector2 ToPlayer()
        {
            var p = Game.Player;
            return p == null ? Vector2.zero : p.Position - _pos;
        }

        protected float DistToPlayer()
        {
            var p = Game.Player;
            return p == null ? 9999f : (p.Position - _pos).magnitude;
        }

        private EnemyBase FindFreeNeighbour()
        {
            var sp = Game.Spawner;
            if (sp == null) return null;
            EnemyBase best = null;
            float bestD = 7f;
            for (int i = 0; i < sp.AliveCount; i++)
            {
                var e = sp.Alive[i];
                if (e == null || e == this || !e.IsAlive) continue;
                if (e.EnemyKind == EnemyKind.Boss) continue;
                float d = (e.Position - _pos).sqrMagnitude;
                if (d < bestD * bestD && d > 0.04f && e._stuckTimer < 0.05f)
                {
                    best = e;
                    bestD = d;
                }
            }
            return best;
        }

        /// <summary>Used by eggs / spawn effects to place an enemy exactly.</summary>
        public void Teleport(Vector2 p)
        {
            _pos = p;
            transform.position = new Vector3(p.x, p.y, 0f);
        }

        protected Vector2 NavigateToPlayer()
        {
            Vector2 dir;
            int dist;
            var nav = Game.Nav;
            if (nav != null && nav.Sample(_pos, out dir, out dist) && dist != GridNav.Unreachable && dir.sqrMagnitude > 0.001f)
                return dir;
            Vector2 toP = ToPlayer();
            return toP.sqrMagnitude > 0.0001f ? toP.normalized : Vector2.zero;
        }

        // ------------------------------------------------------------------ visuals

        private void TickLift(float dt)
        {
            // critically damped-ish return to the ground
            _liftVel -= 34f * _lift * dt;
            _liftVel *= Mathf.Pow(0.0008f, dt);
            _lift += _liftVel * dt;
            if (_lift < 0f) { _lift = 0f; _liftVel = 0f; }
            if (_lift > 0.22f) { _lift = 0.22f; _liftVel = Mathf.Min(_liftVel, 0f); }
        }

        protected virtual void UpdateVisual()
        {
            TickLift(Time.deltaTime);
            if (_popTimer > 0f) _popTimer -= Time.deltaTime;

            if (SimpleVisual)
            {
                if (Body == null) return;
                float vx = _vel.x;
                if (Mathf.Abs(vx) > 0.08f) _facing = vx > 0f ? 1f : -1f;
                Body.flipX = _facing < 0f;
                // Soul-Knight-ish gait: a small hop, a squash and a slight lean.
                float phase = (GetInstanceID() % 100) * 0.37f;
                float speed01 = Mathf.Clamp01(_vel.magnitude / 2.5f);
                float t = Time.time * (6.5f + speed01 * 3.5f) + phase;
                float hop = Mathf.Abs(Mathf.Sin(t)) * 0.075f * (0.35f + speed01);
                float lean = Mathf.Sin(t * 0.5f) * 3.2f * speed01;
                Vector3 bob = new Vector3(Mathf.Sin(t) * 0.02f * speed01, hop + _lift, 0f);
                float pop = _popTimer > 0f ? Mathf.Clamp01(_popTimer / 0.17f) : 0f;
                Body.transform.localPosition = bob + (Vector3)(_popOffset * (pop * pop) * 0.3f);
                float punch = 1f + pop * 0.22f;
                float squash = 1f + Mathf.Sin(t * 2f) * 0.035f * (0.3f + speed01);
                Body.transform.localScale = new Vector3(punch * squash, punch / squash, 1f) * BodyBaseScale;
                Body.transform.localRotation = Quaternion.Euler(0f, 0f, lean * _facing);
                if (_hurtTimer > 0f)
                    Body.color = Color.Lerp(_baseColors.Length > 0 ? _baseColors[0] : Color.white, Color.red, _hurtTimer / 0.12f);
                else if (Body.color != _baseColors[0]) Body.color = _baseColors[0];
                return;
            }

            if (VisualRoot != null)
            {
                if (!_visualBaseCaptured) { _visualBasePos = VisualRoot.localPosition; _visualBaseCaptured = true; }
                float vx = _vel.x;
                if (Mathf.Abs(vx) > 0.08f) _facing = vx > 0f ? 1f : -1f;
                float pop = _popTimer > 0f ? Mathf.Clamp01(_popTimer / 0.17f) : 0f;
                float punch = 1f + pop * 0.22f;
                Vector3 b = _visualBaseScale;
                float speedR = Mathf.Clamp01(_vel.magnitude / 2.5f);
                float phaseR = (GetInstanceID() % 100) * 0.41f;
                float gait = Time.time * (6.5f + speedR * 3.5f) + phaseR;
                float hopR = Mathf.Abs(Mathf.Sin(gait)) * 0.06f * (0.35f + speedR);
                float leanR = Mathf.Sin(gait * 0.5f) * 2.6f * speedR;
                float squashR = 1f + Mathf.Sin(gait * 2f) * 0.03f * (0.3f + speedR);

                VisualRoot.localScale = new Vector3(Mathf.Abs(b.x) * _facing * punch * squashR, Mathf.Abs(b.y) * punch / squashR, b.z);
                VisualRoot.localPosition = _visualBasePos + new Vector3(0f, hopR, 0f) + (Vector3)(_popOffset * (pop * pop) * 0.3f);
                VisualRoot.localRotation = Quaternion.Euler(0f, 0f, leanR * _facing);
            }
        }

        /// <summary>Punchy "hit" reaction: a short visual shove + squash, no gameplay displacement.</summary>
        public void PlayHitPop(Vector2 dir, float strength)
        {
            _popTimer = 0.17f;
            _popOffset = (dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.up) * Mathf.Max(0.2f, strength);
        }

        protected void UpdateBossRing()
        {
            if (BossRing == null || !IsBoss) return;
            _ringPhase += Time.deltaTime * 3.2f;
            float pulse = 0.92f + Mathf.Sin(_ringPhase) * 0.08f;
            BossRing.transform.localScale = new Vector3(pulse, pulse * 0.55f, 1f);
            Color c = BossRing.color;
            c.a = 0.65f + Mathf.Sin(_ringPhase * 1.3f) * 0.2f;
            BossRing.color = c;
        }

        protected void ResetTints()
        {
            if (_cachedRenderers == null) return;
            for (int i = 0; i < _cachedRenderers.Length; i++)
                if (_cachedRenderers[i] != null) _cachedRenderers[i].color = _baseColors[i];
        }

        protected void PlayState(string state, bool restart = false)
        {
            if (SimpleVisual || Animator == null) { _stateName = state; return; }
            if (!restart && _stateName == state) return;
            _stateName = state;
            Animator.Play(state, 0, 0f);
        }

        protected void FacePlayer()
        {
            Vector2 to = ToPlayer();
            if (Mathf.Abs(to.x) > 0.05f) _facing = to.x > 0f ? 1f : -1f;
        }

        // ------------------------------------------------------------------ damage

        public virtual void TakeDamage(DamageInfo info)
        {
            if (!_alive) return;
            _hp -= info.amount;
            _hurtTimer = 0.12f;

            // kick the visual upward; the settle runs in UpdateVisual
            _liftVel = Mathf.Max(_liftVel, 1.35f) + (info.crit ? 0.5f : 0f);

            if (_cachedRenderers != null)
            {
                Color flash = info.crit ? new Color(1f, 0.55f, 0.2f) : new Color(1f, 0.3f, 0.3f);
                for (int i = 0; i < _cachedRenderers.Length; i++)
                {
                    if (_cachedRenderers[i] == null) continue;
                    _cachedRenderers[i].color = flash;
                }
            }

            if (info.knockback > 0f && !IsBoss)
            {
                Vector2 dir = _pos - info.fromPosition;
                if (dir.sqrMagnitude < 1e-4f) dir = Random.insideUnitCircle.normalized;
                _vel += dir.normalized * info.knockback;
            }

            if (Game.Hud != null)
                Game.Hud.ShowDamage(_pos + Vector2.up * 0.7f, info.amount, info.crit);

            if (_hp <= 0)
            {
                _hp = 0;
                Die();
            }
        }

        protected virtual void Die()
        {
            _alive = false;
            _deadTimer = Game.Cfg.corpseFadeTime;
            _vel = Vector2.zero;
            if (BossRing != null) BossRing.enabled = false;

            if (!SimpleVisual)
            {
                PlayState(Kind == EnemyKind.Melee ? "chushouguai_dead"
                        : Kind == EnemyKind.Ranged ? "outuzhe_dead"
                        : "chushouguai_dead", true);
            }

            if (Game.Manager != null) Game.Manager.OnEnemyKilled(this);
            if (Game.Spawner != null) Game.Spawner.OnEnemyDied(this);
        }

        private void FadeCorpse()
        {
            if (_cachedRenderers == null) return;
            float a = Mathf.Clamp01(_deadTimer / Mathf.Max(0.01f, Game.Cfg.corpseFadeTime));
            // darken first, then fade — reads as a body going cold rather than blinking out
            float dark = Mathf.Lerp(0.35f, 1f, a);
            for (int i = 0; i < _cachedRenderers.Length; i++)
            {
                if (_cachedRenderers[i] == null) continue;
                Color c = _baseColors[i];
                c.r *= dark; c.g *= dark; c.b *= dark;
                c.a = _baseColors[i].a * a * a;
                _cachedRenderers[i].color = c;
            }
        }

        public void Despawn()
        {
            if (Game.Spawner != null) Game.Spawner.ReleaseEnemy(this);
            else Destroy(gameObject);
        }
    }
}
