using System.Reflection;
using MonoMod.RuntimeDetour;
using Newtonsoft.Json;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.Achievements;
using Terraria.GameContent.Drawing;
using Terraria.ID;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.Hooks;
using Microsoft.Xna.Framework;

namespace ShimmerItemReplace;

/// <summary>
/// 微光物品替换插件 —— 自定义微光转换与分解（带物品飞行动画）
/// </summary>
[ApiVersion(2, 1)]
public class ShimmerItemReplace : TerrariaPlugin
{
    #region 插件基本信息
    public override string Name => "ShimmerItemReplace";
    public override string Author => "yu、羽学";
    public override Version Version => new(1, 3, 0);
    public override string Description => "自定义微光转换与分解,适配Terraira1458";
    #endregion

    #region 钩子、进度数组与默认值
    private Hook? canShimmerHook;
    private Hook? getShimmeredHook;
    /// <summary> 保存原版微光转换数组，用于重置 </summary>
    public static readonly int[] DefaultShimmerTransformToItem = (int[])ItemID.Sets.ShimmerTransformToItem.Clone();
    /// <summary> 自定义进度检查委托数组，对应每个物品类型 </summary>
    internal static Func<bool>?[] CanShimmerFuncs = new Func<bool>[ItemID.Count];
    /// <summary> 进度条件委托集合（按索引对应配置中的进度值） </summary>
    internal static Func<bool>[] DownedFuncs =
    [
        () => true,                                     // 0  无
        () => NPC.downedSlimeKing,                      // 1  史莱姆王
        () => NPC.downedBoss1,                          // 2  克眼
        () => NPC.downedBoss2,                          // 3  世吞 / 克脑
        () => NPC.downedQueenBee,                       // 4  蜂王
        () => NPC.downedBoss3,                          // 5  骷髅王
        () => NPC.downedDeerclops,                      // 6  鹿角怪
        () => Main.hardMode,                            // 7  困难模式（肉山）
        () => NPC.downedQueenSlime,                     // 8  史莱姆皇后
        () => NPC.downedMechBossAny,                    // 9  任意机械BOSS
        () => NPC.downedMechBoss1,                      // 10 毁灭者
        () => NPC.downedMechBoss2,                      // 11 双子魔眼
        () => NPC.downedMechBoss3,                      // 12 机械骷髅王
        () => NPC.downedPlantBoss,                      // 13 世纪之花
        () => NPC.downedGolemBoss,                      // 14 石巨人
        () => NPC.downedFishron,                        // 15 猪鲨
        () => NPC.downedEmpressOfLight,                 // 16 光女
        () => NPC.downedAncientCultist,                 // 17 教徒
        () => NPC.downedTowerSolar,                     // 18 日耀柱
        () => NPC.downedTowerNebula,                    // 19 星云柱
        () => NPC.downedTowerVortex,                    // 20 星旋柱
        () => NPC.downedTowerStardust,                  // 21 星尘柱
        () => NPC.downedMoonlord,                       // 22 月亮领主
        () => NPC.downedHalloweenTree,                  // 23 哀木
        () => NPC.downedHalloweenKing,                  // 24 南瓜王
        () => NPC.downedChristmasTree,                  // 25 常绿尖叫怪
        () => NPC.downedChristmasSantank,               // 26 圣诞坦克
        () => NPC.downedChristmasIceQueen,              // 27 冰雪女王
        () => NPC.downedTowers,                         // 28 四柱
        () => NPC.downedClown,                          // 29 小丑
        () => NPC.downedGoblins,                        // 30 哥布林入侵
        () => NPC.downedPirates,                        // 31 海盗入侵
        () => NPC.downedMartians                        // 32 火星暴乱
    ];
    #endregion

