using UnityEngine;

namespace Ares
{
    /// <summary>Light weight service locator, keeps wiring explicit and testable.</summary>
    public static class Game
    {
        public static GameConfig Cfg;
        public static GameManager Manager;
        public static PlayerController Player;
        public static PlayerCombat Combat2;
        public static CameraRig Camera;
        public static GridNav Nav;
        public static CombatSystem Combat;
        public static EnemySpawner Spawner;
        public static InventoryModel Inventory;
        public static InventoryModel Stash;
        public static Transform BarrelRoot;
        public static Transform EffectRoot;
        public static EquipmentModel Equipment;
        public static SaveService Save;
        public static ItemDatabase Items;
        public static CraftingSystem Crafting;
        public static HudUI Hud;
        public static MinimapUI Minimap;
        public static EnemyBars EnemyBars;

        public static bool CanPlay
        {
            get { return Manager != null && Manager.Phase == GamePhase.Playing; }
        }

        public static bool UiBlocking
        {
            get
            {
                if (Manager == null) return false;
                if (Manager.Inventory != null && Manager.Inventory.IsOpen) return true;
                return false;
            }
        }

        public static void ResetRuntime()
        {
            Player = null;
            Combat2 = null;
            Nav = null;
            Combat = null;
            Spawner = null;
            Crafting = null;
            Minimap = null;
        }
    }
}
