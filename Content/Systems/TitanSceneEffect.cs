using Terraria;
using Terraria.ModLoader;
using DeterministicChaos.Content.NPCs.Bosses;

namespace DeterministicChaos.Content.Systems
{
    public class TitanSceneEffect : ModSceneEffect
    {
        public override SceneEffectPriority Priority => SceneEffectPriority.BossHigh;

        // Return 0 (no music), actual music is managed by TitanMusicSystem via SoundEngine
        public override int Music => 0;

        public override bool IsSceneEffectActive(Player player)
        {
            return NPC.AnyNPCs(ModContent.NPCType<TitanBody>());
        }
    }
}
