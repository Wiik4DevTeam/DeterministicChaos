using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using DeterministicChaos.Content.Systems;

namespace DeterministicChaos.Content.Projectiles.Enemy
{
    // Falling scythe (1.5x scale). Falls and spins fast from the sky.
    // When it hits the arena floor, spawns a white pillar.
    // ai[0] = arena bottom Y (optional fallback to active arena)
    // ai[1] = timer
    public class ScytheProjectileFalling : ModProjectile
    {
        // Tuning
        public static float FallSpeed = 12f;
        public static float SpinSpeed = 0.3f;
        public static int DefaultDamage = 28;
        public static int DefaultTimeLeft = 300;

        // Pillar tuning
        public static float PillarHeight = 2000f;
        public static float PillarStartWidth = 170f;
        public static int PillarDuration = 30;
        public static int PillarDamage = 32;

        private bool hitGround;
        private int pillarTimer;
        private float pillarWidth;
        private Vector2 groundPos;
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
            Projectile.scale = 1.5f;
        }

        public override void AI()
        {
            _age++;
            if (!hitGround)
            {
                Projectile.velocity.Y = FallSpeed;
                Projectile.rotation += SpinSpeed;

                float arenaBottom = Projectile.ai[0];
                if (arenaBottom <= 0f && BossArenaSystem.ActiveBoxes.Count > 0)
                {
                    var box = BossArenaSystem.ActiveBoxes[0];
                    arenaBottom = box.Center.Y + box.HalfHeight;
                }

                if (arenaBottom > 0f && Projectile.Center.Y >= arenaBottom)
                {
                    Projectile.Center = new Vector2(Projectile.Center.X, arenaBottom);
                    TriggerGroundHit();
                }
            }
            else
            {
                // Scythe stays on ground, pillar shrinks
                Projectile.velocity = Vector2.Zero;
                Projectile.tileCollide = false;
                Projectile.rotation += SpinSpeed * 0.5f;

                pillarTimer++;
                pillarWidth = PillarStartWidth * (1f - (float)pillarTimer / PillarDuration);
                if (pillarWidth <= 0f || pillarTimer >= PillarDuration)
                {
                    Projectile.Kill();
                }

                // Damage local player if in pillar (per-client for multiplayer)
                if (Main.netMode != NetmodeID.Server && pillarWidth > 0f)
                {
                    Rectangle pillarRect = new Rectangle(
                        (int)(groundPos.X - pillarWidth / 2f),
                        (int)(groundPos.Y - PillarHeight),
                        (int)pillarWidth,
                        (int)PillarHeight
                    );

                    Player localP = Main.LocalPlayer;
                    if (!localP.dead && !localP.immune && localP.Hitbox.Intersects(pillarRect))
                    {
                        localP.Hurt(Terraria.DataStructures.PlayerDeathReason.ByProjectile(Main.myPlayer, Projectile.whoAmI), PillarDamage, 0);
                    }
                }
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity)
        {
            return false;
        }

        private void TriggerGroundHit()
        {
            if (hitGround) return;
            hitGround = true;
            groundPos = Projectile.Center;
            pillarWidth = PillarStartWidth;
            pillarTimer = 0;
            Projectile.tileCollide = false;
            SoundEngine.PlaySound(new SoundStyle("DeterministicChaos/Assets/Sounds/JevilBombExplode"), Projectile.Center);

            for (int i = 0; i < 20; i++)
            {
                Vector2 dustVel = new Vector2(Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-5f, -1f));
                Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.Cloud, dustVel.X, dustVel.Y, 100, Color.White, 1.5f);
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = new Vector2(tex.Width / 2f, tex.Height / 2f);

            // Draw pillar if active
            if (hitGround && pillarWidth > 0f)
            {
                Texture2D pixel = TextureAssets.MagicPixel.Value;
                float fade = pillarWidth / PillarStartWidth;
                Color pillarColor = Color.White * fade * 0.7f;

                Rectangle pillarRect = new Rectangle(
                    (int)(groundPos.X - pillarWidth / 2f - Main.screenPosition.X),
                    (int)(groundPos.Y - PillarHeight - Main.screenPosition.Y),
                    (int)pillarWidth,
                    (int)PillarHeight
                );

                Main.spriteBatch.Draw(pixel, pillarRect, pillarColor);
                return false; // Don't draw scythe once pillar is active
            }

            // Draw scythe with glow + spawn tween
            Vector2 pos = Projectile.Center - Main.screenPosition;
            const int TweenFrames = 10;
            float spawnT = MathHelper.Clamp(_age / (float)TweenFrames, 0f, 1f);
            float drawScale = Projectile.scale * spawnT;

            Color glowColor = Color.White * (0.35f * spawnT);
            Main.EntitySpriteDraw(tex, pos, null, glowColor, Projectile.rotation, origin, drawScale * 1.4f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, pos, null, Color.White, Projectile.rotation, origin, drawScale, SpriteEffects.None, 0);
            return false;
        }
    }
}
