using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using DeterministicChaos.Content.Items;
using DeterministicChaos.Content.Items.Accessories;
using DeterministicChaos.Content.Items.BossBags;
using DeterministicChaos.Content.Items.BossSummons;
using DeterministicChaos.Content.Items.Consumables;
using DeterministicChaos.Content.Items.DamageClasses;
using DeterministicChaos.Content.Items.Globals;
using DeterministicChaos.Content.Items.Materials;
using DeterministicChaos.Content.Items.Placeable;
using DeterministicChaos.Content.Items.Rarities;
using DeterministicChaos.Content.Items.Weapons;

namespace DeterministicChaos.Content.Projectiles.Friendly
{
    public class TrueKnifeMegaSlash : ModProjectile
    {
        private ref float DamageMultiplier => ref Projectile.ai[0];
        private ref float AimAngle => ref Projectile.ai[1];

        private bool initialized = false;
        private int timer = 0;
        private int playerDir;
        private float startAngle;

        private const int SwingDuration = 18;
        private const int LingerFrames = 8;
        private const float SwingArc = MathHelper.Pi * 1.5f;
        private const float MegaReach = 260f;

        // Slash animation
        private int slashFrame = 0;
        private int slashFrameCounter = 0;
        private const int SlashFrames = 6;
        private const int SlashFrameWidth = 80;
        private const int SlashFrameHeight = 120;

        public override void SetDefaults()
        {
            Projectile.width = 60;
            Projectile.height = 60;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = ModContent.GetInstance<MeleeMagicDamageClass>();
            Projectile.penetrate = -1;
            Projectile.timeLeft = SwingDuration + LingerFrames + 2;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.ownerHitCheck = false;
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

                startAngle = AimAngle - (SwingArc * 0.5f * playerDir);

                SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.4f, Volume = 1.1f }, player.Center);

