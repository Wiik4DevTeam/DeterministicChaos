using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.Projectiles.Friendly
{
    /// <summary>
    /// Friendly laser beam fired by AppendageHandProjectile.
    /// Reuses the TitanLaserBeam texture at a larger scale.
    /// ai[0] = parent AppendageHandProjectile whoAmI
    /// ai[1] = hand side (0 = left, 1 = right)
    /// </summary>
    public class AppendageLaser : ModProjectile
    {
        private const int TILE_SIZE = 20;
        private const float MAX_WIDTH_SCALE = 5f;
        private const int TOTAL_LIFETIME = 60;        // 1 second
        private const float BEAM_LENGTH = 2000f;

        // Timing breakdown (in ticks out of 60)
        private const int GROW_END = 10;
        private const int PULSATE_END = 48;
        private const int SHRINK_END = 60;

        private const float PULSE_MIN = 0.75f;
        private const float PULSE_MAX = 1.0f;
        private const float PULSE_CYCLES = 3f;

        private const float BEAM_HIT_WIDTH = 50f;

        public override string Texture => "DeterministicChaos/Content/Projectiles/Enemy/TitanLaserBeam";

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 400000;
        }

        // Draw lasers behind regular projectiles so hands render on top
        public override void DrawBehind(int index, System.Collections.Generic.List<int> behindNPCsAndTiles,
            System.Collections.Generic.List<int> behindNPCs, System.Collections.Generic.List<int> behindProjectiles,
            System.Collections.Generic.List<int> overPlayers, System.Collections.Generic.List<int> overWiresUI)
        {
            behindProjectiles.Add(index);
        }

        public override void SetDefaults()
        {
            Projectile.width = 2;
            Projectile.height = 2;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TOTAL_LIFETIME;
            Projectile.hide = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 15;
        }

        public override void AI()
        {
            int parentIdx = (int)Projectile.ai[0];

            // Track parent hand projectile
            if (parentIdx < 0 || parentIdx >= Main.maxProjectiles
                || !Main.projectile[parentIdx].active
                || Main.projectile[parentIdx].type != ModContent.ProjectileType<AppendageHandProjectile>())
            {
                Projectile.Kill();
                return;
            }

            Projectile parent = Main.projectile[parentIdx];
            AppendageHandProjectile handProj = parent.ModProjectile as AppendageHandProjectile;
            if (handProj == null) { Projectile.Kill(); return; }

            // Follow the correct hand's position
            bool isRight = Projectile.ai[1] == 1f;
            Projectile.Center = isRight ? handProj.RightHandPos : handProj.LeftHandPos;

            // Aim at whip target, then nearest enemy
            Player owner = Main.player[Projectile.owner];
            int target = -1;
            int whipTarget = owner.MinionAttackTargetNPC;
            if (whipTarget != -1 && whipTarget < Main.maxNPCs && Main.npc[whipTarget].active && !Main.npc[whipTarget].friendly)
                target = whipTarget;

            if (target == -1)
            {
                float bestDist = 2000f;
                for (int i = 0; i < Main.maxNPCs; i++)
                {
                    NPC npc = Main.npc[i];
                    if (!npc.active || npc.friendly || npc.dontTakeDamage) continue;
                    float dist = Vector2.Distance(Projectile.Center, npc.Center);
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        target = i;
                    }
                }
            }

            if (target != -1)
            {
                float targetAngle = (Main.npc[target].Center - Projectile.Center).ToRotation();
                Projectile.rotation = targetAngle;
            }

            // Play launch sound on first tick
            if (Projectile.localAI[0] == 0f && Main.netMode != NetmodeID.Server)
            {
                SoundEngine.PlaySound(new SoundStyle("DeterministicChaos/Assets/Sounds/TitanLaserLaunch")
                {
                    Volume = 0.35f
                }, Projectile.Center);
            }

            Projectile.localAI[0]++;
        }

        private float GetCurrentWidthScale()
        {
            int elapsed = (int)Projectile.localAI[0];

            if (elapsed <= GROW_END)
            {
                float t = (float)elapsed / GROW_END;
                t = 1f - (1f - t) * (1f - t); // ease-out
                return MathHelper.Lerp(0f, MAX_WIDTH_SCALE, t);
            }
            else if (elapsed <= PULSATE_END)
            {
                float t = (float)(elapsed - GROW_END) / (PULSATE_END - GROW_END);
                float pulse = (float)Math.Sin(t * PULSE_CYCLES * MathHelper.TwoPi);
                float pulseFactor = MathHelper.Lerp(PULSE_MIN, PULSE_MAX, (pulse + 1f) / 2f);
                return MAX_WIDTH_SCALE * pulseFactor;
            }
            else
            {
                float t = (float)(elapsed - PULSATE_END) / (SHRINK_END - PULSATE_END);
                t = t * t; // ease-in
                return MathHelper.Lerp(MAX_WIDTH_SCALE, 0f, t);
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D tex = TextureAssets.Projectile[Type].Value;

            Vector2 origin = new Vector2(tex.Width / 2f, 0f);
            float angle = Projectile.rotation;
            Vector2 direction = angle.ToRotationVector2();
            Vector2 drawStart = Projectile.Center - Main.screenPosition;

            float widthScale = GetCurrentWidthScale();
            if (widthScale <= 0.01f)
                return false;

            int tileCount = (int)(BEAM_LENGTH / TILE_SIZE) + 1;
            Color beamColor = Color.White * Math.Min(widthScale / MAX_WIDTH_SCALE * 1.5f, 1f);

            for (int i = 0; i < tileCount; i++)
            {
                Vector2 tilePos = drawStart + direction * (i * TILE_SIZE);
                sb.Draw(tex, tilePos, null, beamColor, angle - MathHelper.PiOver2,
                    origin, new Vector2(widthScale, 1f), SpriteEffects.None, 0f);
            }

            return false;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            float widthScale = GetCurrentWidthScale();
            if (widthScale <= 0.1f)
                return false;

            float angle = Projectile.rotation;
            Vector2 direction = angle.ToRotationVector2();

            Vector2 beamStart = Projectile.Center;
            Vector2 beamEnd = beamStart + direction * BEAM_LENGTH;

            float hitWidth = BEAM_HIT_WIDTH * (widthScale / MAX_WIDTH_SCALE);

            float point = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                beamStart, beamEnd, hitWidth, ref point);
        }

        public override bool ShouldUpdatePosition() => false;
    }
}
