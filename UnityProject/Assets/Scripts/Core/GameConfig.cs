using UnityEngine;

namespace Ares
{
    public enum GamePhase { Boot = 0, MainMenu = 1, Playing = 2, Paused = 3, Victory = 4, Defeat = 5 }
    public enum EnemyKind { Melee = 0, Ranged = 1, Boss = 2 }
    public enum WeaponKind { None = 0, Melee = 1, Ranged = 2 }

    /// <summary>
    /// Every tunable number in the game. Feel values, difficulty curve and boss scaling all
    /// live here so the whole game can be re-balanced from one asset (or from the Excel
    /// "Balance" sheet, which writes back into this asset).
    /// </summary>
    [CreateAssetMenu(fileName = "GameConfig", menuName = "Ares/Game Config")]
    public class GameConfig : ScriptableObject
    {
        [Header("--- Player movement ---")]
        public float moveSpeed = 5.2f;
        public float moveAccel = 55f;
        public float moveStopDecel = 70f;
        public float moveTurnDecel = 90f;
        public float playerRadius = 0.34f;

        [Header("--- Dodge roll ---")]
        public float rollDistance = 3.1f;
        public float rollDuration = 0.21f;
        public float rollCooldown = 1.0f;
        public float rollInvulnTime = 0.32f;

        [Header("--- Camera ---")]
        public float cameraSize = 5.9f;
        public float cameraFollowLerp = 8.5f;
        public float cameraAimOffset = 1.2f;
        public float cameraMaxAimOffset = 2.4f;
        public float cameraShakeDecay = 5.5f;
        public float cameraShakeMax = 0.55f;

        [Header("--- Ranged combat ---")]
        public float fireInterval = 0.14f;
        public float bulletSpeed = 19f;
        public float bulletLife = 1.05f;
        public float bulletRadius = 0.12f;
        public float bulletSpreadDeg = 2.2f;
        public float recoilKick = 0.9f;

        [Header("--- Melee combat ---")]
        public float meleeSwingTime = 0.22f;
        public float meleeArcDeg = 110f;
        public float meleeKnockback = 5f;
        public float meleeRecoverTime = 0.1f;

        [Header("--- Combat feel ---")]
        [Range(0f, 1f)] public float critChance = 0.18f;
        public float critMultiplier = 2f;
        public float hitStopTime = 0.045f;
        [Range(0f, 1f)] public float hitStopScale = 0.05f;

        [Header("--- Player health ---")]
        public int playerMaxHp = 120;
        public float playerHurtInvuln = 0.4f;
        public float playerHurtKnockback = 1.2f;

        [Header("--- Melee enemy (触手怪) ---")]
        public int meleeHp = 46;
        public float meleeSpeed = 2.65f;
        public float meleeRadius = 0.42f;
        public float meleeAttackRange = 1.05f;
        public float meleeWindup = 0.45f;
        public float meleeCooldown = 1.15f;
        public int meleeDamage = 10;
        public float meleeEnemyKnockback = 5.5f;

        [Header("--- Ranged enemy (呕吐者) ---")]
        public int rangedHp = 34;
        public float rangedSpeed = 1.9f;
        public float rangedRadius = 0.44f;
        public float rangedIdealDist = 5.6f;
        public float rangedKeepDist = 3.4f;
        public float rangedMaxRange = 9.5f;
        public float rangedWindup = 0.7f;
        public float rangedCooldown = 2.0f;
        public int rangedDamage = 8;
        public float rangedProjectileSpeed = 8.5f;
        public int rangedMaxProjectiles = 40;

        [Header("--- Boss ---")]
        public float bossHpMul = 3.5f;
        public float bossDamageMul = 2.2f;
        public float bossSizeMul = 1.55f;
        public float bossSpeedMul = 0.85f;
        public float bossAttackRange = 1.6f;
        public float bossWindup = 0.6f;
        public float bossCooldown = 1.0f;

        [Header("--- Waves ---")]
        public int wavesPerBoss = 10;
        public int maxDifficultyWave = 30;
        public float hpPerWave = 0.12f;
        public float dmgPerWave = 0.05f;
        public float countPerWave = 0.35f;
        public float waveInterval = 7f;
        public float waveClearBonusTime = 6f;
        public int spawnMaxAliveNormal = 40;
        public float spawnScatter = 0.9f;
        public float stressVisualThreshold = 60f;

        [Header("--- Loot ---")]
        public float pickupRadius = 1.9f;
        public float corpseFadeTime = 1.0f;

        [Header("--- Minimap ---")]
        public float minimapWorldRange = 11f;
        public float minimapRotateLerp = 6f;

        [Header("--- Pressure test ---")]
        public int[] stressCounts = new int[] { 100, 500, 1000 };

        private static GameConfig _runtime;

        public static GameConfig Get()
        {
            if (_runtime != null) return _runtime;
            _runtime = Resources.Load<GameConfig>("GameConfig");
            if (_runtime == null)
            {
                Debug.LogWarning("[Ares] Resources/GameConfig.asset not found, using code defaults.");
                _runtime = CreateInstance<GameConfig>();
            }
            return _runtime;
        }
    }
}
