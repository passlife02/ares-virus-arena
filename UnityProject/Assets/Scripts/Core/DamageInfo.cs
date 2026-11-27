using UnityEngine;

namespace Ares
{
    public interface IDamageable
    {
        bool IsAlive { get; }
        Vector2 Position { get; }
        void TakeDamage(DamageInfo info);
    }

    /// <summary>Everything needed to present one hit: damage, crit flag and knockback.</summary>
    public struct DamageInfo
    {
        public int amount;
        public bool crit;
        public Vector2 fromPosition;
        public float knockback;
        public bool explosive;      // burst mod: detonate on impact
        public float explodeRadius;
        public float explodeMul;
        public int pierce;          // remaining targets this bullet can pass through

        public static DamageInfo Simple(int amount, Vector2 from, float knockback)
        {
            DamageInfo d;
            d.amount = amount;
            d.crit = false;
            d.fromPosition = from;
            d.knockback = knockback;
            d.explosive = false;
            d.explodeRadius = 0f;
            d.explodeMul = 1f;
            d.pierce = 0;
            return d;
        }
    }
}
