using System.Collections.Generic;
using UnityEngine;

namespace Ares
{
    /// <summary>
    /// Grid navigation built specifically for the pressure-test requirement.
    ///
    /// A single breadth-first "flow field" is integrated outwards from the player and
    /// shared by every enemy, instead of running one A* per agent. Cost is therefore
    /// O(cells) per repath and independent of the number of enemies, which is what makes
    /// 1000 simultaneous agents affordable. Short obstacles are still avoided locally
    /// through the flow direction plus wall sliding in <see cref="Motion"/>.
    /// </summary>
    public class GridNav
    {
        public readonly Rect Bounds;
        public readonly float CellSize;
        public readonly int Width;
        public readonly int Height;

        private readonly bool[] _blocked;
        private readonly int[] _dist;
        private readonly Vector2[] _flow;
        private readonly int[] _queue;

        public const int Unreachable = int.MaxValue;
        public int BlockedCells { get; private set; }
        public bool Ready { get; private set; }
        public Vector2 Target => _target;
        private Vector2 _target;

        public GridNav(Rect bounds, float cellSize)
        {
            Bounds = bounds;
            CellSize = cellSize;
            Width = Mathf.CeilToInt(bounds.width / cellSize);
            Height = Mathf.CeilToInt(bounds.height / cellSize);
            _blocked = new bool[Width * Height];
            _dist = new int[Width * Height];
            _flow = new Vector2[Width * Height];
            _queue = new int[Width * Height];
        }

        public int CellCount { get { return Width * Height; } }

