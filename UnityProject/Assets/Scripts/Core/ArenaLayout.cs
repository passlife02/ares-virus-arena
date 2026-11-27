using System.Collections.Generic;
using UnityEngine;

namespace Ares
{
    /// <summary>Serialised arena description produced by the Scene Builder.</summary>
    public class ArenaLayout : MonoBehaviour
    {
        public Vector2 BoundsMin = new Vector2(-22f, -13f);
        public Vector2 BoundsMax = new Vector2(22f, 13f);
        public float CellSize = 0.5f;
        public List<Rect> Walls = new List<Rect>();
        public Vector2 PlayerStart = Vector2.zero;

        public Rect Bounds
        {
            get { return new Rect(BoundsMin.x, BoundsMin.y, BoundsMax.x - BoundsMin.x, BoundsMax.y - BoundsMin.y); }
        }

        public bool IsInside(Vector2 p)
        {
            return p.x >= BoundsMin.x && p.x <= BoundsMax.x && p.y >= BoundsMin.y && p.y <= BoundsMax.y;
        }
    }
}
