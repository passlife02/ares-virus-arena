using System.Collections.Generic;
using UnityEngine;

namespace Ares
{
    /// <summary>
    /// Owns every projectile and impact effect. Enemies have no colliders: hits resolve against
    /// the spawner's spatial hash, so cost scales with bullets rather than with enemy count.
    /// </summary>
    public class CombatSystem : MonoBehaviour
    {
        public Projectile ProjectilePrefab;
        public HitSpark SparkPrefab;
        public Sprite PlayerBulletSprite;
        public Sprite EnemyBulletSprite;
        public Sprite SparkSprite;
        public Transform PoolRoot;

        private ObjectPool<Projectile> _playerBullets;
        private ObjectPool<Projectile> _enemyBullets;
        private ObjectPool<HitSpark> _sparks;
        private readonly List<Projectile> _enemyActive = new List<Projectile>(64);
        private int _enemyBulletCap = 40;

        private static Material _trailMaterial;

        public static Material TrailMaterial
        {
            get
            {
                if (_trailMaterial != null) return _trailMaterial;
                var shader = Shader.Find("Legacy Shaders/Particles/Additive");
                if (shader == null) shader = Shader.Find("Sprites/Default");
                if (shader == null) shader = Shader.Find("Unlit/Color");
                _trailMaterial = new Material(shader);
                _trailMaterial.name = "AresTrailMat";
                return _trailMaterial;
            }
        }

        public int ActivePlayerBullets { get; private set; }
        public int ActiveEnemyBullets { get { return _enemyActive.Count; } }

        public void Init()
        {
            if (PoolRoot == null)
            {
                var go = new GameObject("ProjectilePool");
                go.transform.SetParent(transform, false);
                PoolRoot = go.transform;
            }
            _playerBullets = new ObjectPool<Projectile>(ProjectilePrefab, PoolRoot, 32);
            _enemyBullets = new ObjectPool<Projectile>(ProjectilePrefab, PoolRoot, 24);
            _sparks = new ObjectPool<HitSpark>(SparkPrefab, PoolRoot, 40);
            _enemyBulletCap = Mathf.Max(4, Game.Cfg.rangedMaxProjectiles);
        }

        public void FirePlayerBullet(Vector2 pos, Vector2 dir, DamageInfo damage, float speed, float life, Color trailColor)
        {
            var p = _playerBullets.Get();
            if (PlayerBulletSprite != null && p.Renderer != null) p.Renderer.sprite = PlayerBulletSprite;
            p.Launch(this, pos, dir, speed, life, Game.Cfg.bulletRadius, damage, true, trailColor);
            ActivePlayerBullets++;
        }

        public void FireEnemyProjectile(Vector2 pos, Vector2 dir, int damage, float speed, float life, float knockback)
        {
            if (_enemyActive.Count >= _enemyBulletCap) return;
            var p = _enemyBullets.Get();
            if (EnemyBulletSprite != null && p.Renderer != null) p.Renderer.sprite = EnemyBulletSprite;
            p.Launch(this, pos, dir, speed, life, 0.24f, DamageInfo.Simple(damage, pos, knockback), false, new Color(0.55f, 1f, 0.35f, 0.85f));
            _enemyActive.Add(p);
        }

        public void ReleaseProjectile(Projectile p, bool spawnedImpact, Vector2 at, Vector2 normal)
        {
            if (!p.Active && !p.gameObject.activeSelf) return;
            if (p.FromPlayer)
            {
                ActivePlayerBullets = Mathf.Max(0, ActivePlayerBullets - 1);
                p.ClearTrail();
                _playerBullets.Release(p);
                if (spawnedImpact) SpawnImpact(at, new Color(1f, 0.72f, 0.3f), 0.9f);
            }
            else
            {
                _enemyActive.Remove(p);
                p.ClearTrail();
                _enemyBullets.Release(p);
                if (spawnedImpact) SpawnImpact(at, new Color(0.55f, 0.95f, 0.35f), 0.8f);
            }
        }

        /// <summary>Bright burst + expanding shockwave so hits read clearly.</summary>
        private float _nextHitSound;

