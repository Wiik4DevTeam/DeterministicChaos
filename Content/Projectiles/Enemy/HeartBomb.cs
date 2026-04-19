using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.Projectiles.Enemy
{
    // Heart bomb (23x23, 2 frames). Falls from sky, explodes into 4 Heart projectiles in a rotating square.
    // ai[0] = target player index
    // ai[1] = target Y position
    public class HeartBomb : ModProjectile
    {
        // Tuning
        public static float FallSpeed = 6f;
        public static int ExplodeDelay = 10;
        public static float SquareRadius = 120f;
        public static int ProjectileDamage = 20;

        private const int FrameWidth = 23;
        private const int FrameHeight = 23;
        private const int FrameCount = 2;
        private const int AnimTicks = 8;

        private int animTick;
        private int animFrame;
        private bool reachedTarget;
        private int explodeTimer;
        private int _age;

        public override void SetDefaults()
        {
            Projectile.width = 23;
            Projectile.height = 23;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
            Projectile.scale = 2f;
        }

        public override void AI()
        {
            _age++;
            if (_age == 1)
                SoundEngine.PlaySound(new SoundStyle("DeterministicChaos/Assets/Sounds/JevilBombFall"), Projectile.Center);
            animTick++;
            if (animTick >= AnimTicks)
            {
                animTick = 0;
                animFrame = (animFrame + 1) % FrameCount;
            }

            float targetY = Projectile.ai[1];

            if (!reachedTarget)
            {
                Projectile.velocity = new Vector2(0f, FallSpeed);

                if (Projectile.Center.Y >= targetY)
                {
                    reachedTarget = true;
                    Projectile.velocity = Vector2.Zero;
                }
            }
            else
            {
                explodeTimer++;
                if (explodeTimer >= ExplodeDelay)
                {
                    Explode();
                    Projectile.Kill();
                }
            }

        }

        private void Explode()
        {
            SoundEngine.PlaySound(new SoundStyle("DeterministicChaos/Assets/Sounds/JevilBombExplode"), Projectile.Center);

            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            int type = ModContent.ProjectileType<HeartProjectile>();
            Vector2 center = Projectile.Center;

            // Spawn 4 hearts in a square (rotating mode 1)
            int targetIdx = (int)Projectile.ai[0];
            for (int i = 0; i < 4; i++)
            {
                float angleOffset = MathHelper.PiOver2 * i; // 0, 90, 180, 270 degrees
                int id = Projectile.NewProjectile(
                    Projectile.GetSource_FromAI(),
                    center + new Vector2(SquareRadius, 0f).RotatedBy(angleOffset),
                    Vector2.Zero,
                    type,
                    ProjectileDamage,
                    0f,
                    Main.myPlayer,
                    1f, // mode = rotating square from bomb
                    center.X
                );
                if (id >= 0 && id < Main.maxProjectiles)
                {
                    Main.projectile[id].ai[2] = center.Y;
                    Main.projectile[id].localAI[0] = angleOffset;
                    Main.projectile[id].localAI[1] = SquareRadius;
                    if (Main.projectile[id].ModProjectile is HeartProjectile heart)
                        heart.ExtraData = targetIdx;
                    Main.projectile[id].netUpdate = true;
                }
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            int frameX = animFrame * FrameWidth;
            Rectangle sourceRect = new Rectangle(frameX, 0, FrameWidth, FrameHeight);
            Vector2 origin = new Vector2(FrameWidth / 2f, FrameHeight / 2f);
            Vector2 pos = Projectile.Center - Main.screenPosition;

            const int TweenFrames = 10;
            float spawnT = MathHelper.Clamp(_age / (float)TweenFrames, 0f, 1f);
            float drawScale = Projectile.scale * spawnT;

            Color glowColor = new Color(210, 40, 40) * (0.5f * spawnT);
            Main.EntitySpriteDraw(tex, pos, sourceRect, glowColor, Projectile.rotation, origin, drawScale * 1.4f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, pos, sourceRect, Color.White, Projectile.rotation, origin, drawScale, SpriteEffects.None, 0);
            return false;
        }
    }
}
