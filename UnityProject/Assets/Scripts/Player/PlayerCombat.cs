using System.Collections.Generic;
using UnityEngine;

namespace Ares
{
    /// <summary>
    /// Weapon logic: melee swings with durability, ranged fire with ammo consumption.
    /// Damage is rolled here so crits are resolved once per hit and the floating text,
    /// hit-stop and knockback all agree.
    /// </summary>
    public class PlayerCombat : MonoBehaviour
    {
        private readonly List<EnemyBase> _hits = new List<EnemyBase>(32);

        /// <summary>Hold the button with a melee weapon to charge; heavy hit on release.</summary>
        public bool Charging { get; private set; }
        public float Charge01 { get { return _charge / MaxCharge; } }
        public float ChargedRange { get; private set; }
        public const float MaxCharge = 0.9f;
        private float _charge;
        private float _attackTimer;
        private float _swingTimer;
        private float _swingDuration = 0.2f;
        private Vector2 _swingDir;
        private float _swingRange;
        private float _swingCharge;

        public float AttackCooldown01
        {
            get { return _attackTimer <= 0f ? 1f : 1f - Mathf.Clamp01(_attackTimer / 0.9f); }
        }
        public bool Swinging { get { return _swingTimer > 0f; } }
        public float SwingProgress
        {
            get { return _swingDuration <= 0f ? 1f : 1f - Mathf.Clamp01(_swingTimer / _swingDuration); }
        }
        public Vector2 SwingDir { get { return _swingDir; } }
        public float SwingRange { get { return _swingRange; } }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (_attackTimer > 0f) _attackTimer -= dt;
            if (_swingTimer > 0f) _swingTimer -= dt;

            if (!Game.CanPlay || Game.UiBlocking) return;
            var player = Game.Player;
            var eq = Game.Equipment;
            if (player == null || eq == null || !player.IsAlive) return;
            if (player.IsRolling) return;

            if (InputService.SwitchWeaponPressed) eq.Toggle();

            if (InputService.GrenadePressed) ThrowGrenade(player);
            if (InputService.FencePressed) PlaceFence(player);

            var def = eq.ActiveDef;
            bool unarmed = def == null;
            if (def == null) def = FistDef();   // bare hands still work (feedback #11)

            if (def.IsMelee || unarmed)
            {
                // hold to charge, release to swing (feedback #14 item 4)
                bool held = InputService.FireHeld;
                float baseRange = (def.range > 0.1f ? def.range : 1.3f)
                                + WeaponMods.RangeBonus() + Enhancement.ReachBonus(def.id);

                if (held && _attackTimer <= 0f)
                {
                    _charge = Mathf.Min(MaxCharge, _charge + dt);
                    Charging = true;
                }
                else
                {
                    if (Charging)
                    {
                        Charging = false;
                        float c = Mathf.Clamp01(_charge / MaxCharge);
                        _charge = 0f;
                        if (_attackTimer <= 0f) MeleeAttack(def, c);
                    }
                }
                ChargedRange = baseRange * (1f + 0.35f * Mathf.Clamp01(_charge / MaxCharge));
            }
            else
            {
                Charging = false;
                _charge = 0f;
                ChargedRange = 0f;
                if (InputService.FireHeld && _attackTimer <= 0f)
                {
                    RangedAttack(def);
                    Milestones.Bump("shot", 1);
                    if (def.fx == "feather") Milestones.Bump("bow", 1);
                }
            }
        }

        /// <summary>Q: throw a grenade if one is in the bag.</summary>
        private void ThrowGrenade(PlayerController player)
        {
            var inv = Game.Inventory;
            if (inv == null) return;
            if (inv.CountOf("tool_grenade") <= 0)
            {
                if (Game.Hud != null) Game.Hud.ShowPrompt("没有手榴弹（商人可买 / 铁匠可造）", 1.2f);
                return;
            }
            inv.RemoveCount("tool_grenade", 1);

            Vector2 dir = player.AimDir.sqrMagnitude > 0.01f ? player.AimDir.normalized : Vector2.up;
            Vector2 target = player.Position + dir * Grenade.ThrowDistance;
            Vector2 min = Game.Manager.ArenaMin + Vector2.one * 0.8f;
            Vector2 max = Game.Manager.ArenaMax - Vector2.one * 0.8f;
            target.x = Mathf.Clamp(target.x, min.x, max.x);
            target.y = Mathf.Clamp(target.y, min.y, max.y);

            Grenade.Throw(player.Position + dir * 0.4f, (target - player.Position).normalized, Vector2.Distance(player.Position, target));
            if (Game.Hud != null) Game.Hud.ShowPrompt("投出手榴弹！", 0.8f);
            AudioManager.PlaySfx("sfx_click", 0.6f, 0.2f);
        }

