using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SubworldLibrary;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;
using Terraria.ModLoader;
using ReLogic.Content;

namespace DeterministicChaos.Content.Subworlds
{
    // Blue shift shader removed, blue paint on tiles is used instead

    public class DarkDimensionLighting : GlobalTile
    {
        public override void ModifyLight(int i, int j, int type, ref float r, ref float g, ref float b)
        {
            if (DarkDimension.IsInDarkWorld)
            {
                // Subtle ambient light with slight blue tint
                r = System.Math.Max(r, 0.08f);
                g = System.Math.Max(g, 0.10f);
                b = System.Math.Max(b, 0.18f);
            }
        }
    }

    public class DarkDimensionVisualPlayer : ModPlayer
    {
        public override void PostUpdate()
        {
            if (DarkDimension.IsInDarkWorld)
            {
                // Blue-tinted light around the player
                Lighting.AddLight(Player.Center, 0.2f, 0.25f, 0.5f);
            }
        }
    }

    public class DarkDimensionSceneEffect : ModSceneEffect
    {
        public override int Music => MusicLoader.GetMusicSlot(Mod, "Assets/Music/DarkWorld");
        
        // Use BiomeHigh so boss music can override it (bosses use BossMedium/BossHigh)
        public override SceneEffectPriority Priority => SceneEffectPriority.BiomeHigh;
        
        public override bool IsSceneEffectActive(Player player)
        {
            return DarkDimension.IsInDarkWorld;
        }

        public override void SpecialVisuals(Player player, bool isActive)
        {
            if (isActive)
            {
                // Disable sun and moon
                Main.dayTime = false;
                Main.time = 16200; // Midnight
                
                // Force black sky color
                Main.ColorOfTheSkies = Color.Black;
            }
        }
    }
    
    public class DarkDimensionBackground : ModSystem
    {
        public override void ModifySunLightColor(ref Color tileColor, ref Color backgroundColor)
        {
            if (DarkDimension.IsInDarkWorld)
            {
                // Force pure black background
                backgroundColor = Color.Black;
                tileColor = Color.White;
            }
        }
    }
}
