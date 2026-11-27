using UnityEngine;

namespace Ares
{
    /// <summary>触手怪: chases along the flow field, winds up visibly, then lunges.</summary>
    public class MeleeEnemy : EnemyBase
    {
        protected enum State { Chase, Windup, Cooldown }
        protected State _state = State.Chase;
        protected float _timer;
        protected float _windupTime = 0.45f;
        protected bool _tintDirty;

        public override void Setup(Vector2 position, float hpScale, float dmgScale, float sizeScale)
        {
            if (Kind != EnemyKind.Boss)
            {
                Kind = EnemyKind.Melee;
                IsBoss = false;
            }
            ComputeStats(hpScale, dmgScale, sizeScale);
            _state = State.Chase;
            _timer = 0f;
            base.Setup(position, hpScale, dmgScale, sizeScale);
            PlayState("chushouguai_idle", true);
        }

        protected virtual void ComputeStats(float hpScale, float dmgScale, float sizeScale)
        {
            var cfg = Game.Cfg;
            _maxHp = Mathf.Max(1, Mathf.RoundToInt(cfg.meleeHp * hpScale));
            _speed = cfg.meleeSpeed * Random.Range(0.92f, 1.08f);
            _radius = cfg.meleeRadius * sizeScale;
            _damage = Mathf.Max(1, Mathf.RoundToInt(cfg.meleeDamage * dmgScale));
            _windupTime = cfg.meleeWindup;
        }

        protected override void Think(float dt)
        {
            var cfg = Game.Cfg;
            _timer -= Game.Spawner.ThinkInterval;

            switch (_state)
            {
                case State.Chase:
                    _desiredDir = NavigateToPlayer();
                    ComputeSeparation();
                    if (DistToPlayer() <= cfg.meleeAttackRange + _radius && Game.Player != null && Game.Player.IsAlive)
                    {
                        _state = State.Windup;
                        _timer = _windupTime;
                        _desiredDir = Vector2.zero;
                        _vel *= 0.25f;
                        FacePlayer();
                        PlayState("chushouguai_attack_pre", true);
                        AudioManager.PlaySfx("sfx_enemy_attack", 0.55f, 0.16f);

                        // only the small pip above the head — no floor decal (it blocked the view)
                        WarnPip.Spawn(_pos + Vector2.up * (0.85f + _radius), _windupTime, 0.5f);
                        AudioManager.PlaySfx("sfx_enemy_attack", 0.45f, 0.14f);

                        // visible tell: a short red slash arc that grows during the wind-up
                        if (Game.Combat != null)
                        {
                            Vector2 d = Game.Player != null ? (Game.Player.Position - _pos).normalized : Vector2.right;
                            Game.Combat.SpawnSlash(_pos, d, 0.9f, new Color(1f, 0.35f, 0.28f, 0.75f));
                        }
                    }
                    break;

                case State.Windup:
                    _desiredDir = Vector2.zero;
                    FacePlayer();
                    if (_timer <= 0f)
                    {
                        Strike();
                        _state = State.Cooldown;
                        _timer = cfg.meleeCooldown;
                        PlayState("chushouguai_attack_do", true);
                    }
                    break;

                case State.Cooldown:
                    _desiredDir = Vector2.zero;
                    ComputeSeparation();
                    if (_timer <= 0f) _state = State.Chase;
                    break;
            }
        }

        protected virtual void Strike()
        {
            var cfg = Game.Cfg;
            var player = Game.Player;
            if (player == null || !player.IsAlive) return;

            Vector2 dir = (player.Position - _pos);
            if (dir.sqrMagnitude < 0.001f) dir = Vector2.right;
            dir.Normalize();

            // the swing itself: ink claw marks + a ground shockwave (feedback #10)
            {
                float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                float reach = cfg.meleeAttackRange + _radius;
                if (Game.Combat != null)
                    Game.Combat.SpawnSlash(_pos, dir, reach, new Color(1f, 0.92f, 0.85f, 0.9f));

                SheetFx.Play("claw_swipe", _pos + dir * reach * 0.45f, reach * 0.95f, ang, 58, 70f,
                    new Color(1f, 0.72f, 0.68f, 1f));
                Fx.Play("fx_shockwave", _pos, reach * 1.0f, 0.28f, 0f, new Color(1f, 0.42f, 0.34f, 0.8f), 54);
            }

            if ((player.Position - _pos).magnitude <= cfg.meleeAttackRange + _radius + 0.4f)
                player.TakeDamage(DamageInfo.Simple(_damage, _pos, cfg.meleeEnemyKnockback));
            if (Game.Camera != null) Game.Camera.AddShake(0.1f);
        }

        protected override void UpdateVisual()
        {
            base.UpdateVisual();
            if (SimpleVisual || Animator == null) return;

            if (_state == State.Windup)
            {
                float k = 1f - Mathf.Clamp01(_timer / Mathf.Max(0.01f, _windupTime));
                var rends = GetComponentsInChildren<SpriteRenderer>(true);
                Color tint = Color.Lerp(Color.white, new Color(1f, 0.5f, 0.42f), k);
                for (int i = 0; i < rends.Length; i++)
                    if (rends[i] != null) rends[i].color = tint;
                _tintDirty = true;
                return;
            }
            if (_tintDirty)
            {
                _tintDirty = false;
                ResetTints();
            }
            PlayState(_vel.sqrMagnitude > 0.3f ? "chushouguai_walk" : "chushouguai_idle");
        }
    }
}
