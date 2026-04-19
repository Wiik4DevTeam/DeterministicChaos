using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.Projectiles.Enemy
{
    // Diamond bomb (23x23, 2 frames). Falls from sky, explodes into 3 Diamond projectiles aimed at player with decreasing speed.
    // ai[0] = target player index
    // ai[1] = target Y position
    public class DiamondBomb : ModProjectile
    {
        // Tuning
        public static float FallSpeed = 6f;
        public static int ExplodeDelay = 10;
        public static int DiamondCount = 3;
        public static float BaseProjectileSpeed = 10f;
        public static float SpeedDecrement = 2.5f;
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

            int targetIdx = (int)Projectile.ai[0];
            if (targetIdx < 0 || targetIdx >= Main.maxPlayers)
                return;

            Player target = Main.player[targetIdx];
            if (!target.active || target.dead)
                return;

            Vector2 toPlayer = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX);
            int type = ModContent.ProjectileType<DiamondProjectile>();

            for (int i = 0; i < DiamondCount; i++)
            {
                float speed = BaseProjectileSpeed - (SpeedDecrement * i);
                if (speed < 2f) speed = 2f;

                int id = Projectile.NewProjectile(
                    Projectile.GetSource_FromAI(),
                    Projectile.Center,
                    toPlayer * speed,
                    type,
                    ProjectileDamage,
                    0f,
                    Main.myPlayer,
                    0f // mode = straight line
                );
                if (id >= 0 && id < Main.maxProjectiles)
                    Main.projectile[id].netUpdate = true;
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

            Color glowColor = new Color(230, 150, 30) * (0.5f * spawnT);
            Main.EntitySpriteDraw(tex, pos, sourceRect, glowColor, Projectile.rotation, origin, drawScale * 1.4f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, pos, sourceRect, Color.White, Projectile.rotation, origin, drawScale, SpriteEffects.None, 0);
            return false;
        }
    }
}
