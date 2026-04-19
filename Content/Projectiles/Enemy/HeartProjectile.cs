using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.Projectiles.Enemy
{
    // Heart suit projectile (34x34). Behavior set by spawner via ai[] fields.
    // ai[0] = behavior mode: 0 = straight line, 1 = rotating square (from bomb), 2 = standalone rotating square
    // ai[1] = orbit center X (for rotating modes)
    // ai[2] = orbit center Y (for rotating modes)
    // localAI[0] = orbit angle offset
    // localAI[1] = orbit radius
    // localAI[2] = timer
    // localAI[3] = mode 1: target player index / mode 2: shared drift angle (set by spawner)
    public class HeartProjectile : ModProjectile
    {
        // Tuning
        public static float DefaultSpeed = 7f;
        public static int DefaultTimeLeft = 360;
        public static int DefaultDamage = 25;

        // Square rotation tuning
        public static float SquareRadius = 180f;
        public static float RotationSpeed = 0.03f;

        // Standalone mode: spin then drift
        public static int SpinDuration = 60; // 1 second
        public static float DriftSpeed = 6.6f;
        public static float InitialSpinMultiplier = 5f; // standalone hearts start spinning this much faster
        public static int SpinSlowdownTicks = 30;       // 0.5 seconds to decelerate to normal

        // Center drift speed (bomb hearts move center toward player)
        public static float BombCenterDriftSpeed = 4.4f;

        // Private timer (localAI only has indices 0-2)
        private float timer;

        // Cached drift direction for mode 1 (captured once, not tracked)
        private Vector2 cachedDriftDir;
        private bool driftDirCached;

        // Extra data set by spawner: mode 1 = target player index, mode 2 = shared drift angle
        public float ExtraData;

        // Accumulated angle for variable-speed rotation
        private float accumulatedAngle;
        private int _age;

        private ref float Mode => ref Projectile.ai[0];
        private ref float OrbitCenterX => ref Projectile.ai[1];
        private ref float OrbitCenterY => ref Projectile.ai[2];

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
            timer++;

            if ((int)Mode == 1 || (int)Mode == 2)
            {
                // Play movement sound on first tick of standalone mode
                if ((int)Mode == 2 && timer == 1)
                    SoundEngine.PlaySound(new SoundStyle("DeterministicChaos/Assets/Sounds/JevilProjectileMovement"), Projectile.Center);

                // Mode 1 (bomb): drift center in direction of player (captured once)
                if ((int)Mode == 1)
                {
                    if (!driftDirCached)
                    {
                        int targetIdx = (int)ExtraData;
                        if (targetIdx >= 0 && targetIdx < Main.maxPlayers)
                        {
                            Player t = Main.player[targetIdx];
                            if (t.active && !t.dead)
                            {
                                Vector2 center = new Vector2(OrbitCenterX, OrbitCenterY);
                                cachedDriftDir = (t.Center - center).SafeNormalize(Vector2.Zero);
                            }
                        }
                        driftDirCached = true;
                    }

                    Vector2 drift = cachedDriftDir * BombCenterDriftSpeed;
                    OrbitCenterX += drift.X;
                    OrbitCenterY += drift.Y;
                }

                // Mode 2 (standalone): after spin, drift center in shared random direction
                if ((int)Mode == 2 && timer > SpinDuration)
                {
                    float sharedAngle = ExtraData;
                    Vector2 drift = new Vector2(DriftSpeed, 0f).RotatedBy(sharedAngle);
                    OrbitCenterX += drift.X;
                    OrbitCenterY += drift.Y;
                }

                // Rotating square mode
                Vector2 orbitCenter = new Vector2(OrbitCenterX, OrbitCenterY);
                float angleOffset = Projectile.localAI[0];
                float radius = Projectile.localAI[1] > 0f ? Projectile.localAI[1] : SquareRadius;

                // Standalone hearts start spinning fast and decelerate to normal over 0.5s
                float currentSpeed = RotationSpeed;
                if ((int)Mode == 2)
                {
                    float slowT = MathHelper.Clamp(timer / (float)SpinSlowdownTicks, 0f, 1f);
                    float eased = slowT * slowT * (3f - 2f * slowT); // smoothstep
                    currentSpeed = MathHelper.Lerp(RotationSpeed * InitialSpinMultiplier, RotationSpeed, eased);
                }
                accumulatedAngle += currentSpeed;

                float angle = angleOffset + accumulatedAngle;
                Vector2 targetPos = orbitCenter + new Vector2(radius, 0f).RotatedBy(angle);

                // Move towards orbit position
                Vector2 toTarget = targetPos - Projectile.Center;
                Projectile.velocity = toTarget * 0.15f;
            }

            // Keep upright orientation from spritesheet
            Projectile.rotation = 0f;
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

            Color glowColor = new Color(210, 40, 40) * (0.5f * tweenScale);
            Main.EntitySpriteDraw(tex, pos, null, glowColor, Projectile.rotation, origin, drawScale * 1.4f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, pos, null, Color.White, Projectile.rotation, origin, drawScale, SpriteEffects.None, 0);
            return false;
        }
    }
}
