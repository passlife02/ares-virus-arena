using UnityEngine;

namespace Ares
{
    /// <summary>
    /// Player locomotion + aim. The body faces the movement direction (feedback #10) while the
    /// arm/gun is driven by PlayerRig towards the cursor, and the flip is smoothed so turning
    /// reads as a rotation rather than a teleport (feedback #17).
    /// </summary>
    public class PlayerController : MonoBehaviour, IDamageable
    {
        private Vector2 _pos;
        private Vector2 _vel;
        private Vector2 _aimDir = Vector2.right;
        private float _rollTimer;
        private float _rollCooldownTimer;
        private float _invulnTimer;
        private float _hurtTimer;
        private float _knockbackTimer;
        private Vector2 _rollDir;

        private int _hp;
        private bool _alive = true;
        private bool _frozen;

        [Header("Wired by the scene builder")]
        public Transform VisualRoot;
        public PlayerRig Rig;
        public PlayerCombat Weapons;

        private Animator _anim;
        private SpriteRenderer[] _renderers;
        private Color[] _baseColors;
        private string _currentState = "";
        private float _baseScaleX = 1f;
        private float _flip = 1f;
        private float _facing = 1f;

        public int Hp { get { return _hp; } }
        public int BonusMaxHp;
        public int MaxHp { get { return Game.Cfg.playerMaxHp + BonusMaxHp; } }

        /// <summary>Merchant blessing applied for this run only.</summary>
        public float SpeedMul = 1f;

        private float _rootTimer;

        /// <summary>Spider web / trap: movement is locked but shooting still works.</summary>
        public void Root(float seconds) { _rootTimer = Mathf.Max(_rootTimer, seconds); }
        public bool Rooted { get { return _rootTimer > 0f; } }

        public float RollCooldown01
        {
            get
            {
                if (Game.Cfg.rollCooldown <= 0f) return 1f;
                return Mathf.Clamp01(1f - _rollCooldownTimer / Game.Cfg.rollCooldown);
            }
        }

        public bool IsRolling { get { return _rollTimer > 0f; } }
        public bool IsAlive { get { return _alive; } }
        public Vector2 Position { get { return _pos; } }
        public Vector2 AimDir { get { return _aimDir; } }
        public float HurtFlash01 { get { return Mathf.Clamp01(_hurtTimer / 0.16f); } }
        public float Speed01 { get { return Mathf.Clamp01(_vel.magnitude / Mathf.Max(0.01f, Game.Cfg.moveSpeed)); } }
        public Vector2 Velocity { get { return _vel; } }
        /// <summary>Menu showcase brightness (feedback #6). 1 = normal.</summary>
        public float PortraitBrightness = 1f;

        public void Spawn(Vector2 position)
        {
            _pos = position;
            _vel = Vector2.zero;
            var bless = Blessing.Current();
            BonusMaxHp = bless.maxHpBonus;
            SpeedMul = bless.speedMul > 0f ? bless.speedMul : 1f;
            _hp = MaxHp;
            _alive = true;
            _frozen = false;
            _rollTimer = 0f;
            _rollCooldownTimer = 0f;
            _invulnTimer = 0f;
            _hurtTimer = 0f;
            _aimDir = Vector2.right;
            _flip = 1f;
            _facing = 1f;
            transform.position = new Vector3(_pos.x, _pos.y, 0f);
            if (VisualRoot != null) VisualRoot.gameObject.SetActive(true);
            SetState("neo2_idle");
            ClearFlash();
            if (Rig != null) { Rig.Initialise(); Rig.SetAim(_aimDir, 1f); }
        }

        private void Awake()
        {
            if (VisualRoot != null)
            {
                _baseScaleX = Mathf.Abs(VisualRoot.localScale.x);
                _anim = VisualRoot.GetComponentInChildren<Animator>();
                _renderers = VisualRoot.GetComponentsInChildren<SpriteRenderer>(true);
                _baseColors = new Color[_renderers.Length];
                for (int i = 0; i < _renderers.Length; i++) _baseColors[i] = _renderers[i].color;
            }
        }

        private void Update()
        {
            if (!_alive) { UpdateFlash(); return; }

            bool canAct = Game.CanPlay && !_frozen && !Game.UiBlocking;
            float dt = Time.deltaTime;

            TickTimers(dt);
            UpdateAim();
            if (canAct) ReadInput();

            TickFootsteps();
            UpdateMovement(dt, canAct);
            UpdateAnimation(dt);

            transform.position = new Vector3(_pos.x, _pos.y, 0f);
            UpdateFlash();

            if (Rig != null)
            {
                float w = (_rollTimer > 0f || !_alive) ? 0f : 1f;
                Rig.SetAim(_aimDir, w);
            }
        }

