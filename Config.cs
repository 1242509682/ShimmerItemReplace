using Newtonsoft.Json;
using Terraria;
using Terraria.ID;
using TShockAPI;

namespace ShimmerItemReplace;

internal class Config
{
    [JsonProperty("进度名称")]
    public string[] progressName =
    [
        "无 0 | 史莱姆王 1 | 克眼 2 | 世吞克脑 3 | 蜂王 4 | 骷髅王 5 | 鹿角怪 6 | 困难模式(肉山) 7 | 史莱姆皇后 8 |",
        "任意机械BOSS 9 | 毁灭者 10 | 双子魔眼 11 | 机械骷髅王 12 | 世纪之花 13 | 石巨人 14 | 猪鲨 15 | 光女 16 |",
        "教徒 17 | 日耀柱 18 | 星云柱 19 | 星璇柱 20 | 星尘柱 21 | 月总 22 | 衰木 23 | 南瓜王 24 | 常绿尖叫怪 25 |",
        "圣诞坦克 26 | 冰雪女王 27 | 四柱 28 | 血月小丑 29 | 哥布林入侵 30 | 海盗入侵 31 | 火星暴乱 32"
    ];
    [JsonProperty("指令名称")]
    public string[] CommandNames = ["sirc"];
    [JsonProperty("指令权限")]
    public string CommandPermission = "sirc";
    [JsonProperty("微光物品转换表")]
    public TransformInfo[] Replace =
    [
        // 这里保持原样，与之前用户提供的完全一致
        new(ItemID.RodofDiscord,    ItemID.RodOfHarmony, 22),
        new(ItemID.Clentaminator,   ItemID.Clentaminator2, 22),
        new(ItemID.BottomlessBucket,ItemID.BottomlessShimmerBucket,22),
        new(ItemID.BottomlessShimmerBucket,ItemID.BottomlessBucket,22),
        new(ItemID.JungleKey,       ItemID.PiranhaGun,13),
        new(ItemID.CorruptionKey,   ItemID.ScourgeoftheCorruptor,13),
        new(ItemID.CrimsonKey,      ItemID.VampireKnives,13),
        new(ItemID.HallowedKey,     ItemID.RainbowGun,13),
        new(ItemID.FrozenKey,       ItemID.StaffoftheFrostHydra,13),
        new(ItemID.DungeonDesertKey,ItemID.StormTigerStaff,13),
        new(364,174,7),
        new(999,182),
        new(100,57,3),
        new(101,57,3),
        new(102,57,3),
        new(56,880,0),
        new(880,56,0),
        new(393,18,0),
        new(18,393,0),
        new(3118,3084),
        new(3084,3095, 5),
        new(3095,3118),
        new(3119,3102),
        new(3102,3099),
        new(3099,3119),
        new(4263,4819),
        new(4819,4263),
        new(899,900,14),
        new(900,899,14),
        new(5043,5326),
        new(5326,5043),
        new(4442,2292),
        new(2292,4325),
        new(4325,4442),
        new(5132,4361),
        new(4361,5132),
        new(3052,3053,7),
        new(3053,3054,7),
        new(3054,3052,7),
        new(554,535,7),
        new(535,554,7),
        new(517,3209,7),
        new(3209,517,7),
        new(682,1265,7),
        new(1265,682,7),
        new(3029,3030,7),
        new(3030,3029,7),
        new(3835,3854,7),
        new(3854,3836,7),
        new(3836,3823,7),
        new(3823,3835,7),
        new(527,528,7),
        new(528,527,7),
        new(3783,2161,7),
        new(2161,3783,7),
        new(1332,522,7),
        new(522,1332,7),
        new(1330,68),
        new(68,1330),
        new(2887,60),
        new(60,2887),
        new(53,3201),
        new(3201,53),
        new(857,987),
        new(987,857),
        new(950,3017),
        new(3017,950),
        new(319,320),
        new(320,319),
        new(69,209),
        new(209,69),
        new(208,223),
        new(223,208),
        new(86,1329,3),
        new(1329,86,3),
        new(662,4392,13),
        new(4392,662,13),
        new(1513,938,13),
        new(362,5070),
        new(5070,362),
        new(59,2171),
        new(2171,59),
        new(54,3200),
        new(3200,54),
        new(977,963,13),
        new(963,977,13),
        new(953,975),
        new(975,953),
        new(3814,3813,3),
        new(3813,3814,3),
        new(4827,4423),
        new(4423,4827),
        new(1141,2767,9),
        new(2214,2215),
        new(2215,2216),
        new(2216,2217),
        new(2217,2214),
    ];
    [JsonProperty("分解配方表")]
    public RecipeInfo[] Recipe = Array.Empty<RecipeInfo>();

    public void SetDefault()
    {
        Recipe = 
        [
            new RecipeInfo
            {
                progress = 0,
                requiredTile = -1,
                createItem = new ItemInfo { type = ItemID.KingSlimeBossBag, stack = 1 },
                requiredItems = new()
                {
                    { ItemID.Lens, 3 },
                    { ItemID.GoldBar, 1 }
                }
            }
        ];
    }

    public static readonly string path = Path.Combine(TShock.SavePath, "ShimmerItemReplace.json");
    public void Write() => File.WriteAllText(path, JsonConvert.SerializeObject(this, Formatting.Indented));
    public static Config Read()
    {
        if (!File.Exists(path))
        {
            var newCfg = new Config();
            newCfg.SetDefault();
            newCfg.Write();
            return newCfg;
        }
        var cfg = JsonConvert.DeserializeObject<Config>(File.ReadAllText(path))!;

        // 如果已有配置文件缺少分解表，则补充默认值
        if (cfg.Recipe == null || cfg.Recipe.Length == 0)
        {
            cfg.SetDefault(); // 仅填充分解配方
        }
        return cfg;
    }
}

public class TransformInfo
{
    [JsonProperty("来源ID")]
    public short srcType = -1;
    [JsonProperty("目标ID")]
    public short destType = -1;
    [JsonProperty("进度值")]
    public byte progress = 0;
    [JsonProperty("阻止转换")]
    public bool clear = false;
    [JsonProperty("自动备注")]
    public string Text = string.Empty;
    public TransformInfo() { }
    public TransformInfo(short stype, short dtype, byte prog = 0, bool c = false)
    {
        this.srcType = stype;
        this.destType = dtype;
        this.progress = prog;
        this.clear = c;
    }
}

public class RecipeInfo
{
    [JsonProperty("进度值")]
    public byte progress = 0;
    [JsonProperty("所需图格ID")]
    public int requiredTile = -1;  // -1 表示不需要任何图格
    [JsonProperty("合成物品")]
    public ItemInfo createItem = new();
    [JsonProperty("合成材料")]
    public Dictionary<int, int> requiredItems = new(); // 物品ID → 数量
}

public class ItemInfo
{
    [JsonProperty("物品ID")]
    public int type;
    [JsonProperty("物品数量")]
    public int stack = 1;
}