using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.Projectiles.Friendly
{
    public class CookingPotSwing : ModProjectile
    {
        private const float BaseReach = 4f;
        private const float ExtendedReach = 16f;
        private const int SwingDuration = 14;
        private const float SwingArc = MathHelper.Pi * 0.8f; // 144 degree arc

        private ref float SwingDirection => ref Projectile.ai[0];
        private ref float AimAngle => ref Projectile.ai[1];

        private float startAngle;
        private int playerDir;
        private bool initialized = false;
        private int timer = 0;

        private List<float> afterimageRotations = new List<float>();
        private const int MaxAfterimages = 4;

        public override void SetDefaults()
        {
            Projectile.width = 50;
            Projectile.height = 50;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = ModContent.GetInstance<Items.RangedMeleeDamageClass>();
            Projectile.penetrate = -1;
            Projectile.timeLeft = SwingDuration + 5;
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

                if (playerDir == 1)
                {
                    startAngle = MathHelper.PiOver4 + MathHelper.PiOver4;
                }
                else
                {
                    startAngle = MathHelper.Pi - MathHelper.PiOver4 - MathHelper.PiOver4;
                }
            }

            player.direction = playerDir;

            float currentRotation;
            float currentReach;
            float swingProgress;

            if (timer < SwingDuration)
            {
                swingProgress = timer / (float)SwingDuration;
                float easedProgress = EaseOutQuad(swingProgress);

                float swingDir = playerDir == 1 ? -1f : 1f;
                currentRotation = startAngle + (SwingArc * easedProgress * swingDir);

                if (swingProgress < 0.5f)
                {
                    currentReach = MathHelper.Lerp(BaseReach, ExtendedReach, swingProgress * 2f);
                }
                else
                {
                    currentReach = MathHelper.Lerp(ExtendedReach, BaseReach * 1.5f, (swingProgress - 0.5f) * 2f);
                }

                afterimageRotations.Add(currentRotation + MathHelper.PiOver4);
                if (afterimageRotations.Count > MaxAfterimages)
                    afterimageRotations.RemoveAt(0);
            }
            else
            {
                float swingDir = playerDir == 1 ? -1f : 1f;
                currentRotation = startAngle + (SwingArc * swingDir);
                currentReach = BaseReach;
            }

            Projectile.Center = player.Center + new Vector2(currentReach, 0f).RotatedBy(currentRotation);
            Projectile.rotation = currentRotation + MathHelper.PiOver4;
            Projectile.scale = 1f;

            player.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, currentRotation - MathHelper.PiOver2);
            player.heldProj = Projectile.whoAmI;

            timer++;
            if (timer >= SwingDuration + 5)
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

            Vector2 swingDirection = new Vector2(1f, 0f).RotatedBy(Projectile.rotation - MathHelper.PiOver4);
            Vector2 panEnd = player.Center + swingDirection * 80f;

            float collisionPoint = 0f;
            return Collision.CheckAABBvLineCollision(
                targetHitbox.TopLeft(),
                targetHitbox.Size(),
                player.Center,
                panEnd,
                40f * Projectile.scale,
                ref collisionPoint
            );
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
        {
            modifiers.Knockback *= 1.5f;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            SoundEngine.PlaySound(new SoundStyle("DeterministicChaos/Assets/Sounds/FryingPan") { Volume = 0.9f }, target.Center);

            for (int i = 0; i < 8; i++)
            {
                Vector2 vel = Main.rand.NextVector2CircularEdge(4f, 4f);
                Dust dust = Dust.NewDustPerfect(target.Center, DustID.GreenTorch, vel, 0, default, 1.2f);
                dust.noGravity = true;
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

            // Draw afterimages in green
            for (int i = afterimageRotations.Count - 1; i >= 0; i--)
            {
                float alpha = (1f - (i / (float)MaxAfterimages)) * 0.3f;
                float scale = Projectile.scale * (1f - (i * 0.08f));

                float aiRot = afterimageRotations[i];
                if (playerDir == -1)
                    aiRot = afterimageRotations[i] + MathHelper.PiOver2;

                Main.EntitySpriteDraw(
                    tex, drawPos, null,
                    Color.LimeGreen * alpha, aiRot, origin, scale, effects, 0
                );
            }

            // Draw main pot
            Main.EntitySpriteDraw(
                tex, drawPos, null,
                Color.White, drawRotation, origin, Projectile.scale, effects, 0
            );

            return false;
        }
    }
}
