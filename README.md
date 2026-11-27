# Ares Virus Arena · 2D 俯视角波次生存射击（Unity）

个人练习项目：把《阿瑞斯病毒》那套「俯视角 + 物资 + 波次」的玩法，用 Unity 2022.3 / Built-in 管线复刻了一个可完整通关的竞技场版本。

> 本仓库**不含** Windows 构建产物与个人简历。构建包体积超过 GitHub 单文件 100 MB 上限，
> 如需试玩请用 Release 附件分发，或本地用 Unity 2022.3 打开 `UnityProject/` 直接运行。

## 玩法

- **波次生存**：1 / 5 / 10 / 20 / 30 / 无尽；敌人血量 +12%/波、伤害 +5%/波，第 30 波封顶
- **近战蓄力**：长按左键 0.9 秒，伤害 1.0~1.5 倍、扇形范围随蓄力变远；松手即普通攻击
- **远程与投掷**：火枪 / 铁皮弓（不同弹道）、手榴弹（弹射 + 对自己也有 70% 伤害）、木栅栏
- **两种 BOSS**：穿山甲（冲撞 + 落地震波）、蜘蛛（吐网 + 产可击碎的卵）
- **成长**：武器改造 / 装备强化双线各 10 级、铁匠打造与修理、商人交易与救济、里程碑、图鉴

## 工程结构

```
UnityProject/
  Assets/
    Scripts/
      Core/       GameManager Game GameConfig MapVariants MapTransition Trailer
                  AudioManager InputService Motion GridNav ArenaLayout Weather WaterSurface SaveService
      Player/     PlayerController PlayerCombat PlayerRig MeleeArcView
      Combat/     CombatSystem Projectile Grenade Barrel Fence WebProjectile SheetFx WarnPip Fx
      Enemies/    EnemySpawner EnemyBase MeleeEnemy RangedEnemy BossEnemy SpiderEgg EnemyBars
      Items/      ItemDatabase ItemDef InventoryModel EquipmentModel CraftingSystem LootPickup
                  WeaponMods Enhancement Armors
      UI/         HudUI MenuUI ShelterUI InventoryUI CodexUI MinimapUI LoreUI StoryUI UiFactory ...
    Editor/       AresSetup（场景生成 + 打包脚本）
    Resources/    Config(JSON) Audio Art UI Map Shader Trailer ...
docs/             设计说明 / 核心类图 / 配置说明 / 演示视频
```

## 值得一看的技术点

| 主题 | 做法 | 位置 |
|---|---|---|
| 地图切换不卡顿 | 启动时构建 5 套布局，运行期只做 `SetActive` + 重烤导航（实测 0~13 ms） | `Core/MapVariants.cs` |
| 切图不卡角色 | 黑屏过场期间完成换图与重生，避免落进新碰撞体 | `Core/MapTransition.cs` |
| AI 不绕圈僵死 | 网格 A* + 三档恢复：贴墙侧移 / 卡死跟随同伴 / 绕圈 3.5 s 走地图中心 | `Core/GridNav.cs`、`Enemies/EnemyBase.cs` |
| 千级实体 UI 开销 | 血条只对已受伤目标显示，上限 24 条 | `UI/EnemyBars.cs` |
| 全代码构建 UI | 不依赖 prefab，浮窗按屏幕边界钳制、统一的悬停/按压反馈 | `UI/UiFactory.cs`、`UI/UiPressFx.cs` |
| 水面着色器 | 双噪声 UV 扰动 + 采样差分近似高光 + 波带 | `Resources/Shaders/WaterSurface.shader` |
| 配置管线 | Excel → JSON（JsonUtility）→ 运行时数据库 | `Editor/AresSetup.cs`、`Items/ItemDatabase.cs` |
| 存档 | SQLite 单文件，覆盖背包/仓库/耐久/改造等级/里程碑 | `Core/SaveService.cs` |

## 操作

`WASD` 移动 · 左键攻击（近战可长按蓄力）· `空格` 翻滚 · `Q` 手榴弹 · `E` 栅栏 · `F` 拾取 · `Tab` 背包 · `R`/右键 换武器 · `Esc` 暂停

## 打开的步骤

1. Unity Hub 用 **2022.3.x** 打开 `UnityProject/`
2. 首次打开会自动导入；若无场景，菜单 **Tools ▸ Ares ▸ Build Scene** 生成
3. 修改数值：编辑 `docs/GameConfig.xlsx` → 菜单 **Tools ▸ Ares ▸ Import Excel Config**

## 说明

- 工程内美术/音频素材来自购买或授权素材包，**请勿直接商用或再分发**（详见 `NOTICE.md`）
- 玩法参考《阿瑞斯病毒》，本项目仅用于学习与技术展示