                // Initial burst particles scaled by multiplier
                int burstCount = (int)MathHelper.Clamp(DamageMultiplier * 2, 6, 40);
                for (int i = 0; i < burstCount; i++)
                {
                    Vector2 vel = Main.rand.NextVector2CircularEdge(6f, 6f);
                    float dustScale = MathHelper.Clamp(DamageMultiplier / 5f, 1f, 3f);
                    Dust dust = Dust.NewDustPerfect(player.Center + vel * 5f, DustID.RedTorch, vel * 0.4f, 0, default, dustScale);
                    dust.noGravity = true;
                }
            }

            player.direction = playerDir;

            if (timer < SwingDuration)
            {
                float progress = timer / (float)SwingDuration;
                float easedProgress = EaseOutQuad(progress);
                float currentRotation = startAngle + (SwingArc * easedProgress * playerDir);

                float reachOffset = MegaReach * 0.25f;
                Projectile.Center = player.Center + new Vector2(reachOffset, 0f).RotatedBy(currentRotation);
                Projectile.rotation = currentRotation;

                // Trail particles along the arc
                float dustScale = MathHelper.Clamp(DamageMultiplier / 5f, 1f, 2.5f);
                for (int i = 0; i < 4; i++)
                {
                    float randReach = MegaReach * Main.rand.NextFloat(0.2f, 1f);
                    Vector2 dustPos = player.Center + new Vector2(randReach, 0f).RotatedBy(currentRotation + Main.rand.NextFloat(-0.15f, 0.15f));
                    Vector2 dustVel = new Vector2(0f, -1f).RotatedByRandom(MathHelper.Pi) * Main.rand.NextFloat(0.5f, 2f);
                    Dust dust = Dust.NewDustPerfect(dustPos, DustID.RedTorch, dustVel, 0, default, dustScale);
                    dust.noGravity = true;
                }

                // Slash animation
                slashFrameCounter++;
                if (slashFrameCounter >= 2)
                {
                    slashFrameCounter = 0;
                    slashFrame++;
                    if (slashFrame >= SlashFrames)
                        slashFrame = SlashFrames - 1;
                }

                player.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, currentRotation - MathHelper.PiOver2);
                player.heldProj = Projectile.whoAmI;
            }
            else
            {
                // Linger/fade phase, keep projectile at final arc position
                float endRotation = startAngle + (SwingArc * playerDir);
                float lingerProgress = (timer - SwingDuration) / (float)LingerFrames;

                Projectile.Center = player.Center + new Vector2(MegaReach * 0.25f * (1f - lingerProgress * 0.5f), 0f).RotatedBy(endRotation);
                Projectile.rotation = endRotation;

                player.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, endRotation - MathHelper.PiOver2);
                player.heldProj = Projectile.whoAmI;
            }

            float lightStrength = MathHelper.Clamp(DamageMultiplier / 15f, 0.3f, 1f);
            Lighting.AddLight(Projectile.Center, lightStrength, 0.05f, 0.05f);

            timer++;
            if (timer >= SwingDuration + LingerFrames)
            {
                Projectile.Kill();
            }
        }

        private float EaseOutQuad(float t)
        {
            return 1f - (1f - t) * (1f - t);
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
        {
            modifiers.FinalDamage *= DamageMultiplier;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            Player player = Main.player[Projectile.owner];
            if (player == null || !player.active)
                return false;

            if (timer >= SwingDuration)
                return false;

            float progress = timer / (float)SwingDuration;
            float easedProgress = EaseOutQuad(progress);
            float currentRotation = startAngle + (SwingArc * easedProgress * playerDir);

            Vector2 slashDirection = new Vector2(1f, 0f).RotatedBy(currentRotation);
            Vector2 slashTip = player.Center + slashDirection * MegaReach;

            float collisionPoint = 0f;
            return Collision.CheckAABBvLineCollision(
                targetHitbox.TopLeft(),
                targetHitbox.Size(),
                player.Center,
                slashTip,
                80f,
                ref collisionPoint
            );
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Player player = Main.player[Projectile.owner];
            if (player == null || !player.active)
                return false;

            DrawMegaSlash(player);
            return false;
        }

        private void DrawMegaSlash(Player player)
        {
            Texture2D slashTexture = ModContent.Request<Texture2D>("DeterministicChaos/Content/Projectiles/Friendly/RoaringKnightSwordSlashEffect").Value;
            if (slashTexture == null)
                return;

            float progress;
            float alpha;

            if (timer < SwingDuration)
            {
                progress = timer / (float)SwingDuration;
                alpha = 1f - (progress * 0.2f);
            }
            else
            {
                progress = 1f;
                float lingerProgress = (timer - SwingDuration) / (float)LingerFrames;
                alpha = MathHelper.Lerp(0.8f, 0f, lingerProgress);
            }

            if (alpha <= 0f)
                return;

            Rectangle sourceRect = new Rectangle(0, slashFrame * SlashFrameHeight, SlashFrameWidth, SlashFrameHeight);
            Vector2 slashOrigin = new Vector2(SlashFrameWidth * 0.5f, SlashFrameHeight * 0.5f);

            float easedProgress = EaseOutQuad(MathHelper.Clamp(progress, 0f, 1f));
            float currentRotation = startAngle + (SwingArc * easedProgress * playerDir);

            Vector2 slashPosition = player.Center + new Vector2(MegaReach * 0.4f, 0f).RotatedBy(currentRotation);

            float slashRotation;
            SpriteEffects slashEffects = SpriteEffects.None;

            if (playerDir == 1)
            {
                slashRotation = currentRotation;
                slashEffects = SpriteEffects.FlipHorizontally;
            }
            else
            {
                slashRotation = currentRotation + MathHelper.Pi;
            }

            // Scale increases with multiplier: 2.2x base, up to 3.6x at 15x multiplier
            float multScale = MathHelper.Clamp(DamageMultiplier / 15f, 0.5f, 1f);
            float slashScale = MathHelper.Lerp(2.2f, 3.6f, multScale) * (0.9f + progress * 0.15f);

            // Layer 1: Deep dark red outer glow
            for (int layer = 0; layer < 3; layer++)
            {
                float layerOffset = 4f + layer * 2.5f;
                float layerAlpha = (0.35f - layer * 0.08f) * alpha;

                for (int i = 0; i < 4; i++)
                {
                    Vector2 offset = new Vector2(layerOffset, 0f).RotatedBy(i * MathHelper.PiOver2);
                    Main.EntitySpriteDraw(
                        slashTexture,
                        slashPosition + offset - Main.screenPosition,
                        sourceRect,
                        Color.DarkRed * layerAlpha,
                        slashRotation,
                        slashOrigin,
                        slashScale * (1.12f + layer * 0.06f),
                        slashEffects,
                        0
                    );
                }
            }

            // Layer 2: Red inner glow
            for (int i = 0; i < 4; i++)
            {
                Vector2 offset = new Vector2(2.5f, 0f).RotatedBy(i * MathHelper.PiOver2);
                Main.EntitySpriteDraw(
                    slashTexture,
                    slashPosition + offset - Main.screenPosition,
                    sourceRect,
                    Color.Red * 0.55f * alpha,
                    slashRotation,
                    slashOrigin,
                    slashScale * 1.06f,
                    slashEffects,
                    0
                );
            }

            // Layer 3: White-red core slash
            Color coreColor = Color.Lerp(Color.White, Color.Red, 0.25f);
            Main.EntitySpriteDraw(
                slashTexture,
                slashPosition - Main.screenPosition,
                sourceRect,
                coreColor * alpha,
                slashRotation,
                slashOrigin,
                slashScale,
                slashEffects,
                0
            );
        }

        public override void OnKill(int timeLeft)
        {
            Player player = Main.player[Projectile.owner];
            if (player == null || !player.active)
                return;

            int dustCount = (int)MathHelper.Clamp(DamageMultiplier * 2, 8, 30);
            for (int i = 0; i < dustCount; i++)
            {
                float endRotation = startAngle + (SwingArc * playerDir);
                float randomAngle = endRotation + Main.rand.NextFloat(-SwingArc * 0.3f, SwingArc * 0.3f);
                float randomDist = Main.rand.NextFloat(30f, MegaReach * 0.8f);
                Vector2 dustPos = player.Center + new Vector2(randomDist, 0f).RotatedBy(randomAngle);
                Vector2 vel = (dustPos - player.Center).SafeNormalize(Vector2.UnitX) * Main.rand.NextFloat(1f, 3f);
                Dust dust = Dust.NewDustPerfect(dustPos, DustID.RedTorch, vel, 0, default, 1.5f);
                dust.noGravity = true;
            }
        }
    }
}
