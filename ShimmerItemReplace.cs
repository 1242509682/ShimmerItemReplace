using System.Reflection;
using MonoMod.RuntimeDetour;
using Newtonsoft.Json;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.Achievements;
using Terraria.ID;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.Hooks;
using static ShimmerItemReplace.Config;

namespace ShimmerItemReplace;

[ApiVersion(2, 1)]
public class ShimmerItemReplace : TerrariaPlugin
{
    public override string Name => "ShimmerItemReplace";
    public override string Author => "yu、羽学";
    public override Version Version => new(1, 2, 2);
    public override string Description => "修改微光变换的物品";

    #region 配置重载读取与写入方法
    internal static Config config = new();
    private static void ReloadConfig(ReloadEventArgs args = null!)
    {
        LoadConfig();
        args.Player.SendInfoMessage("[ShimmerItemReplace] 重新加载配置完毕。");
    }
    private static void LoadConfig()
    {
        config = Config.Read();
        ShowMess();
        config.Write();
    }
    #endregion

    private Hook? canShimmerHook;
    private Hook? getShimmeredHook;
    public static readonly int[] DefaultShimmerTransformToItem = (int[])ItemID.Sets.ShimmerTransformToItem.Clone();
    internal static Func<bool>?[] CanShimmerFuncs = new Func<bool>[ItemID.Count];
    internal static Func<bool>[] DownedFuncs =
    [
        () => true, //0 无
        () => NPC.downedSlimeKing, // 1 史莱姆王
        () => NPC.downedBoss1, // 2 克眼
        () => NPC.downedBoss2, // 3 世吞 或 克脑
        () => NPC.downedQueenBee, // 4 蜂王
        () => NPC.downedBoss3, // 5 骷髅王
        () => NPC.downedDeerclops, // 6 鹿角怪
        () => Main.hardMode, // 7 困难模式(肉山)
        () => NPC.downedQueenSlime, // 8 史莱姆皇后
        () => NPC.downedMechBossAny, // 9 任意机械Boss
        () => NPC.downedMechBoss1, // 10 毁灭者
        () => NPC.downedMechBoss2, // 11 双子魔眼
        () => NPC.downedMechBoss3, // 12 机械骷髅王
        () => NPC.downedPlantBoss, // 13 世纪之花
        () => NPC.downedGolemBoss, // 14 石巨人
        () => NPC.downedFishron, // 15 猪鲨
        () => NPC.downedEmpressOfLight, // 16 光女
        () => NPC.downedAncientCultist, // 17 教徒
        () => NPC.downedTowerSolar, // 18 日耀柱
        () => NPC.downedTowerNebula, // 19 星云柱
        () => NPC.downedTowerVortex, // 20 星旋柱
        () => NPC.downedTowerStardust, // 21 星尘柱
        () => NPC.downedMoonlord, // 22 月亮领主
        () => NPC.downedHalloweenTree, // 23 哀木
        () => NPC.downedHalloweenKing, // 24 南瓜王
        () => NPC.downedChristmasTree, // 25 常绿尖叫怪
        () => NPC.downedChristmasSantank, // 26 圣诞坦克
        () => NPC.downedChristmasIceQueen, // 27 冰雪女王
        () => NPC.downedTowers, // 28 四柱
        () => NPC.downedClown, // 29 小丑
        () => NPC.downedGoblins, // 30 哥布林入侵
        () => NPC.downedPirates, // 31 海盗入侵
        () => NPC.downedMartians // 32 火星暴乱
    ];
    private bool AddToReload = false;
    private Command Command;
    public ShimmerItemReplace(Main game) : base(game)
    {
        Command = new Command("sirc", Cmd, "sirc");

        try
        {
            if (!Directory.Exists(TShock.SavePath))
            {
                Directory.CreateDirectory(TShock.SavePath);
            }

            if (!File.Exists(path))
            {
                File.WriteAllText(path, JsonConvert.SerializeObject(new Config(), Formatting.Indented));
            }

            if (config is null)
            {
                TSPlayer.Server.SendErrorMessage("配置文件转换失败");
            }
            else
            {
                if (config.AddToReload)
                {
                    AddToReload = true;
                    TShockAPI.Hooks.GeneralHooks.ReloadEvent += ReloadConfig;
                }

                foreach (var info in config.Replace)
                {
                    if (info.clear)
                    {
                        ItemID.Sets.ShimmerTransformToItem[info.srcType] = -1;
                    }
                    else
                    {
                        if (info.destType != -1)
                        {
                            ItemID.Sets.ShimmerTransformToItem[info.srcType] = info.destType;
                        }
                        if (info.progress >= 0 && info.progress < DownedFuncs.Length)
                        {
                            CanShimmerFuncs[info.srcType] = DownedFuncs[info.progress];
                        }
                    }
                }

                for (int i = 0; i < Math.Min(config.Recipe.Length, Recipe.maxRequirements); i++)
                {
                    var recipeInfo = config.Recipe[i];
                    recipeInfo.UpdateRecipe();
                    if (recipeInfo.progress >= 0 && recipeInfo.progress < DownedFuncs.Length)
                    {
                        CanShimmerFuncs[recipeInfo.createItem.type] = DownedFuncs[recipeInfo.progress];
                    }
                }
                Recipe.UpdateWhichItemsAreCrafted();
                Console.WriteLine("当前配方数:{0}", Recipe.numRecipes - 1);
                Command = new Command(config.CommandPermission, Cmd, config.CommandNames);
            }
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
        Commands.ChatCommands.Add(Command);

        var canShimmerMethod = typeof(Item).GetMethod("CanShimmer", Type.EmptyTypes);
        if (canShimmerMethod == null)
            throw new InvalidOperationException("Cannot find Item.CanShimmer method");
        var canShimmerReplacement = typeof(ShimmerItemReplace).GetMethod("CanShimmer", BindingFlags.Public | BindingFlags.Static);
        canShimmerHook = new Hook(canShimmerMethod, canShimmerReplacement!);

        var getShimmeredMethod = typeof(WorldItem).GetMethod("GetShimmered", Type.EmptyTypes);
        if (getShimmeredMethod == null)
            throw new InvalidOperationException("Cannot find WorldItem.GetShimmered method");
        var getShimmeredReplacement = typeof(ShimmerItemReplace).GetMethod("GetShimmered", BindingFlags.Public | BindingFlags.Static);
        getShimmeredHook = new Hook(getShimmeredMethod, getShimmeredReplacement!);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Commands.ChatCommands.Remove(Command);
            canShimmerHook?.Dispose();
            getShimmeredHook?.Dispose();

            if (AddToReload)
            {
                GeneralHooks.ReloadEvent -= OnReload;
            }
        }
        base.Dispose(disposing);
    }

