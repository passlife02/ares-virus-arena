using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ares
{
    [Serializable]
    public class InventorySlot
    {
        public string itemId;
        public int count;
        public int durability;

        public bool IsEmpty { get { return string.IsNullOrEmpty(itemId) || count <= 0; } }

        public void Clear()
        {
            itemId = null;
            count = 0;
            durability = 0;
        }
    }

    /// <summary>
    /// 30 slot inventory with per-item stack limits and per-instance weapon durability.
    /// Pure data (no MonoBehaviour) so it can be saved and reasoned about independently.
    /// </summary>
    public class InventoryModel
    {
        public const int Capacity = 30;

        public readonly InventorySlot[] Slots = new InventorySlot[Capacity];
        public event Action Changed;

        public InventoryModel()
        {
            for (int i = 0; i < Capacity; i++) Slots[i] = new InventorySlot();
        }

        public int UsedSlots
        {
            get
            {
                int n = 0;
                for (int i = 0; i < Capacity; i++) if (!Slots[i].IsEmpty) n++;
                return n;
            }
        }

        public int TotalItemCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < Capacity; i++) if (!Slots[i].IsEmpty) n += Slots[i].count;
                return n;
            }
        }

        public int CountOf(string itemId)
        {
            int n = 0;
            for (int i = 0; i < Capacity; i++)
                if (Slots[i].itemId == itemId) n += Slots[i].count;
            return n;
        }

        public int StackMaxOf(string itemId)
        {
            var def = Game.Items != null ? Game.Items.Get(itemId) : null;
            return def != null ? Mathf.Max(1, def.stackMax) : 1;
        }

        public bool CanAdd(string itemId, int count)
        {
            if (string.IsNullOrEmpty(itemId) || count <= 0) return false;
            int stackMax = StackMaxOf(itemId);
            int room = 0;
            for (int i = 0; i < Capacity; i++)
            {
                var s = Slots[i];
                if (s.IsEmpty) room += stackMax;
                else if (s.itemId == itemId) room += Mathf.Max(0, stackMax - s.count);
                if (room >= count) return true;
            }
            return room >= count;
        }

        /// <summary>Tops up existing stacks first, then uses empty slots. Returns stored amount.</summary>
        public int TryAdd(string itemId, int count)
        {
            if (string.IsNullOrEmpty(itemId) || count <= 0) return 0;
            var def = Game.Items != null ? Game.Items.Get(itemId) : null;
            int stackMax = def != null ? Mathf.Max(1, def.stackMax) : 1;
            int durMax = def != null ? def.durabilityMax : 0;
            int remaining = count;

            for (int i = 0; i < Capacity && remaining > 0; i++)
            {
                var s = Slots[i];
                if (s.IsEmpty || s.itemId != itemId) continue;
                int space = stackMax - s.count;
                if (space <= 0) continue;
                int add = Mathf.Min(space, remaining);
                s.count += add;
                remaining -= add;
            }

            for (int i = 0; i < Capacity && remaining > 0; i++)
            {
                var s = Slots[i];
                if (!s.IsEmpty) continue;
                s.itemId = itemId;
                s.count = Mathf.Min(stackMax, remaining);
                s.durability = durMax > 0 ? durMax : 0;
                remaining -= s.count;
            }

            int added = count - remaining;
            if (added > 0) RaiseChanged();
            return added;
        }

        public bool RemoveCount(string itemId, int count)
        {
            if (count <= 0) return true;
            if (CountOf(itemId) < count) return false;
            int left = count;
            for (int i = 0; i < Capacity && left > 0; i++)
            {
                var s = Slots[i];
                if (s.IsEmpty || s.itemId != itemId) continue;
                int take = Mathf.Min(s.count, left);
                s.count -= take;
                left -= take;
                if (s.count <= 0) s.Clear();
            }
            RaiseChanged();
            return true;
        }

        public int IndexOf(string itemId)
        {
            for (int i = 0; i < Capacity; i++)
                if (Slots[i].itemId == itemId && !Slots[i].IsEmpty) return i;
            return -1;
        }

        public void ClearSlot(int index)
        {
            if (index < 0 || index >= Capacity) return;
            Slots[index].Clear();
            RaiseChanged();
        }

        public void Clear()
        {
            for (int i = 0; i < Capacity; i++) Slots[i].Clear();
            RaiseChanged();
        }

        public void RaiseChanged()
        {
            var h = Changed;
            if (h != null) h();
        }

        // ---------------------------------------------------------------- persistence

        public List<InventorySlot> Snapshot()
        {
            var list = new List<InventorySlot>(Capacity);
            for (int i = 0; i < Capacity; i++)
            {
                var s = Slots[i];
                list.Add(new InventorySlot { itemId = s.itemId, count = s.count, durability = s.durability });
            }
            return list;
        }

        public void LoadFrom(IList<InventorySlot> data)
        {
            for (int i = 0; i < Capacity; i++) Slots[i].Clear();
            if (data != null)
            {
                int n = Mathf.Min(Capacity, data.Count);
                for (int i = 0; i < n; i++)
                {
                    var src = data[i];
                    if (src == null || src.IsEmpty) continue;
                    Slots[i].itemId = src.itemId;
                    Slots[i].count = src.count;
                    Slots[i].durability = src.durability;
                }
            }
            RaiseChanged();
        }
    }
}
