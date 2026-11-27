using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ares
{
    /// <summary>Crafting rules: consume materials from the backpack to produce a new item.</summary>
    public class CraftingSystem : MonoBehaviour
    {
        public event Action Changed;

        public List<RecipeDef> Recipes
        {
            get { return Game.Items != null ? new List<RecipeDef>(Game.Items.Recipes) : new List<RecipeDef>(); }
        }

        public bool CanCraft(RecipeDef r)
        {
            var inv = Game.Inventory;
            if (r == null || inv == null) return false;
            if (!inv.CanAdd(r.output, r.outputCount)) return false;
            if (!string.IsNullOrEmpty(r.input1) && inv.CountOf(r.input1) < r.count1) return false;
            if (!string.IsNullOrEmpty(r.input2) && inv.CountOf(r.input2) < r.count2) return false;
            return true;
        }

        public bool Craft(RecipeDef r)
        {
            if (!CanCraft(r)) return false;
            var inv = Game.Inventory;

            if (!string.IsNullOrEmpty(r.input1) && r.count1 > 0) inv.RemoveCount(r.input1, r.count1);
            if (!string.IsNullOrEmpty(r.input2) && r.count2 > 0) inv.RemoveCount(r.input2, r.count2);

            int added = inv.TryAdd(r.output, r.outputCount);
            if (added <= 0)
            {
                // Roll back the materials if the output could not be stored.
                if (!string.IsNullOrEmpty(r.input1)) inv.TryAdd(r.input1, r.count1);
                if (!string.IsNullOrEmpty(r.input2)) inv.TryAdd(r.input2, r.count2);
                if (Game.Hud != null) Game.Hud.ShowPrompt("背包已满，无法制作", 1.5f);
                return false;
            }

            var def = Game.Items != null ? Game.Items.Get(r.output) : null;
            if (Game.Hud != null) Game.Hud.ShowPrompt("制作成功：" + (def != null ? def.name : r.output), 1.5f);

            var h = Changed;
            if (h != null) h();
            return true;
        }
    }
}
