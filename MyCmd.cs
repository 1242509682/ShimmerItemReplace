using Terraria;
using Terraria.ID;
using TShockAPI;
using static ShimmerItemReplace.ShimmerItemReplace;

namespace ShimmerItemReplace;

internal class MyCmd
{
    #region 命令与配置加载/重置
    /// <summary> 加载配置（命令 /sirc load）</summary>
    private static void Load(TSPlayer ply, bool detailed = false)
    {
        try
        {
            config = Config.Read();
            ApplyConfig();

            if (detailed)
            {
                foreach (var info in config.Replace)
                    ply.SendInfoMessage($"转换: {Lang.GetItemNameValue(info.srcType)} => {Lang.GetItemNameValue(info.destType)} (进度:{info.progress})");
                foreach (var recipe in config.Recipe)
                {
                    string mats = string.Join(", ", recipe.requiredItems.Select(m => $"{Lang.GetItemNameValue(m.Key)}x{m.Value}"));
                    ply.SendInfoMessage($"分解: {Lang.GetItemNameValue(recipe.createItem.type)} => {mats} (进度:{recipe.progress})");
                }
            }
            ply.SendInfoMessage("加载完成");
            ply.SendInfoMessage($"转换规则:{config.Replace.Length} 条, 分解规则:{config.Recipe.Length} 条");
        }
        catch (Exception ex)
        {
            ply.SendErrorMessage(ex.ToString());
            TShock.Log.Error(ex.ToString());
        }
    }

    /// <summary> 重置为原版转换（命令 /sirc reset）</summary>
    private static void Reset(TSPlayer? ply)
    {
        ItemID.Sets.ShimmerTransformToItem = (int[])DefaultShimmerTransformToItem.Clone();
        Array.Fill(CanShimmerFuncs, null);
        Animations.Clear();
        ply?.SendInfoMessage("重置完成（仅重置原版转换表）");
    }

    /// <summary> 重置并重新加载配置（命令 /sirc reload）</summary>
    public static void Reload(TSPlayer? ply, bool detailed = false)
    {
        Reset(null);
        Load(ply, detailed);
    }

    /// <summary> 命令处理入口 </summary>
    public static void SircCmd(CommandArgs args)
    {
        if (args.Parameters.Count == 0)
        {
            args.Player.SendInfoMessage(
                "/{0} reset 重置微光变换为默认值\n" +
                "/{0} load 加载配置\n" +
                "/{0} reload 重置并加载", config.CommandNames[0]);
            return;
        }
        switch (args.Parameters[0])
        {
            case "reset": Reset(args.Player); break;
            case "load": Load(args.Player, args.Parameters.Count > 1 && args.Parameters[1] == "-d"); break;
            case "reload": Reload(args.Player, args.Parameters.Count > 1 && args.Parameters[1] == "-d"); break;
            default: args.Player.SendErrorMessage("未知参数:{0}", args.Parameters[0]); break;
        }
    }

    /// <summary> 将配置应用到运行时数组 </summary>
    public static void ApplyConfig()
    {
        // 过滤无效转换规则
        config.Replace = config.Replace
            .Where(info => info.srcType > 0 && info.destType > 0)
            .ToArray();

        // 过滤无效分解规则（适配字典）
        var valid = new List<RecipeInfo>();
        foreach (var dec in config.Recipe)
        {
            // 检查合成物品
            if (dec.createItem == null || dec.createItem.type <= 0 || dec.createItem.stack <= 0)
            {
                TShock.Log.ConsoleError($"[ShimmerItemReplace] 分解规则无效：合成物品为空或ID为0，已忽略");
                continue;
            }

            // 检查材料字典
            if (dec.requiredItems == null || dec.requiredItems.Count == 0)
            {
                TShock.Log.ConsoleError($"[ShimmerItemReplace] 分解规则无效：合成物品 {dec.createItem.type} 的材料为空，已忽略");
                continue;
            }

            bool has = false;
            foreach (var kv in dec.requiredItems)
            {
                if (kv.Key <= 0 || kv.Value <= 0)
                {
                    TShock.Log.ConsoleError($"[ShimmerItemReplace] 分解规则无效：合成物品 {dec.createItem.type} 的材料包含无效项 (ID:{kv.Key}, Stack:{kv.Value})，已忽略");
                    has = true;
                    break;
                }
            }
            if (has) continue;

            valid.Add(dec);
        }
        config.Recipe = valid.ToArray();

        // 应用转换规则
        foreach (var info in config.Replace)
        {
            if (info.clear)
                ItemID.Sets.ShimmerTransformToItem[info.srcType] = -1;
            else if (info.destType != -1)
                ItemID.Sets.ShimmerTransformToItem[info.srcType] = info.destType;

            if (info.progress >= 0 && info.progress < DownedFuncs.Length)
                CanShimmerFuncs[info.srcType] = DownedFuncs[info.progress];
        }

        // 应用分解规则（仅设置进度锁）
        foreach (var recipe in config.Recipe)
        {
            if (recipe.progress >= 0 && recipe.progress < DownedFuncs.Length)
                CanShimmerFuncs[recipe.createItem.type] = DownedFuncs[recipe.progress];
        }
    }
    #endregion
}
