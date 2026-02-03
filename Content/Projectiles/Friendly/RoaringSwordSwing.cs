using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using DeterministicChaos.Content.Buffs;
using DeterministicChaos.Content.Items.Armor;

namespace DeterministicChaos.Content.Projectiles.Friendly
{
    public class RoaringSwordSwing : ModProjectile
    {
        private const int SwingDuration = 12;
        private const int ShrinkDuration = 2;
        private const int LingerDuration = 3;
        private const int TotalDuration = SwingDuration + ShrinkDuration + LingerDuration;
        private const float SwingArc = MathHelper.Pi * 0.85f;
        private const float BaseReach = 5f;
        private const float ExtendedReach = 20f;
        
        private const int SlashFrames = 6;
        private const int SlashFrameWidth = 80;
        private const int SlashFrameHeight = 120;
        
        private ref float SwingDirection => ref Projectile.ai[0];
        private ref float AimAngle => ref Projectile.ai[1];
        
        private float startAngle;
        private int playerDir;
        private bool initialized = false;
        private int slashFrame = 0;
        private int slashFrameCounter = 0;
        private float baseScale = 1f;
        
        private List<Vector2> afterimagePositions = new List<Vector2>();
        private List<float> afterimageRotations = new List<float>();
        private const int MaxAfterimages = 6;
        
        private int timer = 0;

        public override void SetDefaults()
        {
            Projectile.width = 60;
            Projectile.height = 60;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalDuration;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.ownerHitCheck = true;
        }

        public override void AI()
        {
            Player player = Main.player[Projectile.owner];
            if (player == null || !player.active || player.dead)
            {
                Projectile.Kill();
                return;
            }

            if (!initialized)
            {
                initialized = true;
                
                // Use aim angle from ai[1] which was passed from the weapon
                // This ensures multiplayer sync works correctly
                Vector2 aimDirection = AimAngle.ToRotationVector2();
                playerDir = aimDirection.X >= 0 ? 1 : -1;
                player.direction = playerDir;
                
                // When facing left, invert the swing direction so first swing goes down
                float actualSwingDir = SwingDirection;
                if (playerDir == -1)
                {
                    actualSwingDir = -SwingDirection;
                }
                
                startAngle = AimAngle - (SwingArc * 0.5f * actualSwingDir);
                
                float armorBonus = player.GetModPlayer<RoaringArmorPlayer>().swordScaleBonus;
                baseScale = 1f + armorBonus;
                
                SoundEngine.PlaySound(SoundID.Item1, player.Center);
            }

            player.direction = playerDir;

            float currentReach;
            float currentScale;
            float currentRotation;
            
            float actualSwingDirection = SwingDirection;
            if (playerDir == -1)
            {
                actualSwingDirection = -SwingDirection;
            }
            
            if (timer < SwingDuration)
            {
                float swingProgress = timer / (float)SwingDuration;
                float easedProgress = EaseOutQuad(swingProgress);
                
                currentRotation = startAngle + (SwingArc * easedProgress * actualSwingDirection);
                
                if (swingProgress > 0.7f)
                {
                    float extendProgress = (swingProgress - 0.7f) / 0.3f;
                    currentReach = MathHelper.Lerp(BaseReach, ExtendedReach, extendProgress);
                }
                else
                {
                    currentReach = BaseReach;
                }
                currentScale = baseScale;
                
                slashFrameCounter++;
                if (slashFrameCounter >= 2)
                {
                    slashFrameCounter = 0;
                    slashFrame++;
                    if (slashFrame >= SlashFrames)
                        slashFrame = SlashFrames - 1;
                }
            }
            else if (timer < SwingDuration + ShrinkDuration)
            {
                // Fast shrink phase
                float shrinkProgress = (timer - SwingDuration) / (float)ShrinkDuration;
                float easedShrink = EaseOutQuad(shrinkProgress);
                
                float endSwingRotation = startAngle + (SwingArc * actualSwingDirection);
                float additionalRotation = MathHelper.PiOver4 * 0.5f * actualSwingDirection;
                currentRotation = endSwingRotation + (additionalRotation * easedShrink);
                
                currentReach = MathHelper.Lerp(ExtendedReach, BaseReach * 0.5f, easedShrink);
                currentScale = MathHelper.Lerp(baseScale, baseScale * 0.5f, easedShrink);
                
                slashFrame = SlashFrames - 1;
            }
            else
            {
                // Linger phase at half size
                float lingerProgress = (timer - SwingDuration - ShrinkDuration) / (float)LingerDuration;
                float easedLinger = EaseInQuad(lingerProgress);
                
                float endSwingRotation = startAngle + (SwingArc * actualSwingDirection);
                float shrinkRotation = endSwingRotation + (MathHelper.PiOver4 * 0.5f * actualSwingDirection);
                float additionalRotation = MathHelper.PiOver4 * 0.5f * actualSwingDirection;
                currentRotation = shrinkRotation + (additionalRotation * easedLinger);
                
                currentReach = MathHelper.Lerp(BaseReach * 0.5f, BaseReach * 0.2f, easedLinger);
                currentScale = MathHelper.Lerp(baseScale * 0.5f, baseScale * 0.3f, easedLinger);
                
                slashFrame = SlashFrames - 1;
            }

            Projectile.Center = player.Center + new Vector2(currentReach * baseScale, 0f).RotatedBy(currentRotation);
            Projectile.rotation = currentRotation + MathHelper.PiOver4;
            Projectile.scale = currentScale;

            player.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, currentRotation - MathHelper.PiOver2);
            player.heldProj = Projectile.whoAmI;