    #region 构造函数、初始化与资源释放
    public ShimmerItemReplace(Main game) : base(game)
    {
        try
        {
            if (!Directory.Exists(TShock.SavePath))
                Directory.CreateDirectory(TShock.SavePath);

            string path = Config.path;
            if (!File.Exists(path))
                File.WriteAllText(path, JsonConvert.SerializeObject(new Config(), Formatting.Indented));

            // 首次启动读取配置（包含 Recipe）
            LoadConfig();
            MyCmd.ApplyConfig();

            // 注册重载事件
            GeneralHooks.ReloadEvent += ReloadConfig;

            Console.WriteLine("当前自定义转换规则数:{0}", config.Replace.Length);
            Console.WriteLine("当前自定义分解规则数:{0}", config.Recipe.Length);
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(ex.ToString());
            Console.ResetColor();
            Console.ReadKey();
        }
    }

    public override void Initialize()
    {
        // 注册命令
        Commands.ChatCommands.Add(new Command(config.CommandPermission, MyCmd.SircCmd, config.CommandNames));

        // 钩住 Item.CanShimmer
        var method = typeof(Item).GetMethod("CanShimmer", Type.EmptyTypes);
        if (method == null) throw new InvalidOperationException("Cannot find Item.CanShimmer method");
        var methodinfo = typeof(ShimmerItemReplace).GetMethod("CanShimmer", BindingFlags.Public | BindingFlags.Static);
        canShimmerHook = new Hook(method, methodinfo!);

        // 钩住 WorldItem.GetShimmered
        var method2 = typeof(WorldItem).GetMethod("GetShimmered", Type.EmptyTypes);
        if (method2 == null) throw new InvalidOperationException("Cannot find WorldItem.GetShimmered method");
        var methodinfo2 = typeof(ShimmerItemReplace).GetMethod("GetShimmered", BindingFlags.Public | BindingFlags.Static);
        getShimmeredHook = new Hook(method2, methodinfo2!);

        // 注册游戏更新事件（驱动动画）
        ServerApi.Hooks.GameUpdate.Register(this, OnGameUpdate);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // 移除命令
            Commands.ChatCommands.RemoveAll(x => x.CommandDelegate == MyCmd.SircCmd);
            canShimmerHook?.Dispose();
            getShimmeredHook?.Dispose();
            GeneralHooks.ReloadEvent -= ReloadConfig;
            ServerApi.Hooks.GameUpdate.Deregister(this, OnGameUpdate);
        }
        base.Dispose(disposing);
    }
    #endregion

    #region 配置加载与重载
    internal static Config config = new();
    /// <summary> 服务器 /reload 事件处理 </summary>
    private static void ReloadConfig(ReloadEventArgs args = null!)
    {
        LoadConfig();
        MyCmd.Reload(args?.Player, detailed: false);
        args?.Player.SendInfoMessage("[ShimmerItemReplace] 重新加载配置完毕。");
    }

    /// <summary> 读取配置文件并写入默认备注 </summary>
    private static void LoadConfig()
    {
        config = Config.Read();
        ShowMess();
        config.Write();
    }
    #endregion

    #region 游戏更新事件
    /// <summary> 每帧更新，用于驱动自定义分解动画系统 </summary>
    public static long frame = 0; // 帧计数器（驱动动画）
    private void OnGameUpdate(EventArgs args)
    {
        frame++;
        Animations.Update();
    }
    #endregion

    #region 钩子替换方法（核心逻辑）
    /// <summary> 替换 Item.CanShimmer，加入自定义规则检查 </summary>
    public static bool CanShimmer(Item item)
    {
        int type = item.type;

        // 自定义转换规则
        var trans = config.Replace.FirstOrDefault(r => r.srcType == type);
        if (trans != null)
        {
            if (trans.clear) return false;
            return CheckProgress(trans.progress);
        }

        // 自定义分解规则：只检查进度和堆叠，图格条件在服务端检查
        var dec = config.Recipe.FirstOrDefault(r => r.createItem.type == type);
        if (dec != null)
        {
            if (!CheckProgress(dec.progress)) return false;
            return item.stack >= dec.createItem.stack;
        }

        // 回退原版逻辑
        var func = CanShimmerFuncs[type];
        if (func is not null)
            return func();

        int equiv = item.GetShimmerEquivalentType();
        if (ShimmerTransforms.IsItemTransformLocked(equiv))
            return false;
        if (equiv == ItemID.SlimeCrown && Main.slimeRain)
            return false;

        int target = ShimmerTransforms.GetTransformToItem(equiv);
        if (target <= 0 && !ShimmerTransforms.IsItemDecraftableAndIsDecraftUnlocked(item) && !ItemID.Sets.CommonCoin[equiv])
            return item.makeNPC > 0;

        return true;
    }

    /// <summary> 替换 WorldItem.GetShimmered，执行自定义转换/分解或回退原版 </summary>
    public static void GetShimmered(WorldItem item)
    {
        int type = item.type;
        Vector2 from = item.Center;
        Vector2 to = from - new Vector2(0, 120); // 上升 7.5 格

        // ---- 1. 硬币特殊处理 ----
        if (ItemID.Sets.CommonCoin[type])
        {
            ApplyCoinTransform(item);
            return;
        }

        // ---- 2. 凝胶气球 → 彩虹史莱姆 ----
        if (type == ItemID.GelBalloon)
        {
            if (!NPC.unlockedSlimeRainbowSpawn)
                SpawnRainbowSlime(item);
            return;
        }

        // ---- 3. 史莱姆王冠 → 史莱姆雨 ----
        if (type == ItemID.SlimeCrown)
        {
            if (!Main.slimeRain)
                StartSlimeRain(item);
            return;
        }

        // ---- 4. 自定义转换（1:1 替换） ----
        var trans = config.Replace.FirstOrDefault(r => r.srcType == type);
        if (trans != null && !trans.clear && CheckProgress(trans.progress) && trans.destType > 0 && trans.destType != type)
        {
            ConvertToItem(item, trans.destType);
            return;
        }

        // ---- 5. 自定义分解（带动画） ----
        var dec = config.Recipe.FirstOrDefault(r => r.createItem.type == type);
        if (dec != null && CheckProgress(dec.progress))
        {
            TryCustomDecraft(item, dec, from, to); // 执行分解
            return; // 无论成功失败，直接返回
        }

        // ---- 6. 原版 NPC 转换 ----
        int makeNPC = item.inner.makeNPC;
        if (makeNPC > 0)
        {
            ConvertToNPC(item, makeNPC);
            return;
        }

        // ---- 7. 原版物品转换 ----
        int equiv = item.inner.GetShimmerEquivalentType();
        int target = ShimmerTransforms.GetTransformToItem(equiv);
        if (target > 0)
        {
            ConvertToItem(item, target);
            return;
        }

        // ---- 8. 原版分解（带动画） ----
        int decraftIdx = ShimmerTransforms.GetDecraftingRecipeIndex(equiv);
        if (decraftIdx >= 0)
        {
            if (TryOriginalDecraftWithAnim(item, decraftIdx, from, to))
                return;
        }

        // ---- 9. 无任何转换 ----
        item.TurnToAir();
        PostProcess(item);
    }
    #endregion

    #region 辅助处理方法（核心操作 + 动画）
    /// <summary> 硬币转换（原版逻辑）</summary>
    private static void ApplyCoinTransform(WorldItem item)
    {
        int equiv = item.inner.GetShimmerEquivalentType();
        switch (equiv)
        {
            case ItemID.SilverCoin: item.stack *= 100; break;
            case ItemID.GoldCoin: item.stack *= 10000; break;
            case ItemID.PlatinumCoin:
                if (item.stack > 1) item.stack = 1;
                item.stack *= 1000000;
                break;
        }

        // 利用掉落物反推服务器玩家索引，确保正确添加幸运值
        // 添加判空，防止 playerIndexTheItemIsReservedFor = 255 时崩溃
        int plrIdx = item.playerIndexTheItemIsReservedFor;
        if (plrIdx >= 0 && plrIdx < Main.maxPlayers)
        {
            TSPlayer tsplr = TShock.Players[plrIdx];
            if (tsplr != null && tsplr.Active)
            {
                Player plr = tsplr.TPlayer;
                if (plr != null && plr.active)
                    plr.AddCoinLuck(item.Center, item.stack);
            }
        }

        NetMessage.SendData(MessageID.ShimmerActions, -1, -1, null, 1, item.Center.X, item.Center.Y, item.stack);
        item.type = 0;
        item.stack = 0;
        PostProcess(item);
    }

    /// <summary> 凝胶气球 → 彩虹史莱姆 </summary>
    private static void SpawnRainbowSlime(WorldItem item)
    {
        NPC.unlockedSlimeRainbowSpawn = true;
        NetMessage.SendData(MessageID.WorldData);
        int npcIdx = NPC.NewNPC(item.GetNPCSource_FromThis(), (int)item.Center.X + 4, (int)item.Center.Y, 681);
        if (npcIdx >= 0)
        {
            NPC npc = Main.npc[npcIdx];
            npc.velocity = item.velocity;
            npc.shimmerTransparency = 1f;
            NetMessage.SendData(MessageID.ShimmerActions, -1, -1, null, 2, npcIdx);
        }
        WorldGen.CheckAchievement_RealEstateAndTownSlimes();
        item.stack--;
        if (item.stack <= 0) item.type = 0;
        PostProcess(item);
    }

    /// <summary> 史莱姆王冠 → 史莱姆雨 </summary>
    private static void StartSlimeRain(WorldItem item)
    {
        Main.StartSlimeRain();
        item.stack--;
        if (item.stack <= 0) item.type = 0;
        else item.shimmered = true;
        PostProcess(item);
    }

    /// <summary> 普通物品转换（1:1 替换） </summary>
    private static void ConvertToItem(WorldItem item, int targetType)
    {
        int oldStack = item.stack;
        item.inner.SetDefaults(targetType);
        item.stack = oldStack;
        item.shimmered = true;
        PostProcess(item);
    }

    /// <summary> 物品 → NPC 转换 </summary>
    private static void ConvertToNPC(WorldItem item, int npcType)
    {
        int maxAttempts = 50;
        int availableSlots = NPC.GetAvailableAmountOfNPCsToSpawnUpToSlot(item.stack, Main.maxNPCs);
        while (maxAttempts > 0 && availableSlots > 0 && item.stack > 0)
        {
            maxAttempts--;
            availableSlots--;
            item.stack--;
            int spawnNPC = NPCID.Sets.ShimmerTransformToNPC[npcType] >= 0 ? NPCID.Sets.ShimmerTransformToNPC[npcType] : npcType;
            int npcIdx = NPC.ReleaseNPC((int)item.Center.X, (int)item.Bottom.Y, spawnNPC, 0, Main.myPlayer);
            if (npcIdx >= 0)
            {
                Main.npc[npcIdx].shimmerTransparency = 1f;
                NetMessage.SendData(MessageID.ShimmerActions, -1, -1, null, 2, npcIdx);
            }
        }
        item.shimmered = true;
        if (item.stack <= 0) item.type = 0;
        PostProcess(item);
    }

    /// <summary>
    /// 自定义分解（带飞行动画），支持图格条件检查（服务端）
    /// </summary>
    private static bool TryCustomDecraft(WorldItem item, RecipeInfo rule, Vector2 from, Vector2 to)
    {
        // ---- 1. 检查是否满足图格条件（服务端） ----
        if (rule.requiredTile >= 0)
        {
            int plrIdx = item.playerIndexTheItemIsReservedFor;
            if (plrIdx < 0 || plrIdx >= Main.maxPlayers)
                return false;

            // 使用 TShock 玩家数组获取 Player 对象
            TSPlayer tsplr = TShock.Players[plrIdx];
            if (tsplr == null || !tsplr.Active)
                return false;

            Player plr = tsplr.TPlayer;
            if (plr == null || !plr.active)
                return false;

            // 强制刷新玩家附近的图格状态
            plr.AdjTiles();
            if (!plr.adjTile[rule.requiredTile])
            {
                // 通知玩家
                tsplr.SendInfoMessage($"缺少所需图格 (ID:{rule.requiredTile})，无法分解");
                return false; // 不消耗物品
            }
        }

        // ---- 2. 计算可分解次数 ----
        int stack = item.stack;
        int createStack = rule.createItem.stack;
        int times = stack / createStack;
        if (times == 0) return false; // 不足一次，不分解

        // ---- 3. 原物品立即消失 ----
        item.type = 0;
        item.stack = 0;
        item.TurnToAir();
        item.SyncItem();

        // ---- 4. 播放上升飞行动画 ----
        Animations.PlayFlyAnimation(from, to, rule.createItem.type);

        // ---- 5. 为每种材料创建延迟生成任务 ----
        int delay = 0;
        const int delayStep = 15; // 每材料间隔帧数
        foreach (var kv in rule.requiredItems)  // Key = 物品ID, Value = 单次数量
        {
            int matType = kv.Key;
            int perStack = kv.Value;
            int total = perStack * times;

            Vector2 matPos = to;
            int offPx = 30;
            float offX = Main.rand.Next(-offPx, offPx + 1);
            float offY = Main.rand.Next(-offPx, offPx + 1);
            matPos = to + new Vector2(offX, offY);
            matPos.X = Math.Clamp(matPos.X, 32, (Main.maxTilesX - 1) * 16);
            matPos.Y = Math.Clamp(matPos.Y, 32, (Main.maxTilesY - 1) * 16);

            Animations.AddTask(matType, total, matPos, delay);
            delay += delayStep;
        }

        // ---- 6. 终点特效 ----
        var settings = new ParticleOrchestraSettings
        {
            PositionInWorld = to,
            MovementVector = Vector2.Zero,
            UniqueInfoPiece = 0,
            IndexOfPlayerWhoInvokedThis = 0
        };
        ParticleOrchestrator.BroadcastOrRequestParticleSpawn(ParticleOrchestraType.ShimmerTownNPC, settings);

        return true;
    }

    /// <summary> 原版分解（带飞行动画） </summary>
    private static bool TryOriginalDecraftWithAnim(WorldItem item, int recipeIdx, Vector2 from, Vector2 to)
    {
        Recipe recipe = Main.recipe[recipeIdx];
        int stack = item.stack;
        int createStack = recipe.createItem.stack;
        int times = stack / createStack;
        if (times == 0) return false;

        // 收集材料
        var materials = new List<(int Type, int Stack)>();
        if (recipe.customShimmerResults != null && recipe.customShimmerResults.Count > 0)
        {
            foreach (var matItem in recipe.customShimmerResults)
                materials.Add((matItem.type, matItem.stack));
        }
        else
        {
            foreach (var req in recipe.requiredItemQuickLookup)
            {
                if (req.itemIdOrRecipeGroup == 0) break;
                int matType = req.IsRecipeGroup ? req.RecipeGroup.DecraftItemId : req.itemIdOrRecipeGroup;
                materials.Add((matType, req.stack));
            }
        }
        if (materials.Count == 0) return false;

        // 原物品消失
        item.type = 0;
        item.stack = 0;
        item.TurnToAir();
        item.SyncItem();

        // 飞行动画
        Animations.PlayFlyAnimation(from, to, recipe.createItem.type);

        // 延迟生成材料
        int delay = 0;
        const int delayStep = 15;
        foreach (var (typ, stackPer) in materials)
        {
            int total = stackPer * times;
            Vector2 matPos = to;
            int offPx = 30;
            float offX = Main.rand.Next(-offPx, offPx + 1);
            float offY = Main.rand.Next(-offPx, offPx + 1);
            matPos = to + new Vector2(offX, offY);
            matPos.X = Math.Clamp(matPos.X, 32, (Main.maxTilesX - 1) * 16);
            matPos.Y = Math.Clamp(matPos.Y, 32, (Main.maxTilesY - 1) * 16);

            Animations.AddTask(typ, total, matPos, delay);
            delay += delayStep;
        }

        // 终点特效
        var settings = new ParticleOrchestraSettings
        {
            PositionInWorld = to,
            MovementVector = Vector2.Zero,
            UniqueInfoPiece = 0,
            IndexOfPlayerWhoInvokedThis = 0
        };
        ParticleOrchestrator.BroadcastOrRequestParticleSpawn(ParticleOrchestraType.ShimmerTownNPC, settings);

        return true;
    }

    /// <summary> 通用后处理：特效、同步、成就 </summary>
    private static void PostProcess(WorldItem item)
    {
        if (item.stack > 0 && item.type > 0)
        {
            item.shimmerTime = 1f;
            item.shimmerWet = true;
            item.wet = true;
            item.velocity *= 0.1f;
        }

        if (Main.netMode == 0)
            WorldItem.ShimmerEffect(item.Center);
        else
            NetMessage.SendData(MessageID.ShimmerActions, -1, -1, null, 0, item.Center.X, item.Center.Y);

        item.SyncItem();
        AchievementsHelper.NotifyProgressionEvent(27);

        if (item.stack == 0 || item.type == 0)
            item.TurnToAir();
    }
    #endregion

    #region 辅助工具：进度值翻译与备注生成
    /// <summary> 检查进度条件是否满足 </summary>
    public static bool CheckProgress(byte progress)
    {
        if (progress < 0 || progress >= DownedFuncs.Length) return true;
        return DownedFuncs[progress]();
    }

    /// <summary> 为每个转换规则生成自动备注 </summary>
    public static void ShowMess()
    {
        foreach (var info in config.Replace)
        {
            info.Text = string.Empty;
            string clear = info.clear ? "并阻止转换" : "";
            if (string.IsNullOrEmpty(info.Text))
                info.Text = $"{ProgMess(info.progress)} 将 《{Lang.GetItemNameValue(info.srcType)}》 转换为 《{Lang.GetItemNameValue(info.destType)}》{clear}";
        }
    }

    /// <summary> 进度值 → 中文描述 </summary>
    public static string ProgMess(byte prog)
    {
        return prog switch
        {
            0 => "无条件",
            1 => "击败史王",
            2 => "击败克眼",
            3 => "击败世吞克脑",
            4 => "击败蜂王",
            5 => "击败骷髅王",
            6 => "击败鹿角怪",
            7 => "击败血肉墙",
            8 => "击败史莱姆皇后",
            9 => "击败任意机械BOSS",
            10 => "击败毁灭者",
            11 => "击败双子眼",
            12 => "击败机械骷髅王",
            13 => "击败世花",
            14 => "击败石巨人",
            15 => "击败猪鲨",
            16 => "击败光女",
            17 => "击败教徒",
            18 => "击败日耀柱",
            19 => "击败星云柱",
            20 => "击败星璇柱",
            21 => "击败星尘柱",
            22 => "击败月总",
            23 => "击败哀木",
            24 => "击败南瓜王",
            25 => "击败常绿尖叫怪",
            26 => "击败圣诞坦克",
            27 => "击败冰雪女王",
            28 => "击败四柱",
            29 => "击败血月小丑",
            30 => "击败哥布林入侵",
            31 => "击败海盗入侵",
            32 => "击败火星暴乱",
            _ => string.Empty
        };
    }
    #endregion
}