using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SubworldLibrary;
using Terraria;
using Terraria.Audio;
using Terraria.ModLoader;
using DeterministicChaos.Content.Subworlds;

namespace DeterministicChaos.Content.Systems
{
    // Handles the pixelation and fade to black transition when using the ERAM summon item
    public class ERAMTransitionSystem : ModSystem
    {
        // Sound effect
        private static SoundStyle PixelEnterSound;
        
        // Transition state
        private static bool isTransitioning = false;
        private static float transitionProgress = 0f;
        private static int transitionTimer = 0;
        private static int targetPlayerIndex = -1;
        
        // Transition duration in ticks (60 ticks = 1 second)
        private const int TransitionDuration = 60;
        
        // Render target for pixelation effect
        private static RenderTarget2D screenTarget;
        private static RenderTarget2D pixelatedTarget;
        
        public static bool IsTransitioning => isTransitioning;
        
        public static void StartTransition(int playerIndex)
        {
            if (!isTransitioning)
            {
                isTransitioning = true;
                transitionProgress = 0f;
                transitionTimer = 0;
                targetPlayerIndex = playerIndex;
                
                // Play the enter sound
                SoundEngine.PlaySound(PixelEnterSound);
            }
        }
        
        public override void Load()
        {
            if (!Main.dedServ)
            {
                Main.OnResolutionChanged += OnResolutionChanged;
                On_Main.DoDraw += DrawTransitionOverlay;
                
                // Initialize sound
                PixelEnterSound = new SoundStyle("DeterministicChaos/Assets/Sounds/PixelEnter")
                {
                    Volume = 0.9f
                };
            }
        }
        
        public override void Unload()
        {
            if (!Main.dedServ)
            {
                Main.OnResolutionChanged -= OnResolutionChanged;
                On_Main.DoDraw -= DrawTransitionOverlay;
            }
            
            // Don't dispose render targets here, they will be disposed automatically
            // Disposing on unload can cause threading issues
            screenTarget = null;
            pixelatedTarget = null;
        }
        
        private void OnResolutionChanged(Vector2 newSize)
        {
            CreateRenderTargets();
        }
        
        private static void CreateRenderTargets()
        {
            screenTarget?.Dispose();
            pixelatedTarget?.Dispose();
            
            screenTarget = new RenderTarget2D(
                Main.graphics.GraphicsDevice,
                Main.screenWidth,
                Main.screenHeight,
                false,
                SurfaceFormat.Color,
                DepthFormat.None
            );
            
            pixelatedTarget = new RenderTarget2D(
                Main.graphics.GraphicsDevice,
                Main.screenWidth,
                Main.screenHeight,
                false,
                SurfaceFormat.Color,
                DepthFormat.None
            );
        }
        
        public override void PostUpdateEverything()
        {
            if (Main.dedServ)
                return;
            
            if (!isTransitioning)
                return;
            
            transitionTimer++;
            transitionProgress = (float)transitionTimer / TransitionDuration;
            
            // Clamp progress
            if (transitionProgress >= 1f)
            {
                transitionProgress = 1f;
                
                // Transition complete, teleport to arena
                isTransitioning = false;
                transitionTimer = 0;
                transitionProgress = 0f;
                
                // Only enter the subworld if this is the player who activated it
                if (targetPlayerIndex >= 0 && targetPlayerIndex == Main.myPlayer)
                {
                    SubworldSystem.Enter<ERAMArena>();
                }
                
                targetPlayerIndex = -1;
            }
        }
        
        private void DrawTransitionOverlay(On_Main.orig_DoDraw orig, Main self, GameTime gameTime)
        {
            // Call original draw first
            orig(self, gameTime);
            
            if (!isTransitioning || transitionProgress <= 0f)
                return;
            
            // Ensure render targets exist
            if (screenTarget == null || screenTarget.IsDisposed || 
                screenTarget.Width != Main.screenWidth || screenTarget.Height != Main.screenHeight)
            {
                CreateRenderTargets();
            }
            
            SpriteBatch spriteBatch = Main.spriteBatch;
            GraphicsDevice device = Main.graphics.GraphicsDevice;
            
            // Draw pixelation and fade overlay on top of everything
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);
            
            // Calculate pixelation level (starts normal, ends very pixelated)
            // Progress 0 = no pixelation, Progress 1 = max pixelation
            int pixelSize = (int)MathHelper.Lerp(1, 32, transitionProgress * transitionProgress);
            
            // Draw a black overlay that fades in
            // Start fading to black at 50% progress, fully black at 100%
            float blackAlpha = MathHelper.Clamp((transitionProgress - 0.3f) / 0.7f, 0f, 1f);
            
            // Draw pixelated squares effect
            if (pixelSize > 1)
            {
                // Create a pixelation effect by drawing colored rectangles
                Texture2D pixel = Terraria.GameContent.TextureAssets.MagicPixel.Value;
                
                int cols = (Main.screenWidth / pixelSize) + 1;
                int rows = (Main.screenHeight / pixelSize) + 1;
                
                // Sample colors from screen and draw pixelated blocks
                // Since we cannot easily sample the screen, we will simulate pixelation
                // by drawing a semi-transparent grid that increases in opacity
                float gridAlpha = transitionProgress * 0.8f;
                
                for (int x = 0; x < cols; x++)
                {
                    for (int y = 0; y < rows; y++)
                    {
                        // Create a checkerboard-like pixelation pattern
                        bool dark = ((x + y) % 2 == 0);
                        float cellAlpha = gridAlpha * (dark ? 0.6f : 0.3f);
                        
                        // Add some noise/variation
                        float noise = (float)((x * 31 + y * 17) % 100) / 100f;
                        cellAlpha *= 0.5f + noise * 0.5f;
                        
                        Rectangle rect = new Rectangle(x * pixelSize, y * pixelSize, pixelSize, pixelSize);
                        spriteBatch.Draw(pixel, rect, Color.Black * cellAlpha);
                    }
                }
            }
            
            // Draw the final black fade overlay
            if (blackAlpha > 0f)
            {
                Texture2D pixel = Terraria.GameContent.TextureAssets.MagicPixel.Value;
                spriteBatch.Draw(pixel, new Rectangle(0, 0, Main.screenWidth, Main.screenHeight), Color.Black * blackAlpha);
            }
            
            spriteBatch.End();
        }
    }
}
