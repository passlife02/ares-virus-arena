using UnityEngine;

namespace Ares
{
    /// <summary>
    /// Boss variants (feedback #10 item 1).
    ///   Armadillo: curls up and rolls at the player very fast, huge contact damage.
    ///   Spider:    keeps its distance and spits a web that roots the player for ~1.5s.
    /// Both use the supplied boss sprites as a single-sprite flipbook body.
    /// </summary>
    public class BossEnemy : MeleeEnemy
    {
        public enum Variant { Armadillo, Spider }

        public Variant KindVariant = Variant.Armadillo;

        private enum BossState { Chase, Windup, Roll, Web, Recover }

        private BossState _bossState = BossState.Chase;
        private float _bossTimer;
        private float _nextWeb;
        private float _nextRoll;
        private float _nextEgg;
        private Vector2 _rollDir;
        private float _rollTick;
        private float _hitCooldown;
        private SpriteRenderer _body;
        private Sprite[] _frames;
        private float _frameClock;

        public override void Setup(Vector2 position, float hpScale, float dmgScale, float sizeScale)
        {
            Kind = EnemyKind.Boss;
            IsBoss = true;
            base.Setup(position, hpScale, dmgScale, sizeScale);

            var cfg = Game.Cfg;
            transform.localScale = Vector3.one * cfg.bossSizeMul;
            if (BossRing != null)
            {
                BossRing.enabled = true;
                BossRing.sortingOrder = -600;
            }

            _bossState = BossState.Chase;
            _nextWeb = Random.Range(3.0f, 5.0f);
            _nextRoll = 3.0f;
            _nextEgg = 8.0f;
            ApplyBossVisual();
        }

        /// <summary>Swaps the chushouguai rig for the supplied boss sheet.</summary>
        private void ApplyBossVisual()
        {
            string res = KindVariant == Variant.Spider ? "Art10/BossSpider" : "Art10/BossArmadillo";
            var all = Resources.LoadAll<Sprite>(res);
            if (all == null || all.Length == 0) return;
            System.Array.Sort(all, (a, b) => string.CompareOrdinal(a.name, b.name));
            _frames = all;

            if (VisualRoot != null)
            {
                var rends = VisualRoot.GetComponentsInChildren<SpriteRenderer>(true);
                for (int i = 0; i < rends.Length; i++) rends[i].enabled = false;

                var go = new GameObject("BossBody");
                go.transform.SetParent(VisualRoot, false);
                _body = go.AddComponent<SpriteRenderer>();
                _body.sprite = _frames[0];
                _body.sortingOrder = -250;
                // the source art is ~1.6 units wide at 200 ppu; scale to the boss footprint
                float target = 2.4f;
                float w = _body.sprite.bounds.size.x;
                float s = w > 0.01f ? target / w : 1f;
                go.transform.localScale = new Vector3(s, s, 1f);
            }
        }

        protected override void ComputeStats(float hpScale, float dmgScale, float sizeScale)
        {
            var cfg = Game.Cfg;
            _maxHp = Mathf.Max(1, Mathf.RoundToInt(cfg.meleeHp * hpScale * cfg.bossHpMul));
            _speed = cfg.meleeSpeed * cfg.bossSpeedMul * Random.Range(0.95f, 1.05f);
            _radius = cfg.meleeRadius * sizeScale;
            _damage = Mathf.Max(1, Mathf.RoundToInt(cfg.meleeDamage * dmgScale * cfg.bossDamageMul));
            _windupTime = cfg.bossWindup;
        }