    private void OnReload(ReloadEventArgs e)
    {
        Load(e.Player);
    }

    private static void Load(TSPlayer ply, bool detailed = false)
    {
        try
        {
            if (config is null)
            {
                ply.SendErrorMessage("配置文件转换失败");
                return;
            }

            foreach (var info in config.Replace)
            {
                if (info.clear)
                {
                    ItemID.Sets.ShimmerTransformToItem[info.srcType] = -1;
                }
                else
                {
                    if (info.destType != -1)
                    {
                        ItemID.Sets.ShimmerTransformToItem[info.srcType] = info.destType;
                        if (detailed)
                        {
                            ply.SendInfoMessage($"Shimmer:{Lang.GetItemNameValue(info.srcType)} => {Lang.GetItemNameValue(info.destType)}");
                        }
                    }
                    if (info.progress >= 0 && info.progress < DownedFuncs.Length)
                    {
                        CanShimmerFuncs[info.srcType] = DownedFuncs[info.progress];
                    }
                    else if (detailed)
                    {
                        ply.SendInfoMessage("无效进度值:{0}", info.progress);
                    }
                }
            }

            for (int i = 0; i < Math.Min(config.Recipe.Length, Recipe.maxRequirements); i++)
            {
                var recipeInfo = config.Recipe[i];
                recipeInfo.UpdateRecipe();
                if (recipeInfo.progress >= 0 && recipeInfo.progress < DownedFuncs.Length)
                {
                    CanShimmerFuncs[recipeInfo.createItem.type] = DownedFuncs[recipeInfo.progress];
                }
            }
            Recipe.UpdateWhichItemsAreCrafted();
            Recipe.UpdateMaterialFieldForAllRecipes();

            ply.SendInfoMessage("加载完成");
            ply.SendInfoMessage("当前配方数:{0}", Recipe.numRecipes - 1);
        }
        catch (Exception ex)
        {
            ply.SendErrorMessage(ex.ToString());
            TShock.Log.Error(ex.ToString());
            return;
        }
    }
    private static void Reset(TSPlayer? ply)
    {
        ItemID.Sets.ShimmerTransformToItem = (int[])DefaultShimmerTransformToItem.Clone();
        Array.Fill(CanShimmerFuncs, null);

        Array.Fill(ItemID.Sets.IsCrafted, -1);
        Array.Fill(ItemID.Sets.IsCraftedCrimson, -1);
        Array.Fill(ItemID.Sets.IsCraftedCorruption, -1);
        RecipeGroup.recipeGroups.Clear();
        Recipe.numRecipes = 0;
        Recipe.SetupRecipes();

        ply?.SendInfoMessage("重置完成");
    }
    private static void Reload(TSPlayer ply, bool detailed = false)
    {
        Reset(null);
        Load(ply, detailed);
    }
    private void Cmd(CommandArgs args)
    {
        if (args.Parameters.Count == 0)
        {
            args.Player.SendInfoMessage(
                "/{0} reset 重置微光变换为默认值\n" +
                "/{0} load 加载配置\n" +
                "/{0} reload 重置并加载", Command.Name);
            return;
        }
        switch (args.Parameters[0])
        {
            case "reset":
                Reset(args.Player);
                break;
            case "load":
                Load(args.Player, args.Parameters.Count > 1 && args.Parameters[1] == "-d");
                break;
            case "reload":
                Reload(args.Player, args.Parameters.Count > 1 && args.Parameters[1] == "-d");
                break;
            default:
                args.Player.SendErrorMessage("未知参数:{0}", args.Parameters[0]);
                break;
        }
    }

