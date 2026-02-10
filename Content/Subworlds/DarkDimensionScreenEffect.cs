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
    public class DarkDimensionScreenEffect : ModSystem
    {
        public const string FilterName = "DeterministicChaos:DarkDimensionBlueShift";
        
        public override void Load()
        {
            if (Main.dedServ)
                return;
            
            // Load the blue shift shader and register the filter
            var shaderRef = new Ref<Effect>(ModContent.Request<Effect>("DeterministicChaos/Assets/Effects/BlueShift", AssetRequestMode.ImmediateLoad).Value);
            Filters.Scene[FilterName] = new Filter(new ScreenShaderData(shaderRef, "BlueShiftPass"), EffectPriority.VeryHigh);
            Filters.Scene[FilterName].Load();
        }

        public override void Unload()
        {
            // Filters are automatically cleaned up by tModLoader
        }

        public override void PostUpdateEverything()
        {
            bool isInDarkDimension = SubworldSystem.IsActive<DarkDimension>();
            
            if (isInDarkDimension && !Main.gameMenu)
            {
                if (!Filters.Scene[FilterName].IsActive())
                {
                    Filters.Scene.Activate(FilterName);
                }
            }
            else
            {
                if (Filters.Scene[FilterName].IsActive())
                {
                    Filters.Scene.Deactivate(FilterName);
                }
            }
        }
    }

    public class DarkDimensionLighting : GlobalTile
    {
        public override void ModifyLight(int i, int j, int type, ref float r, ref float g, ref float b)
        {
            if (SubworldSystem.IsActive<DarkDimension>())
            {
                // Subtle ambient light with slight blue tint to complement the shader
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
            if (SubworldSystem.IsActive<DarkDimension>())
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
            return SubworldSystem.IsActive<DarkDimension>();
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
            if (SubworldSystem.IsActive<DarkDimension>())
            {
                // Force pure black background
                backgroundColor = Color.Black;
                tileColor = Color.White;
            }
        }
    }
}