        /// <summary>E: drop a wooden barricade in front of the player.</summary>
        private void PlaceFence(PlayerController player)
        {
            var inv = Game.Inventory;
            if (inv == null) return;
            if (inv.CountOf("tool_fence") <= 0)
            {
                if (Game.Hud != null) Game.Hud.ShowPrompt("没有木栅栏（用木头制作）", 1.2f);
                return;
            }

            Vector2 dir = player.AimDir.sqrMagnitude > 0.01f ? player.AimDir.normalized : Vector2.up;
            Vector2 pos = player.Position + dir * 1.5f;
            if (!Fence.Place(pos))
            {
                if (Game.Hud != null) Game.Hud.ShowPrompt("这里放不下栅栏", 1.1f);
                return;
            }
            inv.RemoveCount("tool_fence", 1);
            if (Game.Hud != null) Game.Hud.ShowPrompt("放下木栅栏", 0.8f);
        }

        private static ItemDef _fist;

        /// <summary>Bare-handed attack: 30% weaker and 20% slower than the machete.</summary>
        public static ItemDef FistDef()
        {
            if (_fist != null) return _fist;
            float knifeDamage = 24f;
            float knifeInterval = 0.36f;
            _fist = new ItemDef
            {
                id = "unarmed",
                name = "拳头",
                category = "武器",
                desc = "没有武器时的徒手攻击。",
                weaponKind = 1,
                damage = Mathf.Max(1, Mathf.RoundToInt(knifeDamage * 0.7f)),
                attackInterval = knifeInterval / 0.8f,
                range = 1.05f,
                durabilityMax = 0,
                stackMax = 1,
            };
            return _fist;
        }

        // ------------------------------------------------------------------ melee

        private void MeleeAttack(ItemDef def, float charge01 = 0f)
        {
            var cfg = Game.Cfg;
            var player = Game.Player;
            var eq = Game.Equipment;

            _attackTimer = Mathf.Max(0.14f, def.attackInterval);
            _swingTimer = cfg.meleeSwingTime;
            _swingDuration = cfg.meleeSwingTime;
            _swingDir = player.AimDir;
            _swingRange = ((def.range > 0.1f ? def.range : 1.3f) + WeaponMods.RangeBonus() + Enhancement.ReachBonus(def.id))
                        * (1f + 0.35f * charge01);
            _swingCharge = charge01;
            AudioManager.PlaySfx("sfx_slash", 0.85f, 0.12f);
            Milestones.Bump("swing", 1);
            if (charge01 >= 0.98f) Milestones.Bump("charge", 1);

            float halfArc = cfg.meleeArcDeg * 0.5f * Mathf.Deg2Rad;
            float cosLimit = Mathf.Cos(halfArc);

            SpiderEgg.HitArc(player.Position, _swingDir, _swingRange, 55f,
                Mathf.Max(1, Mathf.RoundToInt((def.damage + Enhancement.Bonus(def.id)) * (1f + 0.5f * charge01))));

            _hits.Clear();
            var alive = Game.Spawner != null ? Game.Spawner.Alive : null;
            if (alive != null)
            {
                for (int i = 0; i < alive.Count; i++)
                {
                    var e = alive[i];
                    if (e == null || !e.IsAlive) continue;
                    Vector2 to = e.Position - player.Position;
                    float d = to.magnitude;
                    if (d > _swingRange + e.Radius) continue;
                    if (d > 0.001f && Vector2.Dot(_swingDir, to / d) < cosLimit) continue;
                    _hits.Add(e);
                }
            }

            int hitCount = 0;
            for (int i = 0; i < _hits.Count; i++)
            {
                var e = _hits[i];
                var blessM = Blessing.Current();
                bool crit = Random.value < cfg.critChance + WeaponMods.CritBonus() + blessM.critBonus;
                float mulM = WeaponMods.DamageMul() * (blessM.damageMul > 0f ? blessM.damageMul : 1f);
                int dmg = Mathf.Max(1, Mathf.RoundToInt((def.damage + Enhancement.Bonus(def.id)) * mulM
                    * (crit ? cfg.critMultiplier : 1f) * (1f + 0.5f * _swingCharge)));
                var info = new DamageInfo();
                info.amount = dmg;
                info.crit = crit;
                info.fromPosition = player.Position;
                info.knockback = cfg.meleeKnockback;
                e.TakeDamage(info);
                e.PlayHitPop(_swingDir, 1.1f);
                hitCount++;
            }

            // Barrels sitting inside the swing arc take the hit as well.
            var barrel = Barrel.FindNear(player.Position + _swingDir * (_swingRange * 0.5f), _swingRange * 0.5f);
            if (barrel != null) barrel.TakeDamage(def.damage, player.Position);

            AudioManager.PlaySfx("sfx_slash", 0.85f, 0.12f);

            if (Game.Combat != null)
            {
                Color c = hitCount > 0 ? new Color(1f, 0.72f, 0.45f) : new Color(1f, 1f, 1f, 0.85f);
                Game.Combat.SpawnSlash(player.Position, _swingDir, _swingRange, c);
            }

            // pixelfx sword slash (white blade edge), timed to the swing
            {
                float ang = Mathf.Atan2(_swingDir.y, _swingDir.x) * Mathf.Rad2Deg;
                Vector2 c = player.Position + _swingDir * (_swingRange * 0.45f);
                float fps = Mathf.Clamp(24f / Mathf.Max(0.1f, _swingDuration), 30f, 90f);
                SheetFx.Play("sword_slash", c, _swingRange * 1.15f, ang, 62, fps);
            }

            if (Game.Camera != null) Game.Camera.AddShake(hitCount > 0 ? 0.16f : 0.05f);
            if (hitCount > 0 && Game.Manager != null) Game.Manager.HitStop(cfg.hitStopTime);

            if (eq != null && def.durabilityMax > 0) eq.WearWeapon();
        }