    public static bool CanShimmer(Item item)
    {
        var func = CanShimmerFuncs[item.type];
        if (func is not null)
        {
            return func();
        }

        int shimmerEquivalentType = item.GetShimmerEquivalentType();
        if (ShimmerTransforms.IsItemTransformLocked(shimmerEquivalentType))
        {
            return false;
        }

        if (shimmerEquivalentType == ItemID.SlimeCrown && Main.slimeRain)
        {
            return false;
        }

        if ((shimmerEquivalentType != ItemID.GelBalloon || NPC.unlockedSlimeRainbowSpawn) &&
            ShimmerTransforms.GetTransformToItem(shimmerEquivalentType) <= 0 &&
            !ShimmerTransforms.IsItemDecraftableAndIsDecraftUnlocked(item) &&
            !ItemID.Sets.CommonCoin[shimmerEquivalentType])
        {
            return item.makeNPC > 0;
        }

        return true;
    }

    public static void GetShimmered(WorldItem item)
    {
        int shimmerEquivalentType = item.inner.GetShimmerEquivalentType();
        int decraftingRecipeIndex = ShimmerTransforms.GetDecraftingRecipeIndex(shimmerEquivalentType);
        //int transformToItem = ShimmerTransforms.GetTransformToItem(shimmerEquivalentType);
        int transformToItem = ItemID.Sets.ShimmerTransformToItem[shimmerEquivalentType];
        if (ItemID.Sets.CommonCoin[shimmerEquivalentType])
        {
            switch (shimmerEquivalentType)
            {
                case ItemID.SilverCoin:
                    item.stack *= 100;
                    break;
                case ItemID.GoldCoin:
                    item.stack *= 10000;
                    break;
                case ItemID.PlatinumCoin:
                    if (item.stack > 1)
                    {
                        item.stack = 1;
                    }
                    item.stack *= 1000000;
                    break;
            }
            Main.player[Main.myPlayer].AddCoinLuck(item.Center, item.stack);
            NetMessage.SendData(MessageID.ShimmerActions, -1, -1, null, 1, (int)item.Center.X, (int)item.Center.Y, item.stack);
            item.type = 0;
            item.stack = 0;
        }
        else if (transformToItem > 0)
        {
            int num = item.stack;
            item.SetDefaults(transformToItem);
            item.stack = num;
            item.shimmered = true;
        }
        else if (item.type == ItemID.GelBalloon)
        {
            if (NPC.unlockedSlimeRainbowSpawn) return;

            NPC.unlockedSlimeRainbowSpawn = true;
            NetMessage.SendData(MessageID.WorldData);
            int npcIndex = NPC.NewNPC(item.GetNPCSource_FromThis(), (int)item.Center.X + 4, (int)item.Center.Y, 681);
            if (npcIndex >= 0)
            {
                NPC npc = Main.npc[npcIndex];
                npc.velocity = item.velocity;
                npc.netUpdate = true;
                npc.shimmerTransparency = 1f;
                NetMessage.SendData(MessageID.ShimmerActions, -1, -1, null, 2, npcIndex);
            }

            WorldGen.CheckAchievement_RealEstateAndTownSlimes();
            item.stack--;
            if (item.stack <= 0)
                item.type = 0;
        }
        else if (item.type == ItemID.SlimeCrown)
        {
            if (Main.slimeRain) return;

            Main.StartSlimeRain();
            item.stack--;
            if (item.stack <= 0)
                item.type = 0;
            else
                item.shimmered = true;

        }
        else if (item.makeNPC > 0)
        {
            int num3 = 50;
            int maxNPCs = Main.maxNPCs;
            int num4 = NPC.GetAvailableAmountOfNPCsToSpawnUpToSlot(item.stack, maxNPCs);
            while (num3 > 0 && num4 > 0 && item.stack > 0)
            {
                num3--;
                num4--;
                item.stack--;

                int npcIndex = -1;
                npcIndex = (NPCID.Sets.ShimmerTransformToNPC[item.makeNPC] < 0) ? NPC.ReleaseNPC((int)item.Center.X, (int)item.Bottom.Y, item.makeNPC, item.placeStyle, Main.myPlayer) : NPC.ReleaseNPC((int)item.Center.X, (int)item.Bottom.Y, NPCID.Sets.ShimmerTransformToNPC[item.makeNPC], 0, Main.myPlayer);

                if (npcIndex >= 0)
                {
                    Main.npc[npcIndex].shimmerTransparency = 1f;
                    NetMessage.SendData(MessageID.ShimmerActions, -1, -1, null, 2, npcIndex);
                }
            }

            item.shimmered = true;
            if (item.stack <= 0)
                item.type = 0;
        }
        else if (decraftingRecipeIndex >= 0)
        {
            int decraftAmount = item.inner.FindDecraftAmount();
            Recipe recipe = Main.recipe[decraftingRecipeIndex];
            bool flag = recipe.requiredItem[1].stack > 0;
            IEnumerable<Recipe.RequiredItemEntry> enumerable = recipe.requiredItemQuickLookup;
            if (recipe.customShimmerResults != null)
            {
                enumerable = recipe.customShimmerResults.Select(delegate (Item item)
                {
                    Recipe.RequiredItemEntry result = default(Recipe.RequiredItemEntry);
                    result.itemIdOrRecipeGroup = item.type;
                    result.stack = item.stack;
                    return result;
                });
            }

            int num7 = 0;
            foreach (Recipe.RequiredItemEntry requiredItem in enumerable)
            {
                if (requiredItem.itemIdOrRecipeGroup <= 0)
                {
                    break;
                }

                num7++;
                int needSpawnStack = decraftAmount * requiredItem.stack;
                int type = requiredItem.IsRecipeGroup ? requiredItem.RecipeGroup.DecraftItemId : requiredItem.itemIdOrRecipeGroup;
                if (recipe.alchemy)
                {
                    for (int num17 = needSpawnStack; num17 > 0; num17--)
                    {
                        if (Main.rand.Next(3) == 0)
                        {
                            needSpawnStack--;
                        }
                    }
                }

                while (needSpawnStack > 0)
                {
                    int stack = needSpawnStack;
                    if (stack > 9999) stack = 9999;

                    needSpawnStack -= stack;
                    int newItemIndex = Item.NewItem(item.GetItemSource_Misc(ItemSourceID.Shimmer), (int)item.position.X, (int)item.position.Y, item.width, item.height, type);

                    var worldItem = Main.item[newItemIndex];
                    worldItem.stack = stack;
                    worldItem.shimmerTime = 1f;
                    worldItem.shimmered = true;
                    worldItem.shimmerWet = true;
                    worldItem.wet = true;
                    worldItem.velocity *= 0.1f;
                    worldItem.playerIndexTheItemIsReservedFor = Main.myPlayer;
                    if (flag)
                    {
                        worldItem.velocity.X = 1f * num7;
                        worldItem.velocity.X *= 1f + num7 * 0.05f;
                        if (num7 % 2 == 0)
                        {
                            worldItem.velocity.X *= -1f;
                        }
                    }

                    NetMessage.SendData(MessageID.SyncItemsWithShimmer, -1, -1, null, newItemIndex, 1f);
                }
            }

            item.stack -= decraftAmount * recipe.createItem.stack;
            if (item.stack <= 0)
            {
                item.stack = 0;
                item.type = 0;
            }
        }
        item.shimmerTime = item.stack > 0 ? 1f : 0f;
        item.shimmerWet = true;
        item.wet = true;
        item.velocity *= 0.1f;

        if (Main.netMode == 0)
        {
            WorldItem.ShimmerEffect(item.Center);
        }
        else
        {
            NetMessage.SendData(MessageID.ShimmerActions, -1, -1, null, 0, (int)item.Center.X, (int)item.Center.Y);
            NetMessage.SendData(MessageID.SyncItemsWithShimmer, -1, -1, null, item.whoAmI, 1f);
        }

        AchievementsHelper.NotifyProgressionEvent(27);
        if (item.stack == 0)
        {
            item.makeNPC = -1;
            item.TurnToAir();
        }
    }

