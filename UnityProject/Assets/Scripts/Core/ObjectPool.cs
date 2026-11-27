using System.Collections.Generic;
using UnityEngine;

namespace Ares
{
    /// <summary>Simple stack based pool used for projectiles and damage numbers.</summary>
    public class ObjectPool<T> where T : Component
    {
        private readonly T _prefab;
        private readonly Transform _root;
        private readonly Stack<T> _free = new Stack<T>();

        public ObjectPool(T prefab, Transform root, int prewarm = 0)
        {
            _prefab = prefab;
            _root = root;
            for (int i = 0; i < prewarm; i++)
            {
                var item = Object.Instantiate(_prefab, _root);
                item.gameObject.SetActive(false);
                _free.Push(item);
            }
        }

        public T Get()
        {
            T item;
            if (_free.Count > 0)
            {
                item = _free.Pop();
            }
            else
            {
                item = Object.Instantiate(_prefab, _root);
            }
            item.gameObject.SetActive(true);
            return item;
        }

        public void Release(T item)
        {
            if (item == null) return;
            item.gameObject.SetActive(false);
            _free.Push(item);
        }
    }

    /// <summary>Tiny spatial hash used for boid style separation between enemies.</summary>
    public class SpatialHash
    {
        private readonly float _cellSize;
        private readonly Dictionary<long, List<EnemyBase>> _cells = new Dictionary<long, List<EnemyBase>>(512);
        private readonly List<List<EnemyBase>> _recycled = new List<List<EnemyBase>>();

        public SpatialHash(float cellSize)
        {
            _cellSize = Mathf.Max(0.25f, cellSize);
        }

        private long Key(int cx, int cy)
        {
            return ((long)cx << 32) ^ (uint)cy;
        }

        public void Clear()
        {
            foreach (var kv in _cells)
            {
                kv.Value.Clear();
                _recycled.Add(kv.Value);
            }
            _cells.Clear();
        }

        public void Insert(EnemyBase e)
        {
            int cx = Mathf.FloorToInt(e.Position.x / _cellSize);
            int cy = Mathf.FloorToInt(e.Position.y / _cellSize);
            long k = Key(cx, cy);
            List<EnemyBase> list;
            if (!_cells.TryGetValue(k, out list))
            {
                if (_recycled.Count > 0)
                {
                    list = _recycled[_recycled.Count - 1];
                    _recycled.RemoveAt(_recycled.Count - 1);
                }
                else
                {
                    list = new List<EnemyBase>(8);
                }
                _cells[k] = list;
            }
            list.Add(e);
        }

        /// <summary>Fills <paramref name="results"/> with enemies in the 3x3 cell neighbourhood.</summary>
        public void Query(Vector2 pos, List<EnemyBase> results)
        {
            results.Clear();
            int cx = Mathf.FloorToInt(pos.x / _cellSize);
            int cy = Mathf.FloorToInt(pos.y / _cellSize);
            for (int y = cy - 1; y <= cy + 1; y++)
            {
                for (int x = cx - 1; x <= cx + 1; x++)
                {
                    List<EnemyBase> list;
                    if (_cells.TryGetValue(Key(x, y), out list))
                    {
                        results.AddRange(list);
                    }
                }
            }
        }
    }
}
