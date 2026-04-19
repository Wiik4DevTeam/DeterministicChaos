using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.Projectiles.Friendly
{
    // The high-velocity bolt fired by the Cascade bow.
    // Phase 0 (flying)  : travels fast with slight gravity.
    // Phase 1 (stuck NPC) / Phase 2 (stuck tile): stays on impact point, pulls nearby
    //   enemies toward itself, and calls in copies of the loaded arrows from afar.
    // On explosion: massive AoE damage after the suction phase ends.
    public class CascadeProjectile : ModProjectile
    {
        // Borrow TitansArrowProjectile texture until a dedicated sprite is supplied.
        public override string Texture =>
            "DeterministicChaos/Content/Projectiles/Friendly/TitansArrowProjectile";

        private const float SuctionRadius   = 260f;
        private const float SuctionForce    = 3.5f;
        private const int   SuctionDuration = 60;    // 1 s at 60 fps
        private const int   HomingArrowCount = 8;
        private const float SpawnMinDistance = 300f;
        private const float SpawnMaxDistance = 520f;
        private const float HomingArrowSpeed = 18f;
        private const float ExplosionRadius = 240f;
        private const float ExplosionScale  = 2.5f;  // multiplied on top of Projectile.damage

        // ai[0] = state, ai[1] = timer, ai[2] = original arrow projectile type
        private float State { get => Projectile.ai[0]; set => Projectile.ai[0] = value; }
        private float Timer { get => Projectile.ai[1]; set => Projectile.ai[1] = value; }
        private int   ArrowType => (int)Projectile.ai[2];

        private int     stuckNPC      = -1;
        private Vector2 stuckOffset   = Vector2.Zero;
        private bool    hasExploded   = false;
        private int     originalDamage = 0;
        private int     spawnedArrowCount = 0;

        public override void SendExtraAI(BinaryWriter writer)
        {
            writer.Write(stuckNPC);
            writer.WriteVector2(stuckOffset);
            writer.Write(hasExploded);
            writer.Write(originalDamage);
            writer.Write(spawnedArrowCount);
        }

        public override void ReceiveExtraAI(BinaryReader reader)
        {
            stuckNPC       = reader.ReadInt32();
            stuckOffset    = reader.ReadVector2();
            hasExploded    = reader.ReadBoolean();
            originalDamage = reader.ReadInt32();
            spawnedArrowCount = reader.ReadInt32();
        }

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailCacheLength[Type] = 12;
            ProjectileID.Sets.TrailingMode[Type]     = 2;
        }

        public override void OnSpawn(Terraria.DataStructures.IEntitySource source)
        {
            originalDamage       = Projectile.damage;
            Projectile.netUpdate = true;
        }

        public override void SetDefaults()
        {
            Projectile.width       = 12;
            Projectile.height      = 12;
            Projectile.friendly    = true;
            Projectile.hostile     = false;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.penetrate   = -1;
            Projectile.timeLeft    = 900;
            Projectile.DamageType  = DamageClass.Ranged;
        }

        // Only deal the light impact hit while still flying.
        public override bool? CanHitNPC(NPC target) => State == 0f ? null : false;

        // Impact is weak; the explosion is the real payoff.
        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
        {
            modifiers.FinalDamage *= 0.3f;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            stuckNPC            = target.whoAmI;
            stuckOffset         = Projectile.Center - target.Center;
            State               = 1f;
            Timer               = 0f;
            Projectile.velocity = Vector2.Zero;
            Projectile.tileCollide = false;
            Projectile.netUpdate   = true;
            if (Main.netMode != NetmodeID.Server)
                SoundEngine.PlaySound(new SoundStyle("DeterministicChaos/Assets/Sounds/TitanCharge"), Projectile.Center);
        }

        public override bool OnTileCollide(Vector2 oldVelocity)
        {
            if (State != 0f) return false;
            State               = 2f;
            Timer               = 0f;
            Projectile.velocity = Vector2.Zero;
            Projectile.tileCollide = false;
            Projectile.netUpdate   = true;
            if (Main.netMode != NetmodeID.Server)
                SoundEngine.PlaySound(new SoundStyle("DeterministicChaos/Assets/Sounds/TitanCharge"), Projectile.Center);
            return false;   // suppress the default kill-on-tile behaviour
        }

        public override void AI()
        {
            if (State == 0f)
                FlyingAI();
            else
                StuckAI();
        }

        private void FlyingAI()
        {
            Projectile.velocity.Y += 0.12f;
            Projectile.rotation    = Projectile.velocity.ToRotation() - MathHelper.PiOver2;

            Lighting.AddLight(Projectile.Center, 0.2f, 0.4f, 0.9f);

            if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(4))
            {
                Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
                    DustID.BlueTorch, 0f, 0f, 100, default, 0.8f);
                d.noGravity  = true;
                d.velocity  *= 0.3f;
            }
        }

        private void StuckAI()
        {
            // Track the struck NPC if we are stuck to one.
            if (State == 1f)
            {
                if (stuckNPC >= 0 && stuckNPC < Main.maxNPCs && Main.npc[stuckNPC].active)
                    Projectile.Center = Main.npc[stuckNPC].Center + stuckOffset;
                else
                    State = 2f;   // NPC died; remain in place
            }

            Timer++;

            if (Projectile.owner == Main.myPlayer)
            {
                int spawnInterval = System.Math.Max(1, SuctionDuration / HomingArrowCount);
                while (spawnedArrowCount < HomingArrowCount && Timer >= spawnedArrowCount * spawnInterval)
                {
                    SpawnInboundAmmoArrow();
                    spawnedArrowCount++;
                }
            }

            // Suction: pull nearby non-boss enemies toward the bolt every other tick.
            if ((int)Timer % 2 == 0)
            {
                for (int i = 0; i < Main.maxNPCs; i++)
                {
                    NPC npc = Main.npc[i];
                    if (!npc.active || npc.friendly || npc.boss) continue;
                    if (i == stuckNPC) continue;   // don't pull the NPC we're lodged in

                    float dist = Vector2.Distance(npc.Center, Projectile.Center);
                    if (dist > SuctionRadius || dist < 1f) continue;

                    Vector2 pull = (Projectile.Center - npc.Center).SafeNormalize(Vector2.Zero) * SuctionForce;
                    npc.velocity += pull;

                    if (npc.velocity.Length() > 18f)
                        npc.velocity = npc.velocity.SafeNormalize(Vector2.Zero) * 18f;
                }
            }

            // Suction VFX: spawn several inward-spiralling particles per tick for high visibility.
            if (Main.netMode == NetmodeID.Server)
            {
                // Pulsing light still applies on server for tile brightness
                float pulse = 0.7f + 0.3f * (float)System.Math.Sin(Timer * 0.2f);
                Lighting.AddLight(Projectile.Center, 0.55f * pulse, 0.55f * pulse, 1.2f * pulse);

                if (Timer >= SuctionDuration)
                {
                    hasExploded      = true;
                    Projectile.Kill();
                }
                return;
            }

            int suctionCount = Main.rand.Next(3, 6);
            for (int s = 0; s < suctionCount; s++)
            {
                float angle  = Main.rand.NextFloat(MathHelper.TwoPi);
                float radius = Main.rand.NextFloat(80f, SuctionRadius * 0.95f);
                Vector2 dPos = Projectile.Center + Vector2.UnitX.RotatedBy(angle) * radius;
                Vector2 dVel = (Projectile.Center - dPos).SafeNormalize(Vector2.Zero)
                               * Main.rand.NextFloat(6f, 14f);
                // Alternate between bright blue and electric yellow for a striking colour set.
                int dustType = (s % 2 == 0) ? DustID.BlueTorch : DustID.IceTorch;
                Color col    = (s % 2 == 0) ? new Color(80, 160, 255) : new Color(0, 240, 240);
                Dust d = Dust.NewDustPerfect(dPos, dustType, dVel, 80, col,
                    Main.rand.NextFloat(1.0f, 1.8f));
                d.noGravity = true;
                d.fadeIn    = 0.4f;
            }

            // Pulsing blue-yellow glow that alternates phase.
            float pulseClient = 0.7f + 0.3f * (float)System.Math.Sin(Timer * 0.2f);
            Lighting.AddLight(Projectile.Center, 0.55f * pulseClient, 0.55f * pulseClient, 1.2f * pulseClient);

            if (Timer >= SuctionDuration)
            {
                hasExploded      = true;
                Projectile.Kill();
            }
        }

        public override void OnKill(int timeLeft)
        {
            // Explosion visuals, skip on dedicated server.
            if (Main.netMode != NetmodeID.Server)
            {
                SoundEngine.PlaySound(SoundID.Item14, Projectile.Center);
                // Tight inner burst: blue and yellow dust in a condensed radius.
                for (int i = 0; i < 50; i++)
                {
                    int dustType = (i % 2 == 0) ? DustID.BlueTorch : DustID.IceTorch;
                    Color col    = (i % 2 == 0) ? new Color(80, 160, 255) : new Color(0, 240, 240);
                    Dust d = Dust.NewDustDirect(
                        Projectile.Center - new Vector2(20), 40, 40,
                        dustType, 0f, 0f, 60, col, Main.rand.NextFloat(1.8f, 3.2f));
                    d.velocity  = Main.rand.NextVector2Circular(9f, 9f);
                    d.noGravity = true;
                    d.fadeIn    = 0.6f;
                }
                // A few larger sparks fly a bit further out.
                for (int i = 0; i < 14; i++)
                {
                    int dustType = (i % 2 == 0) ? DustID.BlueTorch : DustID.IceTorch;
                    Color col    = (i % 2 == 0) ? new Color(100, 200, 255) : new Color(0, 255, 255);
                    Dust d = Dust.NewDustDirect(
                        Projectile.Center - new Vector2(12), 24, 24,
                        dustType, 0f, 0f, 40, col, Main.rand.NextFloat(2.5f, 4.5f));
                    d.velocity  = Main.rand.NextVector2Circular(14f, 14f);
                    d.noGravity = false;
                }
            }

            if (!hasExploded) return;

            // AoE damage + DPS meter registration.
            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                int explodeDamage = (int)(originalDamage * ExplosionScale);
                Player owner = Main.player[Projectile.owner];
                for (int i = 0; i < Main.maxNPCs; i++)
                {
                    NPC npc = Main.npc[i];
                    if (!npc.active || npc.friendly || npc.dontTakeDamage) continue;
                    if (Vector2.Distance(npc.Center, Projectile.Center) > ExplosionRadius) continue;

                    int dir = npc.Center.X > Projectile.Center.X ? 1 : -1;
                    int dealt = npc.StrikeNPC(npc.CalculateHitInfo(explodeDamage, dir, false,
                        Projectile.knockBack * 2f, DamageClass.Ranged));
                    if (Projectile.owner == Main.myPlayer)
                        owner.addDPS(dealt);
                }

                // Secondary ammo arrows are now called in during the stuck phase instead of on detonation.
            }
        }

        private void SpawnInboundAmmoArrow()
        {
            int arrowType = ArrowType > 0 ? ArrowType : ProjectileID.WoodenArrowFriendly;
            Vector2 spawnPos = FindReachableArrowSpawn();
            Vector2 velocity = (Projectile.Center - spawnPos).SafeNormalize(Vector2.UnitY) * HomingArrowSpeed;

            int id = Projectile.NewProjectile(
                Projectile.GetSource_FromThis(),
                spawnPos,
                velocity,
                arrowType,
                System.Math.Max(1, originalDamage / 2),
                Projectile.knockBack,
                Projectile.owner);

            if (id >= 0 && id < Main.maxProjectiles)
            {
                Projectile spawned = Main.projectile[id];
                spawned.friendly = true;
                spawned.hostile = false;
                spawned.DamageType = DamageClass.Ranged;
                spawned.rotation = velocity.ToRotation() + MathHelper.PiOver2;

                CascadeHomingArrowGlobal homing = spawned.GetGlobalProjectile<CascadeHomingArrowGlobal>();
                homing.active = true;
                homing.anchorProjectile = Projectile.whoAmI;
                homing.lastKnownTarget = Projectile.Center;
                spawned.netUpdate = true;
                Projectile.netUpdate = true;
            }
        }

        private Vector2 FindReachableArrowSpawn()
        {
            for (int i = 0; i < 24; i++)
            {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float distance = Main.rand.NextFloat(SpawnMinDistance, SpawnMaxDistance);
                Vector2 candidate = Projectile.Center + angle.ToRotationVector2() * distance;

                if (Collision.SolidCollision(candidate - new Vector2(6f), 12, 12))
                    continue;

                if (Collision.CanHitLine(candidate, 1, 1, Projectile.Center, 1, 1))
                    return candidate;
            }

            for (int i = 0; i < 12; i++)
            {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float distance = Main.rand.NextFloat(SpawnMinDistance, SpawnMaxDistance);
                Vector2 candidate = Projectile.Center + angle.ToRotationVector2() * distance;
                if (!Collision.SolidCollision(candidate - new Vector2(6f), 12, 12))
                    return candidate;
            }

            return Projectile.Center + Main.rand.NextVector2CircularEdge(1f, 1f) * SpawnMinDistance;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Vector2   origin  = texture.Size() * 0.5f;

            // Draw fading trail.
            for (int i = Projectile.oldPos.Length - 1; i >= 0; i--)
            {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;

                float   alpha    = 1f - i / (float)Projectile.oldPos.Length;
                Color   tCol     = new Color(60, 120, 255, 0) * alpha * 0.5f;
                Vector2 trailPos = Projectile.oldPos[i]
                                   + new Vector2(Projectile.width, Projectile.height) * 0.5f
                                   - Main.screenPosition;
                Main.spriteBatch.Draw(texture, trailPos, null, tCol,
                    Projectile.oldRot[i], origin, Projectile.scale * alpha,
                    SpriteEffects.None, 0f);
            }

            // Main sprite, drawn 1.5× scale to feel more powerful than a regular arrow.
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Main.spriteBatch.Draw(texture, drawPos, null, lightColor,
                Projectile.rotation, origin, Projectile.scale * 1.5f,
                SpriteEffects.None, 0f);

            // Additive glow layer.
            float glowPulse = State == 0f
                ? 1f
                : 0.7f + 0.3f * (float)System.Math.Sin(Timer * 0.2f);
            Color glowCol = new Color(100, 180, 255, 0) * 0.6f * glowPulse;
            Main.spriteBatch.Draw(texture, drawPos, null, glowCol,
                Projectile.rotation, origin, Projectile.scale * 1.9f,
                SpriteEffects.None, 0f);

            return false;
        }
    }

    public class CascadeHomingArrowGlobal : GlobalProjectile
    {
        public override bool InstancePerEntity => true;

        public bool active;
        public int anchorProjectile = -1;
        public Vector2 lastKnownTarget = Vector2.Zero;

        public override void AI(Projectile projectile)
        {
            if (!active || !projectile.active || projectile.hostile || !projectile.friendly)
                return;

            if (anchorProjectile >= 0 && anchorProjectile < Main.maxProjectiles)
            {
                Projectile anchor = Main.projectile[anchorProjectile];
                if (anchor.active && anchor.type == ModContent.ProjectileType<CascadeProjectile>())
                    lastKnownTarget = anchor.Center;
            }

            if (lastKnownTarget == Vector2.Zero)
                return;

            Vector2 toTarget = lastKnownTarget - projectile.Center;
            if (toTarget.LengthSquared() < 16f)
                return;

            float speed = System.Math.Max(projectile.velocity.Length(), 18f);
            Vector2 desiredVelocity = toTarget.SafeNormalize(Vector2.UnitY) * speed;
            projectile.velocity = Vector2.Lerp(projectile.velocity, desiredVelocity, 0.14f);
            projectile.rotation = projectile.velocity.ToRotation() - MathHelper.PiOver2;
        }
    }
}
