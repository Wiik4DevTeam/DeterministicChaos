using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.Projectiles.Enemy
{
    // Club suit projectile (34x34). Behavior set by spawner via ai[] fields.
    // ai[0] = behavior mode: 0 = straight line aimed, 1 = from bomb (3-spread)
    // ai[1] = unused
    // ai[2] = unused
    // Velocity is set directly by spawner.
    public class ClubProjectile : ModProjectile
    {
        // Tuning
        public static float DefaultSpeed = 9f;
        public static int DefaultTimeLeft = 300;
        public static int DefaultDamage = 25;
        public static float SpreadAngle = MathHelper.ToRadians(15f);

        private int _age;

        public override void SetDefaults()
        {
            Projectile.width = 34;
            Projectile.height = 34;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = DefaultTimeLeft;
        }

        public override void AI()
        {
            _age++;
            // Rotate to face velocity direction
            if (Projectile.velocity.LengthSquared() > 0.1f)
                Projectile.rotation = Projectile.velocity.ToRotation();
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = new Vector2(tex.Width / 2f, tex.Height / 2f);
            Vector2 pos = Projectile.Center - Main.screenPosition;

            const int TweenFrames = 10;
            float spawnT = MathHelper.Clamp(_age / (float)TweenFrames, 0f, 1f);
            float despawnT = MathHelper.Clamp(Projectile.timeLeft / (float)TweenFrames, 0f, 1f);
            float tweenScale = spawnT < despawnT ? spawnT : despawnT;
            float drawScale = Projectile.scale * tweenScale;

            Color glowColor = new Color(40, 160, 40) * (0.5f * tweenScale);
            Main.EntitySpriteDraw(tex, pos, null, glowColor, Projectile.rotation, origin, drawScale * 1.4f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, pos, null, Color.White, Projectile.rotation, origin, drawScale, SpriteEffects.None, 0);
            return false;
        }
    }
}
