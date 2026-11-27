using UnityEngine;

namespace Ares
{
    /// <summary>
    /// Circle based kinematic movement with wall sliding, depenetration and local avoidance.
    /// No Rigidbody2D / Collider2D is used by any character: movement is fully deterministic
    /// and cannot tunnel, which is what fixes the "stuck on a wall" reports.
    /// </summary>
    public static class Motion
    {
        public const int MaxIterations = 3;
        private static readonly Collider2D[] _overlap = new Collider2D[8];

        public static int WallMask
        {
            get { return LayerMask.GetMask("Wall"); }
        }

        /// <summary>Moves <paramref name="pos"/> by <paramref name="delta"/>, sliding along walls.</summary>
        public static Vector2 MoveCircle(Vector2 pos, Vector2 delta, float radius, int wallMask, out bool hitWall)
        {
            hitWall = false;
            for (int i = 0; i < MaxIterations; i++)
            {
                float dist = delta.magnitude;
                if (dist < 1e-4f) break;

                Vector2 dir = delta / dist;
                RaycastHit2D hit = Physics2D.CircleCast(pos, radius, dir, dist, wallMask);
                if (hit.collider == null)
                {
                    pos += delta;
                    break;
                }

                hitWall = true;
                float travel = Mathf.Max(0f, hit.distance - 0.01f);
                pos += dir * travel;
                Vector2 remaining = dir * (dist - travel);
                Vector2 tangent = remaining - Vector2.Dot(remaining, hit.normal) * hit.normal;
                delta = tangent;
            }
            return pos;
        }

        /// <summary>
        /// Pushes the circle out of any collider it is already overlapping. Called every frame
        /// by every agent so a knockback or a spawn can never leave a character embedded in a wall.
        /// </summary>
        public static Vector2 Depenetrate(Vector2 pos, float radius, int wallMask)
        {
            int count = Physics2D.OverlapCircleNonAlloc(pos, radius * 0.98f, _overlap, wallMask);
            for (int i = 0; i < count; i++)
            {
                var col = _overlap[i];
                if (col == null) continue;
                Vector2 closest = col.ClosestPoint(pos);
                Vector2 away = pos - closest;
                float d = away.magnitude;
                if (d < 1e-4f)
                {
                    // Dead centre inside the collider: escape along the collider's own normal.
                    Vector2 c = col.bounds.center;
                    away = pos - c;
                    if (away.sqrMagnitude < 1e-4f) away = Vector2.up;
                    d = 0.0001f;
                }
                float push = radius - d;
                if (push > 0f)
                {
                    pos += away / d * (push + 0.005f);
                }
            }
            return pos;
        }

        /// <summary>
        /// Fan-of-rays avoidance. Returns a direction close to <paramref name="desired"/> that is
        /// not pointing straight into a wall. Keeps the flow field honest around corners.
        /// </summary>
        public static Vector2 WallAvoidance(Vector2 pos, Vector2 desired, float radius, int wallMask)
        {
            if (desired.sqrMagnitude < 0.0001f) return desired;
            Vector2 dir = desired.normalized;
            float probe = radius + 0.55f;

            RaycastHit2D centre = Physics2D.CircleCast(pos, radius * 0.9f, dir, probe, wallMask);
            if (centre.collider == null) return desired;

            // Try progressively wider side steps, alternating left / right.
            float[] angles = new float[] { 30f, -30f, 55f, -55f, 80f, -80f };
            for (int i = 0; i < angles.Length; i++)
            {
                Vector2 side = Rotate(dir, angles[i] * Mathf.Deg2Rad);
                if (Physics2D.CircleCast(pos, radius * 0.9f, side, probe * 0.85f, wallMask).collider == null)
                {
                    return side;
                }
            }

            // Fully boxed in: slide along the wall instead of pressing into it.
            Vector2 n = centre.normal;
            Vector2 tangent = new Vector2(-n.y, n.x);
            if (Vector2.Dot(tangent, dir) < 0f) tangent = -tangent;
            return tangent;
        }

        public static Vector2 Rotate(Vector2 v, float rad)
        {
            float c = Mathf.Cos(rad), s = Mathf.Sin(rad);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        public static bool HasLineOfSight(Vector2 a, Vector2 b, int wallMask)
        {
            Vector2 d = b - a;
            float len = d.magnitude;
            if (len < 0.01f) return true;
            return Physics2D.Raycast(a, d / len, len, wallMask).collider == null;
        }
    }
}
