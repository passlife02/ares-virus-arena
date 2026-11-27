using UnityEngine;

namespace Ares
{
    /// <summary>
    /// Spider boss egg: it now has health (60% of a normal infected) and can be destroyed before it
    /// hatches. Small spiders that come out are plain melee enemies, so they never lay eggs again.
    /// </summary>
    public class SpiderEgg : MonoBehaviour
    {
        public float hatchTime = 12f;

        private float _t;
        private SpriteRenderer _sr;
        private readonly Vector3 _baseScale = Vector3.one * 0.45f;
        private int _hp = 1;
        private int _maxHp = 1;
        private bool _dead;

        /// <summary>60% of a normal melee infected's health, per feedback.</summary>
        private static int EggHp()
        {
            var cfg = Game.Cfg;
            int baseHp = cfg != null ? cfg.meleeHp : 46;
            return Mathf.Max(4, Mathf.RoundToInt(baseHp * 0.6f));
        }

        public static SpiderEgg Lay(Vector2 pos, float seconds)
        {
            var root = Game.BarrelRoot;
            if (root == null) return null;

            var go = new GameObject("SpiderEgg");
            go.transform.SetParent(root, false);
            go.transform.position = new Vector3(pos.x, pos.y, 0f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Fx.Get("fx_egg");
            sr.sortingOrder = -180;
            sr.color = new Color(1f, 1f, 1f, 0.95f);

            var egg = go.AddComponent<SpiderEgg>();
            egg._sr = sr;
            egg.hatchTime = seconds;
            egg._maxHp = EggHp();
            egg._hp = egg._maxHp;
            go.transform.localScale = egg._baseScale;
            return egg;
        }

        private void Update()
        {
            if (_dead) return;

            _t += Time.deltaTime;
            float k = Mathf.Clamp01(_t / Mathf.Max(0.1f, hatchTime));

            float pulse = 1f + Mathf.Sin(Time.time * (4f + k * 10f)) * (0.03f + k * 0.06f);
            transform.localScale = _baseScale * pulse;
            if (_sr != null)
            {
                var c = _sr.color;
                c.a = 0.6f + 0.35f * k;
                _sr.color = c;
            }

            if (_t >= hatchTime) Hatch();
        }

        /// <summary>Damages every egg inside a circular blast.</summary>
        public static void HitRadius(Vector2 centre, float radius, int damage)
        {
            var all = Object.FindObjectsOfType<SpiderEgg>();
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == null || all[i]._dead) continue;
                if (Vector2.Distance(all[i].transform.position, centre) > radius) continue;
                all[i].TakeHit(damage);
            }
        }

        /// <summary>Damages eggs inside a melee cone (same convention as the enemy hits).</summary>
        public static void HitArc(Vector2 centre, Vector2 dir, float range, float halfAngleDeg, int damage)
        {
            var all = Object.FindObjectsOfType<SpiderEgg>();
            for (int i = 0; i < all.Length; i++)
            {
                var egg = all[i];
                if (egg == null || egg._dead) continue;
                Vector2 d = (Vector2)egg.transform.position - centre;
                if (d.magnitude > range + 0.35f) continue;
                if (d.sqrMagnitude < 0.0001f) { egg.TakeHit(damage); continue; }
                float ang = Vector2.Angle(dir, d.normalized);
                if (ang <= halfAngleDeg) egg.TakeHit(damage);
            }
        }

        /// <summary>Called by melee arcs, bullets and explosions.</summary>
        public void TakeHit(int damage)
        {
            if (_dead) return;
            _hp -= Mathf.Max(1, damage);
            if (Game.Hud != null)
                Game.Hud.ShowDamageStyled(transform.position + Vector3.up * 0.3f, Mathf.Max(1, damage), HudUI.DmgNormal);
            AudioManager.PlaySfx("sfx_hit", 0.4f, 0.2f);

            if (_hp <= 0) Destroyed();
        }

        private void Destroyed()
        {
            _dead = true;
            Fx.Play("fx_web", transform.position, 0.9f, 0.35f, 0f, new Color(1f, 1f, 1f, 0.9f), 60);
            AudioManager.PlaySfx("sfx_hit", 0.55f, 0.3f);
            if (Game.Hud != null) Game.Hud.ShowPrompt("蜘蛛卵被击碎", 1.2f);
            Destroy(gameObject);
        }

        private void Hatch()
        {
            _dead = true;
            var sp = Game.Spawner;
            if (sp != null)
            {
                // 30% of a normal infected's health and damage, smaller body
                sp.Spawn(EnemyKind.Melee, 0.3f, 0.3f, false);
                var child = sp.LastSpawned;
                if (child != null)
                {
                    child.Teleport((Vector2)transform.position);
                    child.transform.localScale = Vector3.one * 0.7f;
                }
            }
            Fx.Play("fx_web", transform.position, 1.1f, 0.45f);
            AudioManager.PlaySfx("sfx_hit", 0.45f, 0.25f);
            Destroy(gameObject);
        }
    }
}
