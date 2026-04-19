using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using DeterministicChaos.Content.Systems;

namespace DeterministicChaos.Content.Projectiles.Enemy
{
    // Scythe that slides back and forth along a rotating line through the arena center.
    // ai[0] = base angle offset for this scythe's line
    // ai[1] = line rotation speed (radians/tick, positive = counterclockwise)
    // ai[2] = unused
    // localAI[0] = oscillation timer (set with initial offset for phase staggering)
    public class ScytheProjectileTracking : ModProjectile
    {
        public static float SpinSpeed = 0.15f;
        public static int DefaultTimeLeft = 420;
        public static int OscillationPeriod = 220; // ticks for full back-and-forth cycle

        private ref float BaseAngle => ref Projectile.ai[0];
        private ref float RotationSpeed => ref Projectile.ai[1];

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
            Projectile.localAI[0]++;

            // Spin the sprite
            Projectile.rotation += SpinSpeed;

            if (BossArenaSystem.ActiveBoxes.Count == 0)
                return;

            var box = BossArenaSystem.ActiveBoxes[0];

            // Line half-length extends past arena diagonal so scythe body covers corners
            float halfLength = (float)Math.Sqrt(box.HalfWidth * box.HalfWidth + box.HalfHeight * box.HalfHeight) + 100f;

            // Current line angle (rotates counterclockwise over time)
            float lineAngle = BaseAngle + RotationSpeed * Projectile.localAI[0];

            // Oscillation: ease-in-out quadratic ping-pong along the line
            float period = OscillationPeriod;
            float raw = Projectile.localAI[0] % period;
            if (raw < 0f) raw += period;
            float phase = raw / period; // 0 to 1

            float t;
            bool forward = phase < 0.5f;
            if (forward)
                t = phase * 2f;
            else
                t = (phase - 0.5f) * 2f;

            // Ease-in-out quadratic
            float eased;
            if (t < 0.5f)
                eased = 2f * t * t;
            else
                eased = 1f - (-2f * t + 2f) * (-2f * t + 2f) / 2f;

            // Map to line position: one end to the other with ease
            float distance;
            if (forward)
                distance = MathHelper.Lerp(-halfLength, halfLength, eased);
            else
                distance = MathHelper.Lerp(halfLength, -halfLength, eased);

            Vector2 lineDir = new Vector2((float)Math.Cos(lineAngle), (float)Math.Sin(lineAngle));
            Vector2 targetPos = box.Center + lineDir * distance;

            // Smooth following
            Projectile.Center = Vector2.Lerp(Projectile.Center, targetPos, 0.25f);
            Projectile.velocity = Vector2.Zero;
        }

        public override bool? CanDamage()
        {
            // No damage during the 1-second warmup
            if (_age < WarmupFrames)
                return false;
            return null;
        }

        private const int WarmupFrames = 60; // 1 second warning phase

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = new Vector2(tex.Width / 2f, tex.Height / 2f);
            Vector2 pos = Projectile.Center - Main.screenPosition;

            // Spawn warmup: fade in and grow over 1 second
            float spawnT = MathHelper.Clamp(_age / (float)WarmupFrames, 0f, 1f);
            // Smooth ease-in-out for the warmup
            float easedSpawn = spawnT * spawnT * (3f - 2f * spawnT);

            // Despawn tween (quick)
            const int DespawnFrames = 10;
            float despawnT = MathHelper.Clamp(Projectile.timeLeft / (float)DespawnFrames, 0f, 1f);

            float tweenScale = Math.Min(easedSpawn, despawnT);
            float sizeScale = MathHelper.Lerp(0.3f, 1f, easedSpawn); // start small
            float drawScale = Projectile.scale * tweenScale * sizeScale;
            float drawAlpha = tweenScale;

            // White glow
            Color glowColor = Color.White * (0.35f * drawAlpha);
            Main.EntitySpriteDraw(tex, pos, null, glowColor, Projectile.rotation, origin, drawScale * 1.4f, SpriteEffects.None, 0);

            // Main sprite
            Main.EntitySpriteDraw(tex, pos, null, Color.White * drawAlpha, Projectile.rotation, origin, drawScale, SpriteEffects.None, 0);
            return false;
        }
    }
}
