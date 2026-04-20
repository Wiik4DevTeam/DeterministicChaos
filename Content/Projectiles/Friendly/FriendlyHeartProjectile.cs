using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using DeterministicChaos.Content.Items;
using DeterministicChaos.Content.Items.Accessories;
using DeterministicChaos.Content.Items.BossBags;
using DeterministicChaos.Content.Items.BossSummons;
using DeterministicChaos.Content.Items.Consumables;
using DeterministicChaos.Content.Items.DamageClasses;
using DeterministicChaos.Content.Items.Globals;
using DeterministicChaos.Content.Items.Materials;
using DeterministicChaos.Content.Items.Placeable;
using DeterministicChaos.Content.Items.Rarities;
using DeterministicChaos.Content.Items.Weapons;

namespace DeterministicChaos.Content.Projectiles.Friendly
{
    // Friendly heart projectile spawned by Devilsknife on hit. Lower damage, pierces enemies.
    public class FriendlyHeartProjectile : ModProjectile
    {
        // Orbit tuning (matches HeartBomb pattern)
        private const float SquareRadius = 120f;
        private const float RotationSpeed = 0.03f;
        private const float DriftSpeed = 4.4f;
        private const float InitialSpinMultiplier = 5f;
        private const int SpinSlowdownTicks = 30;
        private const int HitImmunityTicks = 18; // ~0.3 seconds

        private int _age;
        private float accumulatedAngle;
        private Vector2 driftDir;
        private bool driftCached;

        // ai[0] = orbit center X, ai[1] = orbit center Y, ai[2] = angle offset
        private ref float OrbitCenterX => ref Projectile.ai[0];
        private ref float OrbitCenterY => ref Projectile.ai[1];
        private ref float AngleOffset => ref Projectile.ai[2];

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.CultistIsResistantTo[Type] = true;
        }

        public override void SetDefaults()
        {
            Projectile.width = 34;
            Projectile.height = 34;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.penetrate = 3;
            Projectile.timeLeft = 360;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
        }

        public override bool? CanDamage()
        {
            bool fromDeckOfCards = Projectile.GetGlobalProjectile<DeckOfCardsGlobalProjectile>().spawnedByDeckOfCards;
            return fromDeckOfCards || _age >= HitImmunityTicks ? null : false;
        }

        public override void AI()
        {
            _age++;
            bool fromDeckOfCards = Projectile.GetGlobalProjectile<DeckOfCardsGlobalProjectile>().spawnedByDeckOfCards;

            // Cache drift direction on first tick (outward from player)
            if (!driftCached)
            {
                Player owner = Main.player[Projectile.owner];
                Vector2 center = new Vector2(OrbitCenterX, OrbitCenterY);
                driftDir = (center - owner.Center).SafeNormalize(Vector2.Zero);
                driftCached = true;
            }

            // Keep deck hearts anchored so they orbit the same center cleanly, like KingOfHearts.
            if (!fromDeckOfCards)
            {
                OrbitCenterX += driftDir.X * DriftSpeed;
                OrbitCenterY += driftDir.Y * DriftSpeed;
            }

            // Rotating square orbit
            Vector2 orbitCenter = new Vector2(OrbitCenterX, OrbitCenterY);

            // Start fast, decelerate to normal speed (smoothstep)
            float slowT = MathHelper.Clamp(_age / (float)SpinSlowdownTicks, 0f, 1f);
            float eased = slowT * slowT * (3f - 2f * slowT);
            float currentSpeed = MathHelper.Lerp(RotationSpeed * InitialSpinMultiplier, RotationSpeed, eased);
            accumulatedAngle += currentSpeed;

            float angle = AngleOffset + accumulatedAngle;
            Vector2 targetPos = orbitCenter + new Vector2(SquareRadius, 0f).RotatedBy(angle);

            Vector2 toTarget = targetPos - Projectile.Center;
            Projectile.velocity = toTarget * 0.15f;

            Projectile.rotation = 0f;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = new Vector2(tex.Width / 2f, tex.Height / 2f);
            Vector2 pos = Projectile.Center - Main.screenPosition;

            const int TweenFrames = 8;
            float spawnT = MathHelper.Clamp(_age / (float)TweenFrames, 0f, 1f);
            float despawnT = MathHelper.Clamp(Projectile.timeLeft / (float)TweenFrames, 0f, 1f);
            float tweenScale = Math.Min(spawnT, despawnT);
            float drawScale = Projectile.scale * tweenScale;

            Color tint = Color.Lerp(Color.White, new Color(210, 40, 40), 0.7f);
            Color glowColor = new Color(210, 40, 40) * (0.5f * tweenScale);
            Main.EntitySpriteDraw(tex, pos, null, glowColor, 0f, origin, drawScale * 1.3f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, pos, null, tint * tweenScale, 0f, origin, drawScale, SpriteEffects.None, 0);
            return false;
        }
    }
}