        /// <summary>Rasterises the wall rectangles (inflated by the agent radius) into the grid.</summary>
        public void Bake(List<Rect> walls, float agentRadius)
        {
            BlockedCells = 0;
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    Vector2 c = CellCenter(x, y);
                    bool blocked = false;
                    for (int i = 0; i < walls.Count; i++)
                    {
                        Rect r = walls[i];
                        r.xMin -= agentRadius;
                        r.xMax += agentRadius;
                        r.yMin -= agentRadius;
                        r.yMax += agentRadius;
                        if (r.Contains(c)) { blocked = true; break; }
                    }
                    _blocked[y * Width + x] = blocked;
                    if (blocked) BlockedCells++;
                }
            }
            // Border cells are always blocked.
            for (int x = 0; x < Width; x++)
            {
                _blocked[0 * Width + x] = true;
                _blocked[(Height - 1) * Width + x] = true;
            }
            for (int y = 0; y < Height; y++)
            {
                _blocked[y * Width + 0] = true;
                _blocked[y * Width + Width - 1] = true;
            }
            Ready = true;
        }

        public Vector2 CellCenter(int x, int y)
        {
            return new Vector2(Bounds.xMin + (x + 0.5f) * CellSize, Bounds.yMin + (y + 0.5f) * CellSize);
        }

        public bool WorldToCell(Vector2 p, out int cx, out int cy)
        {
            cx = Mathf.FloorToInt((p.x - Bounds.xMin) / CellSize);
            cy = Mathf.FloorToInt((p.y - Bounds.yMin) / CellSize);
            return cx >= 0 && cy >= 0 && cx < Width && cy < Height;
        }

        public bool IsBlocked(int cx, int cy)
        {
            if (cx < 0 || cy < 0 || cx >= Width || cy >= Height) return true;
            return _blocked[cy * Width + cx];
        }

        public bool IsWalkable(Vector2 p)
        {
            int cx, cy;
            if (!WorldToCell(p, out cx, out cy)) return false;
            return !_blocked[cy * Width + cx];
        }

        /// <summary>Rebuilds the distance field and the per cell flow vectors.</summary>
        public void Recompute(Vector2 target)
        {
            if (!Ready) return;
            _target = target;

            int scx, scy;
            if (!WorldToCell(target, out scx, out scy)) return;
            if (IsBlocked(scx, scy))
            {
                // Player is hugging a wall: snap the source to the nearest open cell.
                if (!FindNearestOpen(scx, scy, out scx, out scy)) return;
            }

            int n = Width * Height;
            for (int i = 0; i < n; i++) { _dist[i] = Unreachable; _flow[i] = Vector2.zero; }

            int head = 0, tail = 0;
            int start = scy * Width + scx;
            _dist[start] = 0;
            _queue[tail++] = start;

            while (head < tail)
            {
                int idx = _queue[head++];
                int x = idx % Width;
                int y = idx / Width;
                int d = _dist[idx];

                for (int k = 0; k < 4; k++)
                {
                    int nx = x + (k == 0 ? 1 : k == 1 ? -1 : 0);
                    int ny = y + (k == 2 ? 1 : k == 3 ? -1 : 0);
                    if (nx < 0 || ny < 0 || nx >= Width || ny >= Height) continue;
                    int ni = ny * Width + nx;
                    if (_blocked[ni]) continue;
                    if (_dist[ni] != Unreachable) continue;
                    _dist[ni] = d + 1;
                    _queue[tail++] = ni;
                }
            }

            // Derive a 8-direction flow by descending the discrete distance field.
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    int idx = y * Width + x;
                    if (_blocked[idx]) continue;
                    int best = _dist[idx];
                    int bx = x, by = y;
                    for (int oy = -1; oy <= 1; oy++)
                    {
                        for (int ox = -1; ox <= 1; ox++)
                        {
                            if (ox == 0 && oy == 0) continue;
                            int nx = x + ox, ny = y + oy;
                            if (nx < 0 || ny < 0 || nx >= Width || ny >= Height) continue;
                            int ni = ny * Width + nx;
                            if (_blocked[ni]) continue;
                            if (_dist[ni] < best)
                            {
                                best = _dist[ni];
                                bx = nx; by = ny;
                            }
                        }
                    }
                    if (bx != x || by != y)
                    {
                        Vector2 dir = CellCenter(bx, by) - CellCenter(x, y);
                        _flow[idx] = dir.normalized;
                    }
                }
            }
        }

        private bool FindNearestOpen(int cx, int cy, out int ox, out int oy)
        {
            for (int r = 1; r < 12; r++)
            {
                for (int y = cy - r; y <= cy + r; y++)
                {
                    for (int x = cx - r; x <= cx + r; x++)
                    {
                        if (x < 0 || y < 0 || x >= Width || y >= Height) continue;
                        if (!_blocked[y * Width + x]) { ox = x; oy = y; return true; }
                    }
                }
            }
            ox = cx; oy = cy;
            return false;
        }

        /// <summary>Samples the flow direction and remaining grid distance at a world position.</summary>
        public bool Sample(Vector2 pos, out Vector2 dir, out int distance)
        {
            dir = Vector2.zero;
            distance = Unreachable;
            int cx, cy;
            if (!WorldToCell(pos, out cx, out cy)) return false;
            int idx = cy * Width + cx;
            if (_blocked[idx]) return false;
            distance = _dist[idx];
            dir = _flow[idx];
            return true;
        }

        public bool TryRandomOpenPoint(System.Random rng, int minDistanceFromTarget, out Vector2 point)
        {
            point = Vector2.zero;
            for (int tries = 0; tries < 256; tries++)
            {
                int x = rng.Next(1, Width - 1);
                int y = rng.Next(1, Height - 1);
                int idx = y * Width + x;
                if (_blocked[idx]) continue;
                if (_dist[idx] != Unreachable && _dist[idx] < minDistanceFromTarget) continue;
                point = CellCenter(x, y);
                return true;
            }
            return false;
        }

        public Vector2 NearestOpenPoint(Vector2 p)
        {
            int cx, cy;
            if (!WorldToCell(p, out cx, out cy)) return p;
            int ox, oy;
            if (FindNearestOpen(cx, cy, out ox, out oy)) return CellCenter(ox, oy);
            return p;
        }

        public void DebugDraw()
        {
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    if (_blocked[y * Width + x]) continue;
                    Vector2 c = CellCenter(x, y);
                    Vector2 f = _flow[y * Width + x];
                    if (f.sqrMagnitude > 0.01f)
                    {
                        Debug.DrawLine(c, c + f * 0.35f, new Color(0f, 1f, 0.6f, 0.35f));
                    }
                }
            }
        }
    }
}
