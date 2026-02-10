using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SubworldLibrary;
using Terraria;
using Terraria.ModLoader;
using ReLogic.Content;
using System.Collections.Generic;
using DeterministicChaos.Content.Subworlds;
using DeterministicChaos.Content.Tiles;

namespace DeterministicChaos.Content.VFX
{
    public class FountainVisualSystem : ModSystem
    {
        private static Asset<Texture2D> fountainTexture;
        private static float scrollOffset = 0f;
        private static float colorHue = 0f;
        
        // Trail particles
        private static List<FountainParticle> particles = new List<FountainParticle>();
        private const int MaxParticles = 100;
        
        private struct FountainParticle
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public float Life;
            public float MaxLife;
            public float Hue;
            public float Scale;
        }

        public override void PostUpdateEverything()
        {
            if (!SubworldSystem.IsActive<DarkDimension>() || Main.dedServ)
                return;

            // Scroll upward
            scrollOffset -= 2f;
            if (fountainTexture != null && fountainTexture.IsLoaded)
            {
                if (scrollOffset < -fountainTexture.Value.Height)
                    scrollOffset += fountainTexture.Value.Height;
            }
            
            // Cycle through rainbow colors slowly
            colorHue += 0.002f;
            if (colorHue > 1f)
                colorHue -= 1f;

            // Update and spawn particles
            UpdateParticles();
        }

        private void UpdateParticles()
        {
            if (DarkPortal.PortalX < 0)
                return;

            // Update existing particles
            for (int i = particles.Count - 1; i >= 0; i--)
            {
                var p = particles[i];
                p.Position += p.Velocity;
                p.Life -= 1f / 60f;
                p.Velocity.Y -= 0.05f; // Float upward faster
                particles[i] = p;
                
                if (p.Life <= 0)
                    particles.RemoveAt(i);
            }

            // Spawn new particles along the fountain
            if (particles.Count < MaxParticles && Main.rand.NextBool(3))
            {
                float worldX = DarkPortal.PortalX * 16 + 8;
                float worldY = Main.rand.NextFloat(0, Main.maxTilesY * 16);
                
                particles.Add(new FountainParticle
                {
                    Position = new Vector2(worldX + Main.rand.NextFloat(-16, 16), worldY),
                    Velocity = new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), Main.rand.NextFloat(-3f, -1f)),
                    Life = Main.rand.NextFloat(1f, 3f),
                    MaxLife = 3f,
                    Hue = colorHue + Main.rand.NextFloat(-0.1f, 0.1f),
                    Scale = Main.rand.NextFloat(0.5f, 1.5f)
                });
            }
        }

        public override void Load()
        {
            base.Load();
            if (Main.dedServ)
                return;
                
            fountainTexture = ModContent.Request<Texture2D>("DeterministicChaos/Content/VFX/FountainVisual");
            
            // Hook to draw behind tiles
            Terraria.On_Main.DrawBackgroundBlackFill += DrawFountainBehindTiles;
        }
        
        public override void Unload()
        {
            Terraria.On_Main.DrawBackgroundBlackFill -= DrawFountainBehindTiles;
            fountainTexture = null;
            particles?.Clear();
        }
        
        private void DrawFountainBehindTiles(Terraria.On_Main.orig_DrawBackgroundBlackFill orig, Main self)
        {
            orig(self);
            
            if (!SubworldSystem.IsActive<DarkDimension>() || Main.dedServ)
                return;
                
            if (DarkPortal.PortalX < 0)
                return;
                
            if (fountainTexture == null || !fountainTexture.IsLoaded)
                return;

            Texture2D tex = fountainTexture.Value;
            SpriteBatch spriteBatch = Main.spriteBatch;
            
            // End current spritebatch if active, then start our own
            try { spriteBatch.End(); } catch { }
            
            // Begin drawing behind tiles with additive blending
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearWrap, 
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            // Calculate screen position
            float worldX = DarkPortal.PortalX * 16 + 8;
            float screenX = worldX - Main.screenPosition.X;
            
            // Draw the fountain tiled from top to bottom of visible area
            int texHeight = tex.Height;
            int texWidth = tex.Width;
            
            // Calculate how many tiles we need to cover the screen
            float startY = -Main.screenPosition.Y + scrollOffset;
            
            // Adjust start to tile properly
            while (startY > 0)
                startY -= texHeight;
            
            // Draw tiled fountain with trail effect (multiple layers with offset)
            // Draw from back to front (largest/most faded first)
            for (int trail = 5; trail >= 0; trail--)
            {
                float trailOffset = trail * 25f;
                float trailAlpha = 1f - (trail * 0.15f);
                float trailScale = 1f + (trail * 0.3f); // Expand each afterimage
                float trailHue = colorHue - (trail * 0.04f);
                if (trailHue < 0) trailHue += 1f;
                
                Color trailColor = HueToColor(trailHue) * trailAlpha * 0.7f;
                
                float scaledWidth = texWidth * trailScale;
                
                for (float y = startY - trailOffset; y < Main.screenHeight + texHeight; y += texHeight)
                {
                    Vector2 drawPos = new Vector2(screenX, y + texHeight / 2);
                    
                    spriteBatch.Draw(
                        tex,
                        drawPos,
                        null,
                        trailColor,
                        0f,
                        new Vector2(texWidth / 2, texHeight / 2),
                        new Vector2(trailScale, 1f),
                        SpriteEffects.None,
                        0f
                    );
                }
            }

            // Draw particles
            foreach (var p in particles)
            {
                float alpha = p.Life / p.MaxLife;
                Color particleColor = HueToColor(p.Hue % 1f) * alpha;
                
                Vector2 screenPos = p.Position - Main.screenPosition;
                
                spriteBatch.Draw(
                    tex,
                    screenPos,
                    null,
                    particleColor,
                    0f,
                    new Vector2(tex.Width / 2, tex.Height / 2),
                    p.Scale * 0.3f * alpha,
                    SpriteEffects.None,
                    0f
                );
            }

            spriteBatch.End();
            
            // Restart the spritebatch in the default state for the rest of rendering
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, 
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        private static Color HueToColor(float hue)
        {
            float r, g, b;
            
            int i = (int)(hue * 6f);
            float f = hue * 6f - i;
            float q = 1f - f;
            
            switch (i % 6)
            {
                case 0: r = 1; g = f; b = 0; break;
                case 1: r = q; g = 1; b = 0; break;
                case 2: r = 0; g = 1; b = f; break;
                case 3: r = 0; g = q; b = 1; break;
                case 4: r = f; g = 0; b = 1; break;
                default: r = 1; g = 0; b = q; break;
            }
            
            return new Color(r, g, b);
        }
    }
}
