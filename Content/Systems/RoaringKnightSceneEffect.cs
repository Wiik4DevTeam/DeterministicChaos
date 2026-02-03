using Terraria;
using Terraria.ModLoader;
using DeterministicChaos.Content.NPCs.Bosses;

namespace DeterministicChaos.Content.Systems
{
    public class RoaringKnightSceneEffect : ModSceneEffect
    {
        public override SceneEffectPriority Priority => SceneEffectPriority.BossHigh;

        public override int Music => MusicLoader.GetMusicSlot(Mod, "Assets/Music/KnightMusic");

        public override bool IsSceneEffectActive(Player player)
        {
            return NPC.AnyNPCs(ModContent.NPCType<RoaringKnight>());
        }
    }
}
