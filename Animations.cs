using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.Drawing;
using Terraria.ID;
using static ShimmerItemReplace.ShimmerItemReplace;

namespace ShimmerItemReplace;

/// <summary>
/// 微光转换动画系统（物品上升、粒子特效、延迟生成）
/// </summary>
public static class Animations
{
    private class AnimTask
    {
        public int NewType;
        public int Stack;
        public Vector2 To;
        public long Start;
    }

    private static readonly List<AnimTask> anims = new();

    /// <summary>每帧更新，由主类调用</summary>
    public static void Update()
    {
        if (anims.Count == 0)
        {
            frame = 0; 
            return;
        }

        for (int i = anims.Count - 1; i >= 0; i--)
        {
            var task = anims[i];
            if (frame >= task.Start)
            {
                // 粒子特效
                var settings = new ParticleOrchestraSettings
                {
                    PositionInWorld = task.To,
                    MovementVector = Vector2.Zero,
                    UniqueInfoPiece = task.NewType,
                    IndexOfPlayerWhoInvokedThis = 0
                };
                ParticleOrchestrator.BroadcastOrRequestParticleSpawn(ParticleOrchestraType.DeadCellsMushroomBoiTargetFound, settings);
                ParticleOrchestrator.BroadcastOrRequestParticleSpawn(ParticleOrchestraType.HeroicisSetSpawnSound, settings);

                // 生成物品
                SpawnItem(task.NewType, task.Stack, task.To);
                anims.RemoveAt(i);
            }
        }
    }

    /// <summary>添加一个动画任务（延迟生成物品）</summary>
    public static void AddTask(int type, int stack, Vector2 to, int delayFrames)
    {
        anims.Add(new AnimTask
        {
            NewType = type,
            Stack = stack,
            To = to,
            Start = frame + delayFrames
        });
    }

    /// <summary>清空所有待处理动画</summary>
    public static void Clear()
    {
        anims.Clear();
        frame = 0;
    }

    /// <summary>生成物品（支持堆叠拆分）</summary>
    private static void SpawnItem(int type, int total, Vector2 pos)
    {
        int remain = total;
        while (remain > 0)
        {
            int maxStack = ContentSamples.ItemsByType[type]?.maxStack ?? 9999;
            int stack = Math.Min(remain, maxStack);
            int idx = Item.NewItem(null, pos, type, stack);
            NetMessage.SendData((int)PacketTypes.UpdateItemDrop, -1, -1, null, idx);
            remain -= stack;
        }
    }

    /// <summary>播放上升飞行动画（从起点到终点）</summary>
    public static void PlayFlyAnimation(Vector2 from, Vector2 to, int itemType)
    {
        // 起点特效
        var settings = new ParticleOrchestraSettings
        {
            PositionInWorld = from,
            MovementVector = Vector2.Zero,
            UniqueInfoPiece = 0,
            IndexOfPlayerWhoInvokedThis = 0
        };
        ParticleOrchestrator.BroadcastOrRequestParticleSpawn(ParticleOrchestraType.ShimmerTownNPCSend, settings);

        // 物品上升粒子路径
        var settings2 = new ParticleOrchestraSettings
        {
            PositionInWorld = from,
            MovementVector = to - from,
            UniqueInfoPiece = itemType,
            IndexOfPlayerWhoInvokedThis = 0
        };
        ParticleOrchestrator.BroadcastOrRequestParticleSpawn(ParticleOrchestraType.ItemTransfer, settings2);
    }
}