        public void SpawnImpact(Vector2 pos, Color color, float scale)
        {
            if (Time.unscaledTime >= _nextHitSound)
            {
                _nextHitSound = Time.unscaledTime + 0.045f;
                AudioManager.PlaySfx("sfx_hit", 0.5f, 0.18f);
            }

            var s = _sparks.Get();
            if (SparkSprite != null && s.Renderer != null) s.Renderer.sprite = SparkSprite;
            s.Play(pos, color, scale * 0.3f, scale * 2.2f, 0.2f);

            var ring = _sparks.Get();
            if (SparkSprite != null && ring.Renderer != null) ring.Renderer.sprite = SparkSprite;
            Color c = color;
            c.a = 0.55f;
            ring.Play(pos, c, scale * 0.4f, scale * 3.4f, 0.16f);
        }

        /// <summary>Muzzle flare: a couple of fast sparks plus a bright flash at the barrel.</summary>
        public void SpawnMuzzleFlash(Vector2 pos, Vector2 dir, Color color)
        {
            var flash = _sparks.Get();
            if (SparkSprite != null && flash.Renderer != null) flash.Renderer.sprite = SparkSprite;
            flash.Play(pos, new Color(1f, 0.95f, 0.7f, 1f), 0.55f, 0.9f, 0.07f);

            for (int i = 0; i < 2; i++)
            {
                var s = _sparks.Get();
                if (SparkSprite != null && s.Renderer != null) s.Renderer.sprite = SparkSprite;
                Vector2 side = new Vector2(-dir.y, dir.x) * Random.Range(-0.18f, 0.18f);
                s.Play(pos + dir * 0.12f + side, color, 0.25f, 0.75f, 0.12f);
            }
        }

        public void SpawnBlood(Vector2 pos)
        {
            for (int i = 0; i < 4; i++)
            {
                var s = _sparks.Get();
                if (SparkSprite != null && s.Renderer != null) s.Renderer.sprite = SparkSprite;
                Vector2 offset = pos + Random.insideUnitCircle * 0.3f;
                s.Play(offset, new Color(0.78f, 0.1f, 0.1f, 0.95f), 0.14f, 0.5f, 0.34f);
            }
        }

        /// <summary>Melee sweep: a bright crescent stretched along the swing.</summary>
        public void SpawnSlash(Vector2 center, Vector2 dir, float range, Color color)
        {
            var s = _sparks.Get();
            if (SparkSprite != null && s.Renderer != null) s.Renderer.sprite = SparkSprite;
            float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            s.transform.rotation = Quaternion.Euler(0f, 0f, ang);
            s.Play(center + dir * (range * 0.6f), color, 0.5f, range * 1.9f, 0.18f);
        }

        /// <summary>Burst-round detonation: splash damage around the impact point.</summary>
        public void ExplodeAt(Vector2 center, float radius, int damage, EnemyBase ignore)
        {
            SpawnImpact(center, new Color(1f, 0.62f, 0.2f), 1.5f);
            SpiderEgg.HitRadius(center, radius, damage);
            var sp = Game.Spawner;
            if (sp == null) return;
            for (int i = sp.AliveCount - 1; i >= 0; i--)
            {
                var e = sp.Alive[i];
                if (e == null || !e.IsAlive || e == ignore) continue;
                float d = Vector2.Distance(e.Position, center);
                if (d > radius) continue;
                int dmg = Mathf.Max(1, Mathf.RoundToInt(damage * Mathf.Lerp(1f, 0.5f, d / radius)));
                e.TakeDamage(DamageInfo.Simple(dmg, center, 1.6f));
            }
        }

        /// <summary>Spider boss web: a slow projectile that roots the player on contact.</summary>
        public void FireEnemyWeb(Vector2 pos, Vector2 dir, int damage, float speed, float life)
        {
            var go = new GameObject("EnemyWeb");
            go.transform.SetParent(transform, false);
            var web = go.AddComponent<WebProjectile>();
            web.Launch(pos, dir, speed, life, 1.6f);
        }

        public EnemyBase FindEnemyHit(Vector2 point, float radius)
        {
            if (Game.Spawner == null) return null;
            return Game.Spawner.FindEnemyNear(point, radius);
        }

        public void ClearAll()
        {
            if (_enemyActive != null)
            {
                for (int i = _enemyActive.Count - 1; i >= 0; i--)
                    if (_enemyActive[i] != null) _enemyBullets.Release(_enemyActive[i]);
                _enemyActive.Clear();
            }
            ActivePlayerBullets = 0;
            if (PoolRoot != null)
                for (int i = PoolRoot.childCount - 1; i >= 0; i--)
                {
                    var child = PoolRoot.GetChild(i).gameObject;
                    var tr = child.GetComponent<TrailRenderer>();
                    if (tr != null) tr.Clear();
                    child.SetActive(false);
                }
        }
    }
}
