using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using DeterministicChaos.Content.Items;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.Projectiles.Friendly
{
    public class RealKnifeSwing : ModProjectile
    {
        // Same 4-combo system as RustyKnifeSwing, but with a slash effect that extends range
        
        private const float BaseReach = 5f;
        private const float ExtendedReach = 20f;

        // Slash effect settings (reusing RoaringKnightSwordSlashEffect at smaller scale)
        private const int SlashFrames = 6;
        private const int SlashFrameWidth = 80;
        private const int SlashFrameHeight = 120;
        
        private ref float SwingDirection => ref Projectile.ai[0];
        private ref float AimAngle => ref Projectile.ai[1];
        
        private float startAngle;
        private int playerDir;
        private bool initialized = false;
        private int timer = 0;

        // Slash animation state
        private int slashFrame = 0;
        private int slashFrameCounter = 0;

        // Per-swing parameters (set on init based on combo)
        private int swingDuration;
        private float swingArc;
        private float baseScale;
        private float reachMultiplier;
        
        private List<float> afterimageRotations = new List<float>();
        private const int MaxAfterimages = 5;

        public override void SetDefaults()
        {
            Projectile.width = 28;
            Projectile.height = 28;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = ModContent.GetInstance<MeleeMagicDamageClass>();
            Projectile.penetrate = -1;
            Projectile.timeLeft = 30;
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
                
                Vector2 aimDirection = AimAngle.ToRotationVector2();
                playerDir = aimDirection.X >= 0 ? 1 : -1;
                player.direction = playerDir;

                float actualSwingDir = SwingDirection;
                if (playerDir == -1)
                    actualSwingDir = -SwingDirection;

                int comboIndex = player.GetModPlayer<RealKnifePlayer>().swingCombo % 4;

                switch (comboIndex)
                {
                    case 0: // Wide feral slash
                        swingDuration = 10;
                        swingArc = MathHelper.Pi * 0.9f;
                        baseScale = 0.64f;
                        reachMultiplier = 1f;
                        break;
                    case 1: // Quick downward flick
                        swingDuration = 9;
                        swingArc = MathHelper.Pi * 1.0f;
                        baseScale = 0.57f;
                        reachMultiplier = 1f;
                        break;
                    case 2: // Short diagonal swipe
                        swingDuration = 10;
                        swingArc = MathHelper.Pi * 1.0f;
                        baseScale = 0.61f;
                        reachMultiplier = 1.2f;
                        break;
                    case 3: // Wild overhead chop
                        swingDuration = 11;
                        swingArc = MathHelper.Pi * 1.1f;
                        baseScale = 0.68f;
                        reachMultiplier = 1.1f;
                        break;
                    default:
                        swingDuration = 10;
                        swingArc = MathHelper.Pi * 1.0f;
                        baseScale = 0.64f;
                        reachMultiplier = 1f;
                        break;
                }

                Projectile.timeLeft = swingDuration + 3;

                startAngle = AimAngle - (swingArc * 0.5f * actualSwingDir);
                
                SoundEngine.PlaySound(new SoundStyle("DeterministicChaos/Assets/Sounds/DTSwing") { Volume = 0.7f, PitchVariance = 0.3f }, Projectile.Center);
            }

            player.direction = playerDir;

            float actualSwingDirection = SwingDirection;
            if (playerDir == -1)
                actualSwingDirection = -SwingDirection;

            float currentRotation;
            float currentReach;
            float currentScale;

            if (timer < swingDuration)
            {
                float progress = timer / (float)swingDuration;
                float easedProgress = EaseOutQuad(progress);

                currentRotation = startAngle + (swingArc * easedProgress * actualSwingDirection);

                // Extend reach toward end of swing
                if (progress > 0.5f)
                {
                    float extendProgress = (progress - 0.5f) / 0.5f;
                    currentReach = MathHelper.Lerp(BaseReach, ExtendedReach * reachMultiplier, extendProgress);
                }
                else
                {
                    currentReach = BaseReach;
                }
                currentScale = baseScale;

                // Record afterimages
                afterimageRotations.Add(currentRotation + MathHelper.PiOver4);
                if (afterimageRotations.Count > MaxAfterimages)
                    afterimageRotations.RemoveAt(0);

                // Advance slash animation
                slashFrameCounter++;
                if (slashFrameCounter >= 2)
                {
                    slashFrameCounter = 0;
                    slashFrame++;
                    if (slashFrame >= SlashFrames)
                        slashFrame = SlashFrames - 1; // Hold on last frame
                }
            }
            else
            {
                // Quick linger/shrink
                float lingerProgress = (timer - swingDuration) / 3f;
                float endRotation = startAngle + (swingArc * actualSwingDirection);
                currentRotation = endRotation + (MathHelper.PiOver4 * 0.3f * actualSwingDirection * lingerProgress);
                currentReach = MathHelper.Lerp(ExtendedReach * reachMultiplier * 0.5f, BaseReach * 0.2f, lingerProgress);
                currentScale = MathHelper.Lerp(baseScale, baseScale * 0.3f, lingerProgress);
            }

            Projectile.Center = player.Center + new Vector2(currentReach * baseScale, 0f).RotatedBy(currentRotation);
            Projectile.rotation = currentRotation + MathHelper.PiOver4;
            Projectile.scale = currentScale;

            player.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, currentRotation - MathHelper.PiOver2);
            player.heldProj = Projectile.whoAmI;

            timer++;
            if (timer >= swingDuration + 3)
            {
                Projectile.Kill();
            }
        }

        private float EaseOutQuad(float t)
        {
            return 1f - (1f - t) * (1f - t);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            Player player = Main.player[Projectile.owner];
            if (player == null || !player.active)
                return false;

            Vector2 swordDirection = new Vector2(1f, 0f).RotatedBy(Projectile.rotation - MathHelper.PiOver4);
            // Extended reach compared to RustyKnife (140 vs 100) due to slash effect
            Vector2 swordTip = player.Center + swordDirection * (140f * baseScale);

            float collisionPoint = 0f;
            return Collision.CheckAABBvLineCollision(
                targetHitbox.TopLeft(),
                targetHitbox.Size(),
                player.Center,
                swordTip,
                50f * Projectile.scale, // Wider collision due to slash (40 -> 50)
                ref collisionPoint
            );
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            Player player = Main.player[Projectile.owner];
            if (player != null && player.active)
            {
                player.statMana += 20;
                if (player.statMana > player.statManaMax2)
                    player.statMana = player.statManaMax2;
                player.ManaEffect(20);
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Player player = Main.player[Projectile.owner];
            if (player == null || !player.active)
                return false;

            Texture2D tex = TextureAssets.Projectile[Type].Value;
            if (tex == null)
                return false;

            // Draw slash effect behind the knife during the active swing
            if (timer < swingDuration)
            {
                DrawSlashEffect(player);
            }

            Vector2 origin = new Vector2(0f, tex.Height);
            float drawRotation = Projectile.rotation;
            SpriteEffects effects = SpriteEffects.None;

            if (playerDir == -1)
            {
                effects = SpriteEffects.FlipHorizontally;
                origin = new Vector2(tex.Width, tex.Height);
                drawRotation = Projectile.rotation + MathHelper.PiOver2;
            }

            Vector2 drawPos = player.Center - Main.screenPosition;

            // Draw afterimages
            for (int i = afterimageRotations.Count - 1; i >= 0; i--)
            {
                float alpha = (1f - (i / (float)MaxAfterimages)) * 0.3f;
                float scale = Projectile.scale * (1f - (i * 0.06f));

                float aiRot = afterimageRotations[i];
                if (playerDir == -1)
                    aiRot = afterimageRotations[i] + MathHelper.PiOver2;

                Main.EntitySpriteDraw(
                    tex, drawPos, null,
                    Color.Red * alpha, aiRot, origin, scale, effects, 0
                );
            }

            // Draw main knife
            Main.EntitySpriteDraw(
                tex, drawPos, null,
                Color.White, drawRotation, origin, Projectile.scale, effects, 0
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

            // Position slash at knife tip, closer than the sword (50f vs 90f)
            Vector2 slashPosition = player.Center + new Vector2(50f * baseScale, 0f).RotatedBy(AimAngle);

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

            float swingProgress = timer / (float)swingDuration;
            float slashAlpha = 1f - (swingProgress * 0.3f);
            // Smaller scale than the Roaring Sword's slash (0.9 base vs 1.8)
            float slashScale = (0.9f + (swingProgress * 0.2f)) * baseScale;

            // Red glow outlines (Determination theme instead of purple)
            for (int i = 0; i < 4; i++)
            {
                Vector2 offset = new Vector2(2f, 0f).RotatedBy(i * MathHelper.PiOver2);
                Main.EntitySpriteDraw(
                    slashTexture,
                    slashPosition + offset - Main.screenPosition,
                    sourceRect,
                    Color.Red * 0.5f * slashAlpha,
                    slashRotation,
                    slashOrigin,
                    slashScale * 1.1f,
                    slashEffects,
                    0
                );
            }

            // Main white slash
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
