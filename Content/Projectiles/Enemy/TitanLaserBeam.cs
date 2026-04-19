using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using System;

namespace DeterministicChaos.Content.Projectiles.Enemy
{
    // Damaging laser beam fired from a TitanHand.
    // Tiles a 20x20 texture vertically from the hand downward into the ground.
    // Grows in width → pulsates twice → shrinks to nothing over 1 second (60 ticks).
    // A pulsating cyan-blue/yellow sphere is drawn at the hand-laser junction.
    // ai[0] = parent TitanHand NPC whoAmI
    // ai[1] = beam angle (radians)
    public class TitanLaserBeam : ModProjectile
    {
        private const int TILE_SIZE = 20;
        private const float MAX_WIDTH_SCALE = 3f;        // Maximum width multiplier at full size
        private const int TOTAL_LIFETIME = 30;            // 0.5 second total

        // Timing breakdown (in ticks out of 30)
        private const int GROW_END = 8;                   // 0–8: grow to max
        private const int PULSATE_END = 22;               // 8–22: pulsate
        private const int SHRINK_END = 30;                // 22–30: shrink to zero

        // Pulsate parameters
        private const float PULSE_MIN = 0.75f;            // Min width during pulsate (fraction of max)
        private const float PULSE_MAX = 1.0f;             // Max width during pulsate
        private const float PULSE_CYCLES = 2f;            // Number of full pulsations

        // Sphere parameters
        private const float SPHERE_MAX_RADIUS = 40f;      // Max sphere radius in pixels
        private const float SPHERE_PULSE_AMPLITUDE = 8f;   // Pulse amplitude on sphere

        // Damage
        private const int BEAM_DAMAGE = 50;
        private const float BEAM_HIT_WIDTH = 30f;         // Hit detection width in pixels

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 400000;
        }

        public override void SetDefaults()
        {
            Projectile.width = 2;
            Projectile.height = 2;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TOTAL_LIFETIME;
            Projectile.hide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 15; // Hit players every 15 ticks
        }

        public override void AI()
        {
            int parentIdx = (int)Projectile.ai[0];

            // Despawn if parent hand is gone
            if (parentIdx < 0 || parentIdx >= Main.maxNPCs
                || !Main.npc[parentIdx].active
                || Main.npc[parentIdx].type != ModContent.NPCType<NPCs.Bosses.TitanHand>())
            {
                Projectile.Kill();
                return;
            }

            // Stick to parent hand center
            NPC hand = Main.npc[parentIdx];
            Projectile.Center = hand.Center;
            Projectile.rotation = Projectile.ai[1];

            // Play launch sound on first tick (runs on all clients)
            if (Projectile.localAI[0] == 0f && Main.netMode != NetmodeID.Server)
            {
                SoundEngine.PlaySound(new SoundStyle("DeterministicChaos/Assets/Sounds/TitanLaserLaunch")
                {
                    Volume = 0.35f, MaxInstances = 2
                });
            }

            // Track elapsed ticks
            Projectile.localAI[0]++;
        }