        protected override void Think(float dt)
        {
            var cfg = Game.Cfg;
            float step = Game.Spawner.ThinkInterval;
            _bossTimer -= step;
            _hitCooldown -= step;
            _nextWeb -= step;
            _nextRoll -= step;
            _nextEgg -= step;

            var player = Game.Player;
            bool hasPlayer = player != null && player.IsAlive;

            // ---- spider: lay 3-5 eggs, faster as it loses health (60s -> 30s) ----
            if (KindVariant == Variant.Spider && hasPlayer && _nextEgg <= 0f && _bossState == BossState.Chase)
            {
                LayEggs();
                float hp01 = _maxHp > 0 ? Mathf.Clamp01((float)_hp / _maxHp) : 1f;
                _nextEgg = Mathf.Lerp(30f, 60f, hp01);
                _bossState = BossState.Recover;
                _bossTimer = 0.9f;
                return;
            }

            // ---- spider: fire a web when the player keeps distance ----
            if (KindVariant == Variant.Spider && hasPlayer && _nextWeb <= 0f && _bossState == BossState.Chase)
            {
                _bossState = BossState.Web;
                _bossTimer = 0.5f;
                _desiredDir = Vector2.zero;
                FacePlayer();
                AudioManager.PlaySfx("sfx_enemy_attack", 0.5f, 0.2f);
                return;
            }

            switch (_bossState)
            {
                case BossState.Chase:
                    _desiredDir = NavigateToPlayer();
                    ComputeSeparation();

                    if (hasPlayer)
                    {
                        float d = DistToPlayer();
                        if (KindVariant == Variant.Armadillo && d > 2.2f && d < 9f && _nextRoll <= 0f)
                        {
                            // crouch, then roll
                            _bossState = BossState.Windup;
                            _bossTimer = 0.7f;
                            _desiredDir = Vector2.zero;
                            float hpFrac = _maxHp > 0 ? Mathf.Clamp01((float)_hp / _maxHp) : 1f;
                            _nextRoll = Mathf.Lerp(5f, 20f, hpFrac);   // 20s at full hp -> 5s when nearly dead
                            FacePlayer();
                            AudioManager.PlaySfx("sfx_enemy_attack", 0.7f, 0.1f);
                            Fx.Play("fx_dust", _pos, 1.5f, 0.45f);
                        }
                        else if (d <= cfg.bossAttackRange + _radius)
                        {
                            _bossState = BossState.Windup;
                            _bossTimer = _windupTime;
                            _desiredDir = Vector2.zero;
                            FacePlayer();
                            WarnPip.Spawn(_pos + Vector2.up * (1.15f + _radius), _windupTime, 0.7f);
                            AudioManager.PlaySfx("sfx_boss_roar", 0.85f, 0.06f);
                        }
                    }
                    break;

                case BossState.Windup:
                    _desiredDir = Vector2.zero;
                    FacePlayer();
                    if (KindVariant == Variant.Armadillo && _bossTimer > 0.15f)
                        Fx.Play("fx_dust", _pos + Random.insideUnitCircle * 0.6f, 1.0f, 0.4f);

                    if (_bossTimer <= 0f)
                    {
                        if (KindVariant == Variant.Armadillo)
                        {
                            _bossState = BossState.Roll;
                            _bossTimer = 0.95f;
                            _rollDir = hasPlayer ? (player.Position - _pos).normalized : _facingDir();
                            _rollTick = 0f;
                            Fx.Play("fx_crack", _pos + _rollDir * 0.9f, 2.6f, 0.8f, 0f, new Color(0.85f, 0.8f, 0.75f, 0.9f), -170);
                            if (Game.Camera != null) Game.Camera.AddShake(0.35f);
                        }
                        else
                        {
                            Strike();
                            _bossState = BossState.Recover;
                            _bossTimer = cfg.bossCooldown;
                        }
                    }
                    break;

                case BossState.Roll:
                    _desiredDir = _rollDir;
                    _rollTick -= step;
                    if (_rollTick <= 0f)
                    {
                        _rollTick = 0.08f;
                        Fx.Play("fx_dust", _pos, 1.3f, 0.35f);
                    }

                    if (hasPlayer && _hitCooldown <= 0f)
                    {
                        float d = (player.Position - _pos).magnitude;
                        if (d <= _radius + 0.9f)
                        {
                            var info = DamageInfo.Simple(Mathf.RoundToInt(_damage * 1.25f), _pos, 6f);
                            info.crit = true;
                            player.TakeDamage(info);
                            _hitCooldown = 0.6f;
                            if (Game.Camera != null) Game.Camera.AddShake(0.45f);
                            Fx.Play("fx_shockwave", _pos, 2.2f, 0.4f);
                        }
                    }

                    if (_bossTimer <= 0f)
                    {
                        _bossState = BossState.Recover;
                        _bossTimer = cfg.bossCooldown;
                        Fx.Play("fx_shockwave", _pos, 2.2f, 0.45f);
                        Fx.Play("fx_crack", _pos, 3.0f, 0.9f, 0f, new Color(0.8f, 0.75f, 0.7f, 0.9f), -170);
                    }
                    break;

                case BossState.Web:
                    _desiredDir = Vector2.zero;
                    if (_bossTimer <= 0f)
                    {
                        FireWeb();
                        _nextWeb = Random.Range(3.5f, 5.5f);
                        _bossState = BossState.Recover;
                        _bossTimer = 0.8f;
                    }
                    break;

                case BossState.Recover:
                    _desiredDir = Vector2.zero;
                    if (_bossTimer <= 0f) _bossState = BossState.Chase;
                    break;
            }
        }