    #region 进度值翻译
    private static void ShowMess()
    {
        foreach (var info in config.Replace)
        {
            info.Text = string.Empty;
            string clear = info.clear ? "并阻止转换" : "";

            if (string.IsNullOrEmpty(info.Text))
                info.Text = $"{ProgMess(info.progress)} 将 《{Lang.GetItemNameValue(info.srcType)}》 转换为 《{Lang.GetItemNameValue(info.destType)}》{clear}";
        }
    }

    public static string ProgMess(byte prog)
    {
        switch (prog)
        {
            case 0: return "无条件";
            case 1: return "击败史王";
            case 2: return "击败克眼";
            case 3: return "击败世吞克脑";
            case 4: return "击败蜂王";
            case 5: return "击败骷髅王";
            case 6: return "击败鹿角怪";
            case 7: return "击败血肉墙";
            case 8: return "击败史莱姆皇后";
            case 9: return "击败任意机械BOSS";
            case 10: return "击败毁灭者";
            case 11: return "击败双子眼";
            case 12: return "击败机械骷髅王";
            case 13: return "击败世花";
            case 14: return "击败石巨人";
            case 15: return "击败猪鲨";
            case 16: return "击败光女";
            case 17: return "击败教徒";
            case 18: return "击败日耀柱";
            case 19: return "击败星云柱";
            case 20: return "击败星璇柱";
            case 21: return "击败星尘柱";
            case 22: return "击败月总";
            case 23: return "击败衰木";
            case 24: return "击败南瓜王";
            case 25: return "击败常绿尖叫怪";
            case 26: return "击败圣诞坦克";
            case 27: return "击败冰雪女王";
            case 28: return "击败四柱";
            case 29: return "击败血月小丑";
            case 30: return "击败哥布林入侵";
            case 31: return "击败海盗入侵";
            case 32: return "击败火星暴乱";
            default: return string.Empty;
        }
    }
    #endregion
}