        // Calculate the current width scale based on elapsed time (grow → pulsate → shrink).
        public float GetCurrentWidthScale()
        {
            int elapsed = (int)Projectile.localAI[0];
            float maxWidth = MAX_WIDTH_SCALE * (Projectile.ai[2] == 1f ? 2f : 1f);

            if (elapsed <= GROW_END)
            {
                // Grow phase: 0 → maxWidth
                float t = (float)elapsed / GROW_END;
                // Ease-out for snappy growth
                t = 1f - (1f - t) * (1f - t);
                return MathHelper.Lerp(0f, maxWidth, t);
            }
            else if (elapsed <= PULSATE_END)
            {
                // Pulsate phase: oscillate between PULSE_MIN and PULSE_MAX of max width
                float t = (float)(elapsed - GROW_END) / (PULSATE_END - GROW_END);
                float pulse = (float)Math.Sin(t * PULSE_CYCLES * MathHelper.TwoPi);
                float pulseFactor = MathHelper.Lerp(PULSE_MIN, PULSE_MAX, (pulse + 1f) / 2f);
                return maxWidth * pulseFactor;
            }
            else
            {
                // Shrink phase: maxWidth → 0
                float t = (float)(elapsed - PULSATE_END) / (SHRINK_END - PULSATE_END);
                // Ease-in for snappy shrink
                t = t * t;
                return MathHelper.Lerp(maxWidth, 0f, t);
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D tex = TextureAssets.Projectile[Type].Value;

            Vector2 origin = new Vector2(tex.Width / 2f, 0f); // Top-center for tiling downward
            float angle = Projectile.rotation;
            Vector2 direction = angle.ToRotationVector2();
            Vector2 drawStart = Projectile.Center - Main.screenPosition;

            float widthScale = GetCurrentWidthScale();
            if (widthScale <= 0.01f)
                return false;

            // Calculate beam length
            float beamLength = TitanLaserIndicator.CalculateBeamLength(Projectile.Center, direction);
            int tileCount = (int)(beamLength / TILE_SIZE) + 1;

            // Beam color: bright white-cyan core
            Color beamColor = Color.White * Math.Min(widthScale / MAX_WIDTH_SCALE * 1.5f, 1f);

            for (int i = 0; i < tileCount; i++)
            {
                Vector2 tilePos = drawStart + direction * (i * TILE_SIZE);
                sb.Draw(tex, tilePos, null, beamColor, angle - MathHelper.PiOver2,
                    origin, new Vector2(widthScale, 1f), SpriteEffects.None, 0f);
            }

            return false;
        }

        // Draws the pulsating cyan-blue/yellow sphere at the given screen position.
        // Called by TitanHand.PostDraw so the sphere renders above the hand.
        public static void DrawSphere(SpriteBatch sb, Vector2 screenPos, float widthScale, float elapsed)
        {
            if (widthScale <= 0.01f) return;

            float widthFraction = widthScale / MAX_WIDTH_SCALE;
            float pulse = (float)Math.Sin(elapsed * 0.5f) * SPHERE_PULSE_AMPLITUDE;
            float radius = SPHERE_MAX_RADIUS * widthFraction + pulse * widthFraction;

            if (radius <= 1f) return;

            // Use Extra_49 (soft radial glow circle) instead of MagicPixel to draw actual circles
            Texture2D glowTex = Terraria.GameContent.TextureAssets.Extra[49].Value;

            // Outer glow, cyan-blue
            Color cyanBlue = new Color(0, 200, 255, 0) * 0.3f;
            float outerRadius = radius * 1.5f;
            DrawGlowCircle(sb, glowTex, screenPos, outerRadius, cyanBlue);

            // Middle layer, blend cyan and yellow based on pulsation
            float blend = ((float)Math.Sin(elapsed * 0.4f) + 1f) / 2f;
            Color midColor = Color.Lerp(new Color(0, 220, 255, 80), new Color(255, 255, 80, 80), blend);
            DrawGlowCircle(sb, glowTex, screenPos, radius, midColor);

            // Inner core, bright white-yellow
            Color coreColor = new Color(255, 255, 200, 120);
            DrawGlowCircle(sb, glowTex, screenPos, radius * 0.4f, coreColor);
        }

        private static void DrawGlowCircle(SpriteBatch sb, Texture2D glowTex, Vector2 center, float radius, Color color)
        {
            // Scale the glow texture to fit the desired diameter
            float scale = (radius * 2f) / glowTex.Width;
            Vector2 origin = new Vector2(glowTex.Width / 2f, glowTex.Height / 2f);
            sb.Draw(glowTex, center, null, color, 0f, origin, scale, SpriteEffects.None, 0f);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            // Custom collision: check if target intersects the beam line
            float widthScale = GetCurrentWidthScale();
            if (widthScale <= 0.1f)
                return false;

            float angle = Projectile.rotation;
            Vector2 direction = angle.ToRotationVector2();
            float beamLength = TitanLaserIndicator.CalculateBeamLength(Projectile.Center, direction);

            Vector2 beamStart = Projectile.Center;
            Vector2 beamEnd = beamStart + direction * beamLength;

            // Widen the collision based on current width
            float hitWidth = BEAM_HIT_WIDTH * (widthScale / MAX_WIDTH_SCALE);

            float point = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                beamStart, beamEnd, hitWidth, ref point);
        }

        public override bool ShouldUpdatePosition() => false;
    }
}
