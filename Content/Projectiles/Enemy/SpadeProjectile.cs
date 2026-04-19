using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.Projectiles.Enemy
{
    // Spade suit projectile (34x34). Behavior is set by spawner via ai[] fields.
    // ai[0] = behavior mode: 0 = straight line, 1 = circle burst (from bomb), 2 = standalone pull-then-fire
    // ai[1] = extra param (varies by mode)
    // ai[2] = extra param (varies by mode)
    public class SpadeProjectile : ModProjectile
    {
        // Tuning
        public static float DefaultSpeed = 8f;
        public static int DefaultTimeLeft = 600;
        public static int DefaultDamage = 25;

        // Standalone mode (mode 2) tuning
        public static float PullBackSpeed = 2f;
        public static int PullBackDuration = 30;
        public static float LaunchSpeed = 12f;

        private ref float Mode => ref Projectile.ai[0];
        private ref float Timer => ref Projectile.ai[1];
        private ref float LaunchAngle => ref Projectile.ai[2];

        // For standalone mode: cached launch direction
        private Vector2 launchDir;
        private bool launchDirCached;
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
            if ((int)Mode == 2)
            {
                // Standalone pull-then-fire
                // Timer == -999 means frozen/waiting for activation
                if (Timer < -900f)
                {
                    Projectile.velocity = Vector2.Zero;
                    Projectile.rotation = LaunchAngle;
                    return;
                }

                Timer++;

                if (!launchDirCached)
                {
                    launchDir = new Vector2(1f, 0f).RotatedBy(LaunchAngle);
                    launchDirCached = true;
                }

                if (Timer <= PullBackDuration)
                {
                    // Pull back away from launch direction (away from center)
                    if (Timer == 1)
                        SoundEngine.PlaySound(new SoundStyle("DeterministicChaos/Assets/Sounds/JevilProjectileMovement"), Projectile.Center);
                    Projectile.velocity = -launchDir * PullBackSpeed;
                }
                else if (Timer == PullBackDuration + 1)
                {
                    // Fire toward center
                    Projectile.velocity = launchDir * LaunchSpeed;
                }
                // else: continue at launch velocity
            }
            else
            {
                Timer++;
            }

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

            Color glowColor = new Color(40, 40, 200) * (0.5f * tweenScale);
            Main.EntitySpriteDraw(tex, pos, null, glowColor, Projectile.rotation, origin, drawScale * 1.4f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, pos, null, Color.White, Projectile.rotation, origin, drawScale, SpriteEffects.None, 0);
            return false;
        }
    }
}
