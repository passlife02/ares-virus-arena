using UnityEngine;

namespace Ares
{
    /// <summary>呕吐者: keeps its distance, telegraphs, then spits a wall-blocked projectile.</summary>
    public class RangedEnemy : EnemyBase
    {
        private enum State { Reposition, Windup, Cooldown }
        private State _state = State.Reposition;
        private float _timer;
        private bool _tintDirty;

        public override void Setup(Vector2 position, float hpScale, float dmgScale, float sizeScale)
        {
            Kind = EnemyKind.Ranged;
            IsBoss = false;
            var cfg = Game.Cfg;
            _maxHp = Mathf.Max(1, Mathf.RoundToInt(cfg.rangedHp * hpScale));
            _speed = cfg.rangedSpeed * Random.Range(0.92f, 1.08f);
            _radius = cfg.rangedRadius * sizeScale;
            _damage = Mathf.Max(1, Mathf.RoundToInt(cfg.rangedDamage * dmgScale));
            _state = State.Reposition;
            _timer = 0f;
            base.Setup(position, hpScale, dmgScale, sizeScale);
            PlayState("outuzhe_idle", true);
        }

        protected override void Think(float dt)
        {
            var cfg = Game.Cfg;
            _timer -= Game.Spawner.ThinkInterval;
            Vector2 toP = ToPlayer();
            float dist = toP.magnitude;

            switch (_state)
            {
                case State.Reposition:
                    _desiredDir = RepositionDirection(toP, dist);
                    ComputeSeparation();
                    if (dist <= cfg.rangedMaxRange && Game.Player != null && Game.Player.IsAlive)
                    {
                        _state = State.Windup;
                        _timer = cfg.rangedWindup;
                        _desiredDir = Vector2.zero;
                        FacePlayer();
                        PlayState("outuzhe_attack_pre", true);
                    }
                    break;

                case State.Windup:
                    _desiredDir = Vector2.zero;
                    FacePlayer();
                    if (_timer <= 0f)
                    {
                        Fire();
                        _state = State.Cooldown;
                        _timer = cfg.rangedCooldown;
                        PlayState("outuzhe_attack_do", true);
                    }
                    break;

                case State.Cooldown:
                    _desiredDir = RepositionDirection(ToPlayer(), DistToPlayer()) * 0.6f;
                    ComputeSeparation();
                    if (_timer <= 0f) _state = State.Reposition;
                    break;
            }
        }

        private Vector2 RepositionDirection(Vector2 toP, float dist)
        {
            var cfg = Game.Cfg;
            if (dist < cfg.rangedKeepDist)
            {
                Vector2 away = -toP.normalized;
                Vector2 side = new Vector2(-away.y, away.x) * (Random.value > 0.5f ? 1f : -1f);
                Vector2 dir = (away + side * 0.55f).normalized;
                if (Game.Nav != null && !Game.Nav.IsWalkable(_pos + dir * 1.2f)) dir = side.normalized;
                return dir;
            }
            if (dist > cfg.rangedMaxRange * 0.9f) return NavigateToPlayer();

            Vector2 tangent = new Vector2(-toP.y, toP.x).normalized;
            if (Game.Nav != null && !Game.Nav.IsWalkable(_pos + tangent * 1.2f)) tangent = -tangent;
            return tangent * 0.55f;
        }

        private void Fire()
        {
            var cfg = Game.Cfg;
            var player = Game.Player;
            if (player == null || !player.IsAlive || Game.Combat == null) return;

            Vector2 toP = player.Position - _pos;
            if (toP.sqrMagnitude < 1e-4f) return;
            if (!Motion.HasLineOfSight(_pos, player.Position, Motion.WallMask)) return;

            Vector2 dir = toP.normalized;
            Vector2 spawn = _pos + dir * (_radius + 0.35f);
            Game.Combat.FireEnemyProjectile(spawn, dir, _damage, cfg.rangedProjectileSpeed, 3f, 3.5f);
        }

        protected override void UpdateVisual()
        {
            base.UpdateVisual();
            if (SimpleVisual || Animator == null) return;

            if (_state == State.Windup)
            {
                var rends = GetComponentsInChildren<SpriteRenderer>(true);
                float k = 1f - Mathf.Clamp01(_timer / Mathf.Max(0.01f, Game.Cfg.rangedWindup));
                Color tint = Color.Lerp(Color.white, new Color(0.58f, 1f, 0.5f), k);
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
            PlayState(_vel.sqrMagnitude > 0.3f ? "outuzhe_walk" : "outuzhe_idle");
        }
    }
}