        /// <summary>Fire weapons get a hot ember tracer, the bow gets a pale feather-cut trail.</summary>
        public static Color TrajectoryColor(ItemDef def)
        {
            if (def == null) return new Color(1f, 0.6f, 0.2f, 0.9f);
            if (!string.IsNullOrEmpty(def.fx) && def.fx == "feather") return new Color(0.55f, 0.85f, 1f, 0.9f);
            return new Color(1f, 0.55f, 0.16f, 0.95f);
        }

        // ------------------------------------------------------------------ ranged'

        private void RangedAttack(ItemDef def)
        {
            var cfg = Game.Cfg;
            var player = Game.Player;
            var inv = Game.Inventory;

            if (def.UsesAmmo)
            {
                if (inv == null || inv.CountOf(def.ammoItemId) < def.ammoPerShot * Mathf.Max(1, WeaponMods.Pellets()))
                {
                    _attackTimer = 0.35f;
                    if (Game.Hud != null) Game.Hud.ShowPrompt("没有子弹", 1f);
                    return;
                }
                inv.RemoveCount(def.ammoItemId, def.ammoPerShot * Mathf.Max(1, WeaponMods.Pellets()));
            }

            _attackTimer = Mathf.Max(0.06f, def.attackInterval);

            var bless = Blessing.Current();
            bool crit = Random.value < cfg.critChance + WeaponMods.CritBonus() + bless.critBonus;
            float dmgMul = WeaponMods.DamageMul() * (bless.damageMul > 0f ? bless.damageMul : 1f);
            int dmg = Mathf.Max(1, Mathf.RoundToInt((def.damage + Enhancement.Bonus(def.id)) * dmgMul * (crit ? cfg.critMultiplier : 1f)));
            if (WeaponMods.Spread()) dmg = Mathf.Max(1, Mathf.RoundToInt(dmg * 0.7f));

            Vector2 aim = player.AimDir;
            float spread = Random.Range(-cfg.bulletSpreadDeg, cfg.bulletSpreadDeg) * Mathf.Deg2Rad;
            Vector2 dir = Motion.Rotate(aim, spread);

            Vector2 muzzle = player.Position + aim * 0.6f;
            if (player.Rig != null && player.Rig.CurrentWeight > 0.3f)
                muzzle = player.Rig.MuzzlePosition;

            var info = new DamageInfo();
            info.amount = dmg;
            info.crit = crit;
            info.fromPosition = muzzle;
            info.knockback = 1.5f;

            info.explosive = WeaponMods.Explosive();
            info.explodeRadius = WeaponMods.ExplodeRadius();
            info.explodeMul = WeaponMods.ExplodeMul();
            info.pierce = WeaponMods.Pierce() + Enhancement.PierceBonus(def.id);

            Color trail = TrajectoryColor(def);
            if (Game.Combat != null)
            {
                float speed = def.projectileSpeed > 1f ? def.projectileSpeed : cfg.bulletSpeed;
                float life = def.projectileLife > 0.05f ? def.projectileLife : cfg.bulletLife;

                int pellets = WeaponMods.Pellets();
                if (pellets > 1)
                {
                    // fan the pellets evenly across ~22 degrees
                    float step = 22f / (pellets - 1);
                    float start = -11f;
                    for (int p = 0; p < pellets; p++)
                        Game.Combat.FirePlayerBullet(muzzle, Motion.Rotate(dir, (start + step * p) * Mathf.Deg2Rad),
                            info, speed, life, trail);
                }
                else
                {
                    Game.Combat.FirePlayerBullet(muzzle, dir, info, speed, life, trail);
                }
                Game.Combat.SpawnMuzzleFlash(muzzle, dir, trail);
                AudioManager.PlaySfx(def.fx == "feather" ? "sfx_bow" : "sfx_gun", 0.9f, 0.1f);
            }

            player.AddRecoil(-aim * cfg.recoilKick);
            if (Game.Camera != null) Game.Camera.AddShake(0.06f);
            if (Game.Hud != null) Game.Hud.PulseCrosshair();
        }
    }
}
