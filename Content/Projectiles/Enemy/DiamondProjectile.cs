using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.Projectiles.Enemy
{
    // Diamond suit projectile (34x34). Behavior set by spawner via ai[] fields.
    // ai[0] = behavior mode: 0 = straight line, 1 = from bomb (multi-speed), 2 = standalone rise-from-below
    // ai[1] = extra param
    // ai[2] = extra param
    public class DiamondProjectile : ModProjectile
    {
        // Tuning
        public static float DefaultSpeed = 8f;
        public static int DefaultTimeLeft = 300;
        public static int DefaultDamage = 25;

        // Standalone rise mode (mode 2)
        public static int DipDuration = 20;
        public static float DipSpeed = 2f;
        public static float RiseSpeed = 10f;

        private ref float Mode => ref Projectile.ai[0];
        private ref float Timer => ref Projectile.ai[1];

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
            Timer++;

            if ((int)Mode == 2)
            {
                // Rise from below: dip down briefly, then shoot upward
                if (Timer <= DipDuration)
                {
                    Projectile.velocity = new Vector2(0f, DipSpeed);
                }
                else if (Timer == DipDuration + 1)
                {
                    SoundEngine.PlaySound(new SoundStyle("DeterministicChaos/Assets/Sounds/JevilProjectileMovement"), Projectile.Center);
                    Projectile.velocity = new Vector2(0f, -RiseSpeed);
                }
            }

            // Rotate to face velocity direction (sprite faces right)
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

            Color glowColor = new Color(230, 150, 30) * (0.5f * tweenScale);
            Main.EntitySpriteDraw(tex, pos, null, glowColor, Projectile.rotation, origin, drawScale * 1.4f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, pos, null, Color.White, Projectile.rotation, origin, drawScale, SpriteEffects.None, 0);
            return false;
        }
    }
}
