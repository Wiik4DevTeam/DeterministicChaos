using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SubworldLibrary;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.Subworlds
{
    public class ERAMArenaScreenEffect : ModSceneEffect
    {
        // Music is handled by ERAMSceneEffect for dynamic cutscene/fight switching
        public override int Music => -1; // No music from this effect

        public override SceneEffectPriority Priority => SceneEffectPriority.BossHigh;

        public override bool IsSceneEffectActive(Player player)
        {
            return SubworldSystem.IsActive<ERAMArena>();
        }

        public override void SpecialVisuals(Player player, bool isActive)
        {
            if (isActive)
            {
                // Apply VHS filter
                if (Filters.Scene["DeterministicChaos:VHSFilter"] != null && !Filters.Scene["DeterministicChaos:VHSFilter"].IsActive())
                {
                    Filters.Scene.Activate("DeterministicChaos:VHSFilter", player.Center);
                }
            }
            else
            {
                // Deactivate when leaving
                if (Filters.Scene["DeterministicChaos:VHSFilter"] != null && Filters.Scene["DeterministicChaos:VHSFilter"].IsActive())
                {
                    Filters.Scene["DeterministicChaos:VHSFilter"].Deactivate();
                }
            }
        }
    }
}
