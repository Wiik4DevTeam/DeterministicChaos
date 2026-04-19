using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.Projectiles.Enemy
{
    // Visual indicator that appears on the Titan during charge-up.
    // Grows in size over its lifetime, then despawns.
    // Not hostile, purely visual.
    public class TitanBallWhite : ModProjectile
    {
        private const float MAX_SCALE = 1.6f;

        public override void SetDefaults()
        {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 60; // 1 second charge
            Projectile.light = 0.8f;
            Projectile.alpha = 0;
        }

        public override void AI()
        {
            // Grow from tiny to MAX_SCALE over lifetime
            float progress = 1f - (Projectile.timeLeft / 60f);
            Projectile.scale = MathHelper.Lerp(0.1f, MAX_SCALE, progress);

            // Slow spin
            Projectile.rotation += 0.05f;

            // Stay anchored to parent (stored in ai[0] = NPC whoAmI)
            int parentIdx = (int)Projectile.ai[0];
            if (parentIdx >= 0 && parentIdx < Main.maxNPCs && Main.npc[parentIdx].active)
            {
                // Position at roughly where the star would be (FACE_OFFSET_Y = -20)
                Projectile.Center = Main.npc[parentIdx].Center + new Vector2(0f, -20f);
            }

            // Pulsing glow dust
            if (Main.rand.NextBool(3))
            {
                Dust d = Dust.NewDustDirect(Projectile.Center - new Vector2(8f), 16, 16, DustID.WhiteTorch, Scale: 1.4f);
                d.noGravity = true;
                d.velocity *= 0.3f;
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D tex = Terraria.GameContent.TextureAssets.Projectile[Type].Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = new Vector2(tex.Width / 2f, tex.Height / 2f);

            Color drawColor = Color.White;

            Main.EntitySpriteDraw(tex, drawPos, null, drawColor, Projectile.rotation,
                origin, Projectile.scale, SpriteEffects.None, 0);

            return false;
        }
    }
}
