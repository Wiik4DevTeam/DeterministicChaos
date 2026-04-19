using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using System;

namespace DeterministicChaos.Content.Projectiles.Enemy
{
    // Hostile projectile fired from the Titan during the damage phase ball attack.
    // Moves at medium speed toward the player, rapidly pulsates in size.
    public class TitanBallAttack : ModProjectile
    {
        private const float MOVE_SPEED = 5.5f;
        private const float PULSATE_SPEED = 0.35f;  // Radians per tick, fast pulsation
        private const float PULSATE_MIN = 0.6f;
        private const float PULSATE_MAX = 1.2f;
        private const int LIFETIME = 300; // 5 seconds

        public override void SetDefaults()
        {
            Projectile.width = 26;
            Projectile.height = 26;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = LIFETIME;
            Projectile.light = 0.6f;
            Projectile.alpha = 0;
            Projectile.damage = 40;
        }

        public override void AI()
        {
            // Play fire sound on first tick (runs on clients when projectile syncs from server)
            if (Projectile.localAI[1] == 0f)
            {
                Projectile.localAI[1] = 1f;
                if (Main.netMode != NetmodeID.Server)
                    SoundEngine.PlaySound(new SoundStyle("DeterministicChaos/Assets/Sounds/TitanBallFire") { MaxInstances = 3 });
            }

            // Rapid pulsation
            Projectile.localAI[0] += PULSATE_SPEED;
            Projectile.scale = MathHelper.Lerp(PULSATE_MIN, PULSATE_MAX,
                ((float)Math.Sin(Projectile.localAI[0]) + 1f) * 0.5f);

            // Slow spin
            Projectile.rotation += 0.08f;

            // Dust trail
            if (Main.rand.NextBool(3))
            {
                Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
                    DustID.WhiteTorch, Scale: 1.0f);
                d.noGravity = true;
                d.velocity *= 0.2f;
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

        public override void OnHitPlayer(Player target, Player.HurtInfo info)
        {
            // Impact dust burst
            for (int i = 0; i < 10; i++)
            {
                Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
                    DustID.WhiteTorch, Scale: 1.5f);
                d.noGravity = true;
                d.velocity = Main.rand.NextVector2Circular(4f, 4f);
            }
        }
    }
}
