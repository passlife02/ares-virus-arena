using System;

namespace Ares
{
    [Serializable]
    public class ItemDef
    {
        public string id;
        public string name;
        public string icon;
        public int stackMax = 1;
        public string category = "misc";
        public string desc = "";

        // --- weapon fields (0 = plain material) ---
        public int weaponKind;          // 0 none, 1 melee, 2 ranged
        public int damage;
        public float attackInterval = 0.3f;
        public float range = 1.2f;      // melee reach / unused for ranged
        public int durabilityMax;       // melee only, 0 = indestructible
        public string ammoItemId = "";
        public int ammoPerShot = 1;
        public float projectileSpeed = 19f;
        public float projectileLife = 1.05f;
        public int healAmount;          // >0 = usable consumable
        public string fx = "";          // "fire" / "feather" - projectile tracer style

        public bool IsWeapon { get { return weaponKind != 0; } }
        public bool IsMelee { get { return weaponKind == 1; } }
        public bool IsRanged { get { return weaponKind == 2; } }
        public bool UsesAmmo { get { return IsRanged && !string.IsNullOrEmpty(ammoItemId); } }
    }

    [Serializable]
    public class ItemList
    {
        public ItemDef[] items;
    }

    [Serializable]
    public class DropEntry
    {
        public string enemy;   // "Melee" / "Ranged" / "Boss"
        public string item;
        public float chance;
        public int min;
        public int max;
    }

    [Serializable]
    public class DropTable
    {
        public DropEntry[] drops;
    }

    [Serializable]
    public class RecipeDef
    {
        public string id;
        public string output;
        public int outputCount = 1;
        public string input1;
        public int count1;
        public string input2;
        public int count2;
        public string category = "武器";
    }

    [Serializable]
    public class RecipeList
    {
        public RecipeDef[] recipes;
    }
}