        private void TickTimers(float dt)
        {
            if (_rollCooldownTimer > 0f) _rollCooldownTimer -= dt;
            if (_invulnTimer > 0f) _invulnTimer -= dt;
            if (_hurtTimer > 0f) _hurtTimer -= dt;
            if (_rootTimer > 0f) _rootTimer -= dt;
        }

        private void UpdateAim()
        {
            if (Game.Camera == null || Game.Camera.Cam == null) return;
            Vector3 world = Game.Camera.Cam.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, -Game.Camera.Cam.transform.position.z));
            Vector2 d = (Vector2)(world - (Vector3)_pos);
            if (d.sqrMagnitude > 0.0004f) _aimDir = d.normalized;
        }

        private void ReadInput()
        {
            if (InputService.RollPressed && _rollTimer <= 0f && _rollCooldownTimer <= 0f)
                StartRoll();
        }

        private void StartRoll()
        {
            Vector2 dir = InputService.MoveAxis();
            if (dir.sqrMagnitude < 0.01f) dir = _aimDir;
            _rollDir = dir.normalized;
            _rollTimer = Game.Cfg.rollDuration;
            _rollCooldownTimer = Game.Cfg.rollCooldown;
            _invulnTimer = Mathf.Max(_invulnTimer, Game.Cfg.rollInvulnTime);
            SetState("neo2_avoid");
            if (Game.Camera != null) Game.Camera.AddShake(0.12f);
        }

        private float _stepTimer;

        private void TickFootsteps()
        {
            if (Game.Manager == null || Game.Manager.Phase != GamePhase.Playing) return;
            if (_vel.sqrMagnitude < 0.05f) { _stepTimer = 0f; return; }
            _stepTimer -= Time.deltaTime;
            if (_stepTimer > 0f) return;
            _stepTimer = 0.36f;
            AudioManager.PlaySfx("sfx_footstep", 0.26f, 0.2f);
        }

        private void UpdateMovement(float dt, bool canAct)
        {
            var cfg = Game.Cfg;

            if (_rollTimer > 0f)
            {
                _rollTimer -= dt;
                float t = 1f - Mathf.Clamp01(_rollTimer / cfg.rollDuration);
                float speed = (cfg.rollDistance / cfg.rollDuration) * (1.15f - t * 0.55f);
                _vel = _rollDir * speed;
            }
            else
            {
                Vector2 input = canAct ? InputService.MoveAxis() : Vector2.zero;
                Vector2 desired = input * (cfg.moveSpeed * SpeedMul);
            if (_rootTimer > 0f) desired = Vector2.zero;
                float accel;
                if (desired.sqrMagnitude < 0.0001f) accel = cfg.moveStopDecel;
                else if (Vector2.Dot(desired, _vel) < 0f) accel = cfg.moveTurnDecel;
                else accel = cfg.moveAccel;
                _vel = Vector2.MoveTowards(_vel, desired, accel * dt);
            }

            Vector2 delta = _vel * dt;
            bool hitWall;
            Vector2 next = Motion.MoveCircle(_pos, delta, cfg.playerRadius, Motion.WallMask, out hitWall);
            if (hitWall)
            {
                Vector2 actual = next - _pos;
                if (actual.sqrMagnitude > 1e-6f)
                {
                    Vector2 movedDir = actual.normalized;
                    float along = Vector2.Dot(_vel, movedDir);
                    if (along < 0f) _vel -= movedDir * along;
                }
                else _vel *= 0.2f;
                if (_rollTimer > 0f) _rollTimer = Mathf.Min(_rollTimer, 0.05f);
            }
            _pos = next;

            // Never let a knockback or a corner wedge leave the player inside geometry.
            _pos = Motion.Depenetrate(_pos, cfg.playerRadius, Motion.WallMask);

            var m = Game.Manager;
            if (m != null)
            {
                _pos.x = Mathf.Clamp(_pos.x, m.ArenaMin.x + cfg.playerRadius, m.ArenaMax.x - cfg.playerRadius);
                _pos.y = Mathf.Clamp(_pos.y, m.ArenaMin.y + cfg.playerRadius, m.ArenaMax.y - cfg.playerRadius);
            }
        }

        private void UpdateAnimation(float dt)
        {
            if (VisualRoot == null) return;

            bool moving = _vel.sqrMagnitude > 0.35f * 0.35f;
            string want;
            if (!_alive) want = "neo2_dead";
            else if (_rollTimer > 0f) want = "neo2_avoid";
            else if (Weapons != null && Weapons.Swinging) want = "neo2_attack";
            else if (moving) want = "neo2_run";
            else want = "neo2_idle";

            if (want != _currentState) SetState(want);

            // Body faces the movement direction; when standing still it follows the aim.
            Vector2 move = InputService.MoveAxis();
            if (Mathf.Abs(move.x) > 0.15f) _facing = move.x > 0f ? 1f : -1f;
            else if (Mathf.Abs(_aimDir.x) > 0.05f) _facing = _aimDir.x > 0f ? 1f : -1f;

            _flip = Mathf.MoveTowards(_flip, _facing, 12f * dt);
            float mag = _baseScaleX * (1f - (1f - Mathf.Abs(_flip)) * 0.45f);

            Vector3 s = VisualRoot.localScale;
            s.x = (_flip >= 0f ? 1f : -1f) * mag;
            VisualRoot.localScale = s;
        }

        private void SetState(string state)
        {
            _currentState = state;
            if (_anim != null) _anim.Play(state, 0, 0f);
        }

        public void AddRecoil(Vector2 impulse)
        {
            _vel += impulse;
        }

        public void TakeDamage(DamageInfo info)
        {
            if (!_alive || _invulnTimer > 0f) return;

            var cfg = Game.Cfg;

            // worn armour soaks part of every hit and loses durability doing it
            int incoming = info.amount;
            float soak = Armors.Defense01();
            if (soak > 0.001f)
            {
                incoming = Mathf.Max(1, Mathf.RoundToInt(info.amount * (1f - soak)));
                var broken = Armors.Wear(1);
                if (broken != null && Game.Hud != null)
                    Game.Hud.ShowPrompt(broken.name + " 已经损坏，需要修理", 2f);
            }

            _hp -= incoming;
            _hurtTimer = 0.16f;

            // a small kick so getting hit registers (feedback #15)
            if (Game.Camera != null) Game.Camera.AddShake(0.16f);
            AudioManager.PlaySfx("sfx_hurt", 0.9f, 0.08f);
            // Clamp defensively: a corrupted tuning value must never make the player immortal.
            _invulnTimer = Mathf.Clamp(cfg.playerHurtInvuln, 0.05f, 1.5f);

            // Short, capped shove: enemies used to launch the player across the arena.
            Vector2 dir = _pos - info.fromPosition;
            if (dir.sqrMagnitude < 1e-4f) dir = Random.insideUnitCircle.normalized;
            float push = Mathf.Clamp(info.knockback, 0f, 6f);
            _vel += dir.normalized * push;

            if (Game.Camera != null) Game.Camera.AddShake(0.34f);
            if (Game.Hud != null)
            {
                Game.Hud.FlashDamage();
                Game.Hud.ShowDamage(_pos + Vector2.up * 0.9f, info.amount, false);
            }
            if (Game.Combat != null) Game.Combat.SpawnBlood(_pos);

            if (_hp <= 0)
            {
                _hp = 0;
                Die();
            }
        }

        private void Die()
        {
            _alive = false;
            _rollTimer = 0f;
            SetState("neo2_dead");
            if (Rig != null) Rig.SetAim(_aimDir, 0f);
            if (Game.Manager != null) Game.Manager.OnPlayerDied();
        }

        private void UpdateFlash()
        {
            if (_renderers == null) return;
            float f = _hurtTimer > 0f ? Mathf.Clamp01(_hurtTimer / 0.16f) : 0f;
            float bri = Mathf.Max(0.05f, PortraitBrightness);
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] == null) continue;
                Color b = _baseColors[i];
                b.r = Mathf.Clamp01(b.r * bri);
                b.g = Mathf.Clamp01(b.g * bri);
                b.b = Mathf.Clamp01(b.b * bri);
                _renderers[i].color = Color.Lerp(b, new Color(1f, 0.2f, 0.18f, b.a), f * 0.9f);
            }
        }

        private void ClearFlash()
        {
            if (_renderers == null) return;
            for (int i = 0; i < _renderers.Length; i++)
                if (_renderers[i] != null) _renderers[i].color = _baseColors[i];
        }

        public void Heal(int amount)
        {
            if (!_alive || amount <= 0) return;
            _hp = Mathf.Min(MaxHp, _hp + amount);
            if (Game.Hud != null) Game.Hud.ShowDamage(_pos + Vector2.up * 0.9f, amount, false);
        }

        public bool IsFrozen { get { return _frozen; } }

        public void SetFrozen(bool frozen)
        {
            _frozen = frozen;
            if (frozen) _vel = Vector2.zero;
        }
    }
}
