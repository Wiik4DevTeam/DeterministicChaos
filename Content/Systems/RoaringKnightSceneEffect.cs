using Terraria;
using Terraria.ModLoader;
using DeterministicChaos.Content.NPCs.Bosses;

namespace DeterministicChaos.Content.Systems
{
    public class RoaringKnightSceneEffect : ModSceneEffect
    {
        public override SceneEffectPriority Priority => SceneEffectPriority.BossHigh;

        public override float GetWeight(Player player)
        {
            if (TryGetActiveKnight(out RoaringKnight knight) && knight.IsInFinalStand)
                return 10000f;

            return 1f;
        }

        public override int Music
        {
            get
            {
                int normal = MusicLoader.GetMusicSlot(Mod, "Assets/Music/KnightMusic");
                int finalStand = MusicLoader.GetMusicSlot(Mod, "Assets/Music/KnightFinalStand");

                if (TryGetActiveKnight(out RoaringKnight knight) && knight.IsInFinalStand)
                    return finalStand;

                return normal;
            }
        }

        public override bool IsSceneEffectActive(Player player)
        {
            return NPC.AnyNPCs(ModContent.NPCType<RoaringKnight>());
        }

        private static bool TryGetActiveKnight(out RoaringKnight knight)
        {
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.type != ModContent.NPCType<RoaringKnight>())
                    continue;

                if (npc.ModNPC is RoaringKnight found)
                {
                    knight = found;
                    return true;
                }
            }

            knight = null;
            return false;
        }
    }
}
