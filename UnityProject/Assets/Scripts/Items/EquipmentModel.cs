using System;
using UnityEngine;

namespace Ares
{
    /// <summary>
    /// Which inventory slot is currently the melee weapon and which is the ranged weapon,
    /// plus which one is in hand. Weapons are non-stackable items, so durability lives on
    /// the inventory slot and travels with the weapon.
    /// </summary>
    public class EquipmentModel
    {
        public int meleeSlot = -1;
        public int rangedSlot = -1;
        public bool meleeActive;

        public event Action Changed;

        public InventoryModel Inv { get; private set; }

        public void Bind(InventoryModel inv)
        {
            Inv = inv;
            if (Inv != null) Inv.Changed += Refresh;
            Refresh();
        }

        /// <summary>Validates the equipped slots still hold usable weapons and auto-picks new ones.</summary>
        public void Refresh()
        {
            if (Inv == null) return;
            var items = Game.Items;

            if (!IsWeaponInSlot(meleeSlot, true)) meleeSlot = FindWeapon(true);
            if (!IsWeaponInSlot(rangedSlot, false)) rangedSlot = FindWeapon(false);

            if (meleeSlot < 0 && rangedSlot < 0) meleeActive = false;
            else if (meleeSlot < 0) meleeActive = false;
            else if (rangedSlot < 0) meleeActive = true;

            Raise();
        }

        private bool IsWeaponInSlot(int idx, bool melee)
        {
            if (idx < 0 || idx >= InventoryModel.Capacity) return false;
            var s = Inv.Slots[idx];
            if (s.IsEmpty) return false;
            var def = Game.Items != null ? Game.Items.Get(s.itemId) : null;
            if (def == null || !def.IsWeapon) return false;
            return melee ? def.IsMelee : def.IsRanged;
        }

        private int FindWeapon(bool melee)
        {
            for (int i = 0; i < InventoryModel.Capacity; i++)
            {
                var s = Inv.Slots[i];
                if (s.IsEmpty) continue;
                var def = Game.Items != null ? Game.Items.Get(s.itemId) : null;
                if (def == null || !def.IsWeapon) continue;
                if (melee ? def.IsMelee : def.IsRanged) return i;
            }
            return -1;
        }

        public ItemDef ActiveDef
        {
            get
            {
                var slot = ActiveSlot;
                if (slot == null || slot.IsEmpty) return null;
                return Game.Items != null ? Game.Items.Get(slot.itemId) : null;
            }
        }

        public InventorySlot ActiveSlot
        {
            get
            {
                if (Inv == null) return null;
                int idx = meleeActive ? meleeSlot : rangedSlot;
                if (idx < 0 || idx >= InventoryModel.Capacity) return null;
                var s = Inv.Slots[idx];
                return s.IsEmpty ? null : s;
            }
        }

        public int ActiveSlotIndex { get { return meleeActive ? meleeSlot : rangedSlot; } }

        public bool HasWeapon { get { return ActiveSlot != null; } }

        public void Toggle()
        {
            if (meleeActive && rangedSlot >= 0) meleeActive = false;
            else if (!meleeActive && meleeSlot >= 0) meleeActive = true;
            else if (meleeSlot >= 0) meleeActive = true;
            Raise();
        }

        /// <summary>Consumes one point of durability; the weapon breaks (and disappears) at zero.</summary>
        public void WearWeapon()
        {
            var slot = ActiveSlot;
            if (slot == null) return;
            var def = Game.Items != null ? Game.Items.Get(slot.itemId) : null;
            if (def == null || def.durabilityMax <= 0) return;

            slot.durability--;
            if (slot.durability <= 0)
            {
                string name = def.name;
                slot.Clear();
                if (Game.Hud != null) Game.Hud.ShowPrompt(name + " 已损坏", 1.6f);
            }
            Inv.RaiseChanged();
            Raise();
        }

        public void Raise()
        {
            var h = Changed;
            if (h != null) h();
        }
    }
}
