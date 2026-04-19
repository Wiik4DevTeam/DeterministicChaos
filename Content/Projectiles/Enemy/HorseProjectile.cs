using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.Projectiles.Enemy
{
    // Horse projectile (facing left in spritesheet, 2x scale in game).
    // Spawns in a column of 9, bobs up and down, moves horizontally across the screen.
    // ai[0] = horizontal move direction (-1 or 1)
    // ai[1] = bob phase offset (unique per horse in the column)
    // ai[2] = horizontal speed
    public class HorseProjectile : ModProjectile
    {
        // Tuning
        public static float HorizontalSpeed = 4f;
        public static float BobAmplitude = 30f;
        public static float BobFrequency = 0.04f;
        public static int DefaultTimeLeft = 600;
        public static int DefaultDamage = 30;
        public static int ColumnCount = 19;
        public static float VerticalSpacing = 220f;

        private float startY;
        private bool startCaptured;
        private int _age;
        private static int _lastHorseSoundTick = -100;

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
            Projectile.scale = 2f;
        }

        public override void AI()
        {
            _age++;
            // Play spawn sound once per column batch (deduplicated)
            if (_age == 1 && (int)Main.GameUpdateCount - _lastHorseSoundTick > 5)
            {
                _lastHorseSoundTick = (int)Main.GameUpdateCount;
                SoundEngine.PlaySound(new SoundStyle("DeterministicChaos/Assets/Sounds/JevilHorseSpawn"), Projectile.Center);
            }
            if (!startCaptured)
            {
                startY = Projectile.Center.Y;
                startCaptured = true;
            }

            float dir = Projectile.ai[0];
            float bobPhase = Projectile.ai[1];
            float speed = Projectile.ai[2] > 0f ? Projectile.ai[2] : HorizontalSpeed;

            // Move horizontally
            Projectile.velocity.X = dir * speed;

            // Bob up and down
            Projectile.localAI[0]++;
            float bobOffset = (float)System.Math.Sin(Projectile.localAI[0] * BobFrequency + bobPhase) * BobAmplitude;
            Projectile.position.Y = startY - Projectile.height / 2f + bobOffset;
            Projectile.velocity.Y = 0f;

            // Face movement direction (sprite faces left by default)
            Projectile.spriteDirection = dir > 0 ? -1 : 1;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = new Vector2(tex.Width / 2f, tex.Height / 2f);
            Vector2 pos = Projectile.Center - Main.screenPosition;
            SpriteEffects effects = Projectile.spriteDirection == -1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            const int TweenFrames = 10;
            float spawnT = MathHelper.Clamp(_age / (float)TweenFrames, 0f, 1f);
            float despawnT = MathHelper.Clamp(Projectile.timeLeft / (float)TweenFrames, 0f, 1f);
            float tweenScale = spawnT < despawnT ? spawnT : despawnT;
            float drawScale = Projectile.scale * tweenScale;

            Color glowColor = Color.White * (0.35f * tweenScale);
            Main.EntitySpriteDraw(tex, pos, null, glowColor, 0f, origin, drawScale * 1.4f, effects, 0);
            Main.EntitySpriteDraw(tex, pos, null, Color.White, 0f, origin, drawScale, effects, 0);
            return false;
        }
    }
}
