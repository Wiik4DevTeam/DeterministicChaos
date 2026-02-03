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
    /// <summary>
    /// Applies an inverted color effect when in the Dark World using tModLoader's Filter system.
    /// </summary>
    public class DarkDimensionScreenEffect : ModSystem
    {
        public const string FilterName = "DeterministicChaos:DarkDimensionInvert";
        
        public override void Load()
        {
            if (Main.dedServ)
                return;
            
            // Load the shader and register the filter
            var shaderRef = new Ref<Effect>(ModContent.Request<Effect>("DeterministicChaos/Assets/Effects/InvertColors", AssetRequestMode.ImmediateLoad).Value);
            Filters.Scene[FilterName] = new Filter(new ScreenShaderData(shaderRef, "InvertPass"), EffectPriority.VeryHigh);
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
                // Activate the filter if not already active
                if (!Filters.Scene[FilterName].IsActive())
                {
                    Filters.Scene.Activate(FilterName);
                }
            }
            else
            {
                // Deactivate if active
                if (Filters.Scene[FilterName].IsActive())
                {
                    Filters.Scene.Deactivate(FilterName);
                }
            }
        }
    }

    /// <summary>
    /// Handles lighting in the Dark World, subtle ambient glow.
    /// </summary>
    public class DarkDimensionLighting : GlobalTile
    {
        public override void ModifyLight(int i, int j, int type, ref float r, ref float g, ref float b)
        {
            if (SubworldSystem.IsActive<DarkDimension>())
            {
                // Low ambient light, inverted this becomes dark with bright highlights
                r = System.Math.Max(r, 0.15f);
                g = System.Math.Max(g, 0.15f);
                b = System.Math.Max(b, 0.15f);
            }
        }
    }

    /// <summary>
    /// ModPlayer for Dark World lighting override.
    /// </summary>
    public class DarkDimensionVisualPlayer : ModPlayer
    {
        public override void PostUpdate()
        {
            if (SubworldSystem.IsActive<DarkDimension>())
            {
                // Moderate light around player, will invert to darker area around player
                Lighting.AddLight(Player.Center, 0.4f, 0.4f, 0.4f);
            }
        }
    }

    /// <summary>
    /// Scene effect for the Dark World, handles music and forces black background.
    /// </summary>
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
    
    /// <summary>
    /// Prevents background drawing in the Dark World.
    /// </summary>
    public class DarkDimensionBackground : ModSystem
    {
        public override void ModifySunLightColor(ref Color tileColor, ref Color backgroundColor)
        {
            if (SubworldSystem.IsActive<DarkDimension>())
            {
                // Force pure black background
                backgroundColor = Color.Black;
                tileColor = Color.White; // Keep tiles visible
            }
        }
    }
}
