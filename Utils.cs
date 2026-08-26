using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using static ShimmerItemReplace.ShimmerItemReplace;

namespace ShimmerItemReplace;

internal class Utils
{
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

    #region 获取图格的中文名称
    /// <summary>
    /// 通过图格ID反向查找对应的物品名称（因为图格本身没有中文名，利用 createTile 映射物品）。
    /// </summary>
    public static string GetTileName(int tileID)
    {
        var name = string.Empty;
        foreach (var kv in ContentSamples.ItemsByType)
        {
            Item item = kv.Value;
            if (item != null && (item.createTile == tileID || item.createWall == tileID))
                name = Lang.GetItemNameValue(item.type);
        }

        // 如果物品模板没有这个图格名称
        if (string.IsNullOrEmpty(name))
        {
            // 从配方类里获取图格名 再拿不到 就返回图格英文名
            name = !string.IsNullOrEmpty(Recipe.GetRequiredTileName(tileID)) ?
                   $"{Recipe.GetRequiredTileName(tileID)}[{tileID}]" :
                   $"{TileID.Search.GetName(tileID)}({tileID})";
        }

        return name;
    }
    #endregion

    /// <summary>
    /// 获取距离物品最近的玩家
    /// </summary>
    public static Player? GetPlayer(Entity entity, float maxDistance = 200f)
    {
        Player? nearest = null;
        float nearestDist = float.MaxValue;

        for (int i = 0; i < Main.maxPlayers; i++)
        {
            Player p = Main.player[i];
            if (p?.active != true) continue;

            float dist = entity.Distance(p.Center);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = p;
            }
        }

        return (nearest != null && nearestDist <= maxDistance) ? nearest : null;
    }
}
