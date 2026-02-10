using Terraria;
using Terraria.ModLoader;
using SubworldLibrary;
using DeterministicChaos.Content.NPCs.Bosses;
using DeterministicChaos.Content.Subworlds;

namespace DeterministicChaos.Content.Systems
{
    public class ERAMSceneEffect : ModSceneEffect
    {
        public override SceneEffectPriority Priority => SceneEffectPriority.BossHigh;

        public override int Music
        {
            get
            {
                // If dialogue is active, play cutscene music
                if (DialogueSystem.Instance != null && DialogueSystem.Instance.IsDialogueActive)
                {
                    return MusicLoader.GetMusicSlot(Mod, "Assets/Music/ERAMCutscene");
                }
                
                // Check if ERAM boss exists
                bool bossExists = false;
                bool bossDefeated = false;
                
                for (int i = 0; i < Main.maxNPCs; i++)
                {
                    if (Main.npc[i].active && Main.npc[i].type == ModContent.NPCType<ERAM>())
                    {
                        bossExists = true;
                        // Check if boss is in Defeated state (ai[0] == 5)
                        if (Main.npc[i].ai[0] == 5f)
                        {
                            bossDefeated = true;
                        }
                        break;
                    }
                }
                
                // If boss is defeated, play cutscene music
                if (bossDefeated)
                {
                    return MusicLoader.GetMusicSlot(Mod, "Assets/Music/ERAMCutscene");
                }
                
                // If boss exists and is fighting, play boss music
                if (bossExists)
                {
                    return MusicLoader.GetMusicSlot(Mod, "Assets/Music/ERAM");
                }
                
                // No boss yet (pre-fight dialogue), play cutscene music
                return MusicLoader.GetMusicSlot(Mod, "Assets/Music/ERAMCutscene");
            }
        }

        public override bool IsSceneEffectActive(Player player)
        {
            // Active when in ERAM arena
            return SubworldSystem.IsActive<ERAMArena>();
        }
    }
}
