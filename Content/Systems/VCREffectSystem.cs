using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using SubworldLibrary;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;
using Terraria.ModLoader;
using DeterministicChaos.Content.Subworlds;

namespace DeterministicChaos.Content.Systems
{
    public class VCREffectSystem : ModSystem
    {
        private const string EffectName = "DeterministicChaos:VCREffect";
        
        // Fade settings
        private float currentIntensity = 0f;
        private float targetIntensity = 0f;
        private const float FadeSpeed = 1.5f; // How fast to fade in/out (per second)
        
        // Track if we were in subworld last frame
        private bool wasInSubworld = false;
        
        // Track if effect loaded successfully
        private bool effectLoaded = false;
        
        public override void Load()
        {
            // Load and register the shader effect
            if (!Main.dedServ)
            {
                try
                {
                    Asset<Effect> effectAsset = ModContent.Request<Effect>("DeterministicChaos/Assets/Effects/VCREffect", AssetRequestMode.ImmediateLoad);
                    
                    if (effectAsset != null && effectAsset.Value != null)
                    {
                        Ref<Effect> effectRef = new Ref<Effect>(effectAsset.Value);
                    
                        ScreenShaderData shaderData = new ScreenShaderData(effectRef, "VCRPass");
                        
                        // Register the filter
                        Filters.Scene[EffectName] = new Filter(shaderData, EffectPriority.High);
                        Filters.Scene[EffectName].Load();
                        effectLoaded = true;
                    }
                }
                catch (System.Exception)
                {
                    effectLoaded = false;
                }
            }
        }

        public override void PostUpdateEverything()
        {
            if (Main.dedServ || !effectLoaded)
                return;
                
            bool inSubworld = SubworldSystem.IsActive<ERAMArena>();
            
            // Detect entering/exiting subworld
            if (inSubworld && !wasInSubworld)
            {
                targetIntensity = 1f;
            }
            else if (!inSubworld && wasInSubworld)
            {
                targetIntensity = 0f;
            }
            
            // Keep target at 1 while in subworld
            if (inSubworld)
            {
                targetIntensity = 1f;
            }
            
            wasInSubworld = inSubworld;
            
            // Smooth fade
            float deltaTime = (float)Main.gameTimeCache.ElapsedGameTime.TotalSeconds;
            if (currentIntensity < targetIntensity)
            {
                currentIntensity = MathHelper.Min(currentIntensity + FadeSpeed * deltaTime, targetIntensity);
            }
            else if (currentIntensity > targetIntensity)
            {
                currentIntensity = MathHelper.Max(currentIntensity - FadeSpeed * deltaTime, targetIntensity);
            }
            
            // Apply or deactivate filter
            if (currentIntensity > 0.01f)
            {
                if (!Filters.Scene[EffectName].IsActive())
                {
                    Filters.Scene.Activate(EffectName);
                }
                
                // Update shader parameters, use standard tModLoader shader methods
                // uOpacity controls intensity, uTime is set via UseProgress
                Filters.Scene[EffectName].GetShader().UseOpacity(currentIntensity);
                Filters.Scene[EffectName].GetShader().UseProgress((float)Main.gameTimeCache.TotalGameTime.TotalSeconds);
            }
            else
            {
                if (Filters.Scene[EffectName].IsActive())
                {
                    Filters.Scene.Deactivate(EffectName);
                }
            }
        }
    }
}