            Lighting.AddLight(Projectile.Center, 0.6f, 0.2f, 0.6f);

            timer++;
            if (timer >= TotalDuration)
            {
                Projectile.Kill();
            }
        }

        private float EaseOutQuad(float t)
        {
            return 1f - (1f - t) * (1f - t);
        }
        
        private float EaseInQuad(float t)
        {
            return t * t;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (target == null || !target.active)
                return;
            
            RoaringSwordMarkGlobalNPC markNPC = target.GetGlobalNPC<RoaringSwordMarkGlobalNPC>();
            int previousStacks = markNPC.markStacks;
            
            markNPC.AddMark(target, 1);
            
            // Sync mark changes in multiplayer
            if (Main.netMode != NetmodeID.SinglePlayer)
                target.netUpdate = true;
            
            if (previousStacks < RoaringSwordMarkGlobalNPC.MaxStacks && markNPC.markStacks >= RoaringSwordMarkGlobalNPC.MaxStacks)
            {
                SoundEngine.PlaySound(SoundID.NPCDeath6 with { Volume = 0.7f, Pitch = 0.6f }, target.Center);
                
                for (int i = 0; i < 15; i++)
                {
                    Vector2 vel = Main.rand.NextVector2CircularEdge(6f, 6f);
                    Dust dust = Dust.NewDustPerfect(target.Center, DustID.WhiteTorch, vel, 0, Color.White, 1.5f);
                    dust.noGravity = true;
                }
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            Player player = Main.player[Projectile.owner];
            if (player == null || !player.active)
                return false;
            
            Vector2 swordBase = player.Center;
            Vector2 swordDirection = new Vector2(1f, 0f).RotatedBy(Projectile.rotation - MathHelper.PiOver4);
            Vector2 swordTip = player.Center + swordDirection * (160f * baseScale);
            
            float collisionPoint = 0f;
            
            return Collision.CheckAABBvLineCollision(
                targetHitbox.TopLeft(),
                targetHitbox.Size(),
                swordBase,
                swordTip,
                40f * Projectile.scale,
                ref collisionPoint
            );
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Player player = Main.player[Projectile.owner];
            if (player == null || !player.active)
                return false;
                
            Texture2D swordTexture = TextureAssets.Projectile[Type].Value;
            if (swordTexture == null)
                return false;
            
            if (timer < SwingDuration)
            {
                DrawSlashEffect(player);
            }
            
            Vector2 swordOrigin = new Vector2(0f, swordTexture.Height);
            float drawRotation = Projectile.rotation;
            SpriteEffects swordEffects = SpriteEffects.None;
            
            if (playerDir == -1)
            {
                swordEffects = SpriteEffects.FlipHorizontally;
                swordOrigin = new Vector2(swordTexture.Width, swordTexture.Height);
                drawRotation = Projectile.rotation + MathHelper.PiOver2;
            }

            Vector2 drawPos = player.Center - Main.screenPosition;

            for (int i = afterimagePositions.Count - 1; i >= 0; i--)
            {
                float alpha = (1f - (i / (float)MaxAfterimages)) * 0.4f;
                float scale = Projectile.scale * (1f - (i * 0.05f));
                
                float afterimageRotation = afterimageRotations[i];
                if (playerDir == -1)
                {
                    afterimageRotation = afterimageRotations[i] + MathHelper.PiOver2;
                }
                
                Main.EntitySpriteDraw(
                    swordTexture,
                    drawPos,
                    null,
                    Color.White * alpha,
                    afterimageRotation,
                    swordOrigin,
                    scale,
                    swordEffects,
                    0
                );
            }

            Main.EntitySpriteDraw(
                swordTexture,
                drawPos,
                null,
                Color.White,
                drawRotation,
                swordOrigin,
                Projectile.scale,
                swordEffects,
                0
            );

            return false;
        }
        
        private void DrawSlashEffect(Player player)
        {
            Texture2D slashTexture = ModContent.Request<Texture2D>("DeterministicChaos/Content/Projectiles/Friendly/RoaringKnightSwordSlashEffect").Value;
            if (slashTexture == null)
                return;
            
            Rectangle sourceRect = new Rectangle(0, slashFrame * SlashFrameHeight, SlashFrameWidth, SlashFrameHeight);
            Vector2 slashOrigin = new Vector2(SlashFrameWidth * 0.5f, SlashFrameHeight * 0.5f);
            
            Vector2 slashPosition = player.Center + new Vector2(90f * baseScale, 0f).RotatedBy(AimAngle);
            
            float slashRotation;
            SpriteEffects slashEffects = SpriteEffects.None;
            
            if (playerDir == 1)
            {
                slashRotation = AimAngle;
                slashEffects = SpriteEffects.FlipHorizontally;
                
                // Flip vertically when SwingDirection is negative (second swing)
                if (SwingDirection < 0)
                {
                    slashEffects |= SpriteEffects.FlipVertically;
                }
            }
            else
            {
                slashRotation = AimAngle + MathHelper.Pi;
                
                // Flip vertically when SwingDirection is negative (second swing)
                if (SwingDirection < 0)
                {
                    slashEffects = SpriteEffects.FlipVertically;
                }
            }
            
            float swingProgress = timer / (float)SwingDuration;
            float slashAlpha = 1f - (swingProgress * 0.3f);
            float slashScale = (1.8f + (swingProgress * 0.4f)) * baseScale;
            
            for (int i = 0; i < 4; i++)
            {
                Vector2 offset = new Vector2(3f, 0f).RotatedBy(i * MathHelper.PiOver2);
                Main.EntitySpriteDraw(
                    slashTexture,
                    slashPosition + offset - Main.screenPosition,
                    sourceRect,
                    Color.Purple * 0.5f * slashAlpha,
                    slashRotation,
                    slashOrigin,
                    slashScale * 1.1f,
                    slashEffects,
                    0
                );
            }
            
            Main.EntitySpriteDraw(
                slashTexture,
                slashPosition - Main.screenPosition,
                sourceRect,
                Color.White * slashAlpha,
                slashRotation,
                slashOrigin,
                slashScale,
                slashEffects,
                0
            );
        }
    }
}