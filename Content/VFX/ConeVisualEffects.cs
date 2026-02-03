using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.VFX
{
    public class ConeVisualEffect : ModProjectile
    {
        private float scrollOffset;
        private BasicEffect effect;
        
        public override void SetStaticDefaults()
        {
            // Visual only, no hitbox
        }

        public override void SetDefaults()
        {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.aiStyle = -1;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 999999;
            Projectile.alpha = 0;
        }

        public override void AI()
        {
            // ai[0] = NPC index (owner)
            // ai[1] = cone angle in degrees
            
            int npcIndex = (int)Projectile.ai[0];
            if (npcIndex < 0 || npcIndex >= Main.maxNPCs || !Main.npc[npcIndex].active)
            {
                Projectile.Kill();
                return;
            }

            NPC owner = Main.npc[npcIndex];
            
            // Position at boss center
            Projectile.Center = owner.Center;
            
            // Find target player for direction
            Player target = null;
            float bestDist = float.MaxValue;
            for (int i = 0; i < Main.maxPlayers; i++)
            {
                Player p = Main.player[i];
                if (!p.active || p.dead) continue;
                
                float dist = Vector2.Distance(owner.Center, p.Center);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    target = p;
                }
            }
            
            if (target != null)
            {
                Vector2 toPlayer = target.Center - owner.Center;
                Projectile.rotation = toPlayer.ToRotation();
            }
            
            // Scroll the texture
            scrollOffset += 0.15f;
            if (scrollOffset >= 1f)
                scrollOffset -= 1f;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            SpriteBatch spriteBatch = Main.spriteBatch;
            
            // Try to load textures
            Texture2D baseTexture = null;
            Texture2D maskTexture = null;
            
            try
            {
                baseTexture = ModContent.Request<Texture2D>("DeterministicChaos/Content/VFX/ConeVisualEffect").Value;
            }
            catch 
            { 
                return false; // Don't draw if base texture doesn't exist
            }
            
            try
            {
                maskTexture = ModContent.Request<Texture2D>("DeterministicChaos/Content/VFX/ConeVisualEffect_Mask").Value;
            }
            catch 
            { 
                return false; // Don't draw if mask doesn't exist
            }
            
            float coneAngle = MathHelper.ToRadians(Projectile.ai[1]);
            float coneLength = 800f;
            float halfAngle = coneAngle * 0.5f;
            
            // End sprite batch to use custom blend
            spriteBatch.End();
            
            // Start with additive blending for glow effect
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.AnisotropicClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            
            // Draw the cone as multiple segments
            int segments = 80;
            
            for (int i = 0; i < segments; i++)
            {
                float progress = i / (float)segments;
                float nextProgress = (i + 1) / (float)segments;
                
                float dist1 = progress * coneLength;
                float dist2 = nextProgress * coneLength;
                
                // Calculate the width at each distance using tan
                float width1 = dist1 * (float)Math.Tan(halfAngle);
                float width2 = dist2 * (float)Math.Tan(halfAngle);
                
                // Calculate center positions along the rotation axis
                Vector2 centerPos1 = Projectile.Center + new Vector2(dist1, 0f).RotatedBy(Projectile.rotation);
                Vector2 centerPos2 = Projectile.Center + new Vector2(dist2, 0f).RotatedBy(Projectile.rotation);
                
                // Convert to screen space
                Vector2 screenPos = centerPos1 - Main.screenPosition;
                
                // Calculate dimensions for this segment
                float segmentLength = dist2 - dist1;
                float avgWidth = (width1 + width2);
                
                // Source rectangle for base texture, use scrolling for vertical tiling
                Rectangle baseSource = new Rectangle(
                    0,
                    (int)(scrollOffset * baseTexture.Height) % baseTexture.Height,
                    baseTexture.Width,
                    (int)(baseTexture.Height * 0.1f) // Sample a small slice
                );
                
                // Source rectangle for mask, sample horizontally based on distance
                Rectangle maskSource = new Rectangle(
                    (int)(progress * maskTexture.Width),
                    0,
                    Math.Max(1, (int)((nextProgress - progress) * maskTexture.Width)),
                    maskTexture.Height
                );
                
                // Get mask alpha value at this distance
                float maskAlpha = MathHelper.Lerp(1f, 0f, progress);
                Color tint = Color.White * maskAlpha;
                
                // Draw base texture segment
                spriteBatch.Draw(baseTexture, screenPos, baseSource, tint,
                    Projectile.rotation, 
                    new Vector2(0, baseSource.Height * 0.5f),
                    new Vector2(segmentLength / baseSource.Width, avgWidth / baseSource.Height),
                    SpriteEffects.None, 0f);
            }
            
            // Restart sprite batch with normal settings
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            
            return false;
        }
    }
}