        private Vector2 _facingDir()
        {
            return _facing >= 0f ? Vector2.right : Vector2.left;
        }

        private void LayEggs()
        {
            int count = Random.Range(3, 6);
            for (int i = 0; i < count; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                float r = Random.Range(0.9f, 2.2f);
                Vector2 p = _pos + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                var m = Game.Manager;
                if (m != null)
                {
                    p.x = Mathf.Clamp(p.x, m.ArenaMin.x + 0.6f, m.ArenaMax.x - 0.6f);
                    p.y = Mathf.Clamp(p.y, m.ArenaMin.y + 0.6f, m.ArenaMax.y - 0.6f);
                }
                SpiderEgg.Lay(p, Random.Range(10f, 14f));
            }
            Fx.Play("fx_web", _pos, 1.6f, 0.6f);
            AudioManager.PlaySfx("sfx_enemy_attack", 0.5f, 0.15f);
            if (Game.Hud != null) Game.Hud.ShowPrompt("蜘蛛产下了蛛卵！", 1.4f);
        }

        private void FireWeb()
        {
            var player = Game.Player;
            if (player == null || !player.IsAlive) return;
            Vector2 dir = (player.Position - _pos);
            if (dir.sqrMagnitude < 0.001f) dir = Vector2.right;
            dir.Normalize();

            if (Game.Combat != null)
                Game.Combat.FireEnemyWeb(_pos, dir, 6, 7.5f, 3f);
            AudioManager.PlaySfx("sfx_bow", 0.6f, 0.2f);
        }

        protected override void Strike()
        {
            var cfg = Game.Cfg;
            var player = Game.Player;
            if (player == null || !player.IsAlive) return;

            Vector2 dir = (player.Position - _pos);
            if (dir.sqrMagnitude < 0.001f) dir = Vector2.right;
            dir.Normalize();

            if (Game.Combat != null)
            {
                Game.Combat.SpawnSlash(_pos, dir, cfg.bossAttackRange + _radius, new Color(1f, 0.95f, 0.9f, 0.95f));
                Fx.Play("fx_claw", _pos + dir * 0.7f, 1.4f,
                    0.26f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 20f, new Color(1f, 0.34f, 0.28f, 0.95f));
                Fx.Play("fx_shockwave", _pos, 2.3f, 0.4f);
            }

            if ((player.Position - _pos).magnitude <= cfg.bossAttackRange + _radius + 0.5f)
                player.TakeDamage(DamageInfo.Simple(_damage, _pos, cfg.meleeEnemyKnockback * 1.4f));
            if (Game.Camera != null) Game.Camera.AddShake(0.32f);
        }

        protected override void UpdateVisual()
        {
            base.UpdateVisual();

            // flipbook so the supplied boss art animates
            if (_body != null && _frames != null && _frames.Length > 0)
            {
                _frameClock += Time.deltaTime;
                int idx = Mathf.FloorToInt(_frameClock * 8f) % _frames.Length;
                _body.sprite = _frames[idx];
                _body.flipX = _facing < 0f;
            }
        }
    }
}
