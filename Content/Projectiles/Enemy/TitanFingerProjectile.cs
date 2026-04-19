using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Audio;
using Terraria.ModLoader;
using System;

namespace DeterministicChaos.Content.Projectiles.Enemy
{
    // A launched TitanFinger projectile. Flies in a straight line, dealing damage on contact.
    // Uses the TitanFinger spritesheet (8 frames, 166x48 each).
    // ai[0] = finger slot (0–3), used for visual frame offset
    // ai[1] = 1 if left hand, 0 if right hand
    public class TitanFingerProjectile : ModProjectile
    {
        private const int FRAME_WIDTH = 166;
        private const int FRAME_HEIGHT = 48;
        private const int FRAME_COUNT = 8;
        private const float ANIM_FPS = 10f;
        private const float SPEED = 14f;
        private const int LIFETIME = 180; // 3 seconds

        private float animTimer = 0f;
        private int animFrame = 0;

        public override string Texture => "DeterministicChaos/Content/NPCs/Bosses/TitanFinger";

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2000;
        }

        public override void SetDefaults()
        {
            Projectile.width = 40;
            Projectile.height = 20;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = LIFETIME;
        }

        public override void AI()
        {
            // Play launch sound on first tick
            if (Projectile.localAI[0] == 0f && Main.netMode != NetmodeID.Server)
            {
                SoundEngine.PlaySound(SoundID.Item73 with { Volume = 0.7f, Pitch = -0.3f }, Projectile.Center);
            }
            Projectile.localAI[0]++;

            // Straight line, no homing
            Projectile.rotation = Projectile.velocity.ToRotation();

            // Trail particles
            if (Main.rand.NextBool(2))
            {
                Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
                    DustID.ShadowbeamStaff, 0f, 0f, 120, default, 1.2f);
                dust.noGravity = true;
                dust.velocity = Projectile.velocity * -0.2f + Main.rand.NextVector2Circular(1f, 1f);
            }
            if (Main.rand.NextBool(4))
            {
                Dust dust2 = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
                    DustID.Smoke, 0f, 0f, 150, default, 0.8f);
                dust2.noGravity = true;
                dust2.velocity = Main.rand.NextVector2Circular(2f, 2f);
            }

            // Animate
            animTimer += ANIM_FPS / 60f;
            if (animTimer >= 1f)
            {
                animTimer -= 1f;
                animFrame = (animFrame + 1) % FRAME_COUNT;
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Rectangle sourceRect = new Rectangle(animFrame * FRAME_WIDTH, 0, FRAME_WIDTH, FRAME_HEIGHT);
            Vector2 origin = new Vector2(FRAME_WIDTH / 2f, FRAME_HEIGHT / 2f);
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            SpriteEffects effects = SpriteEffects.None; // Rotation handles direction

            Main.spriteBatch.Draw(tex, drawPos, sourceRect, lightColor, Projectile.rotation, origin, Projectile.scale, effects, 0f);
            return false;
        }

        public override bool ShouldUpdatePosition() => true;
    }
}
