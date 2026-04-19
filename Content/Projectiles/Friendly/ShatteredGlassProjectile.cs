using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.Projectiles.Friendly
{
    public class ShatteredGlassProjectile : ModProjectile
    {
        public override string Texture => "DeterministicChaos/Content/Items/ShatteredGlass";

        public override void SetDefaults()
        {
            Projectile.width = 34;
            Projectile.height = 34;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 24;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override bool? CanDamage() => false;

        public override void AI()
        {
            Player player = Main.player[Projectile.owner];
            if (!player.active || player.dead)
            {
                Projectile.Kill();
                return;
            }

            Vector2 aim = Projectile.velocity.SafeNormalize(Vector2.UnitX * player.direction);
            player.ChangeDir(aim.X >= 0f ? 1 : -1);
            player.heldProj = Projectile.whoAmI;

            Projectile.spriteDirection = player.direction;
            Projectile.rotation = aim.ToRotation();

            // Anchor the holdout directly to the player's hand.
            Projectile.Center = player.MountedCenter + aim * 2f;
            player.itemRotation = Projectile.rotation;

            if (Projectile.localAI[0] == 0f)
            {
                Projectile.localAI[0] = 1f;
                if (Main.myPlayer == Projectile.owner)
                {
                    Projectile.NewProjectile(
                        Projectile.GetSource_FromThis(),
                        player.MountedCenter,
                        aim,
                        ModContent.ProjectileType<DefaultBeam>(),
                        Projectile.damage,
                        Projectile.knockBack,
                        Projectile.owner);
                }
            }

            Projectile.localAI[1]++;
            if (Projectile.localAI[1] >= 10f)
                Projectile.Kill();
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            // The handle is near the bottom-left of the sprite.
            Vector2 origin = new Vector2(4f, texture.Height - 4f);
            SpriteEffects effects = Projectile.spriteDirection == -1 ? SpriteEffects.FlipVertically : SpriteEffects.None;

            Main.EntitySpriteDraw(
                texture,
                drawPos,
                null,
                Projectile.GetAlpha(lightColor),
                Projectile.rotation,
                origin,
                Projectile.scale,
                effects,
                0);

            return false;
        }
    }

    public class DefaultBeam : ModProjectile
    {
        public override string Texture => "DeterministicChaos/Content/Projectiles/Friendly/DefaultBeam";

        private const int Lifetime = 12;
        private const float MaxBeamScale = 1.15f;
        private const float MaxBeamLength = 960f;
        private const float BeamTileCollisionWidth = 1f;
        private const float BeamHitboxCollisionWidth = 15f;
        private const int NumSamplePoints = 3;
        private const float BeamLengthChangeFactor = 0.75f;
        private const float OuterBeamOpacityMultiplier = 0.82f;
        private const float InnerBeamOpacityMultiplier = 0.2f;
        private const float MaxBeamBrightness = 0.75f;
        private const float BeamRenderTileOffset = 10.5f;
        private const float BeamLengthReductionFactor = 14.5f;

        private Vector2 beamVector = Vector2.Zero;

        public override void SetDefaults()
        {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.hide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
            Projectile.timeLeft = Lifetime;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI()
        {
            Player owner = Main.player[Projectile.owner];
            if (!owner.active || owner.dead)
            {
                Projectile.Kill();
                return;
            }

            if (Projectile.velocity != Vector2.Zero)
            {
                beamVector = Vector2.Normalize(Projectile.velocity);
                Projectile.rotation = Projectile.velocity.ToRotation();
                Projectile.velocity = Vector2.Zero;
            }

            if (beamVector == Vector2.Zero)
                beamVector = Vector2.UnitX * owner.direction;

            Projectile.Center = owner.MountedCenter + beamVector * 18f;

            float power = Projectile.timeLeft / (float)Lifetime;
            Projectile.scale = MaxBeamScale * power;

            if (Projectile.localAI[0] == 0f)
            {
                float[] laserScanResults = new float[NumSamplePoints];
                float scanWidth = Projectile.scale < 1f ? 1f : Projectile.scale;
                Collision.LaserScan(Projectile.Center, beamVector, BeamTileCollisionWidth * scanWidth, MaxBeamLength, laserScanResults);
                float avg = 0f;
                for (int i = 0; i < laserScanResults.Length; i++)
                    avg += laserScanResults[i];
                avg /= NumSamplePoints;
                Projectile.ai[0] = MathHelper.Lerp(Projectile.ai[0], avg, BeamLengthChangeFactor);
            }

            Projectile.Opacity = power;
            ProduceBeamDust(GetBeamColor());
            DelegateMethods.v3_1 = GetBeamColor().ToVector3() * power * MaxBeamBrightness;
            Utils.PlotTileLine(Projectile.Center, Projectile.Center + beamVector * Projectile.ai[0], Projectile.width * Projectile.scale, DelegateMethods.CastLight);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            if (projHitbox.Intersects(targetHitbox))
                return true;

            float collisionPoint = 0f;
            return Collision.CheckAABBvLineCollision(
                targetHitbox.TopLeft(),
                targetHitbox.Size(),
                Projectile.Center,
                Projectile.Center + beamVector * Projectile.ai[0],
                BeamHitboxCollisionWidth * Projectile.scale,
                ref collisionPoint);
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
        {
            modifiers.HitDirectionOverride = (Projectile.Center.X < target.Center.X).ToDirectionInt();
        }

        private Color GetBeamColor()
        {
            Color c = new Color(55, 120, 255, 64);
            return c;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (Projectile.owner != Main.myPlayer)
                return;

            List<int> nearbyShards = FindNearbyShards(target.Center, 300f);
            if (nearbyShards.Count >= 2)
                FireRicochetBursts(target, nearbyShards);

            SpawnShardsAround(target);

            SoundEngine.PlaySound(SoundID.Item27 with { Pitch = -0.1f, Volume = 0.7f }, target.Center);
        }

        private List<int> FindNearbyShards(Vector2 center, float radius)
        {
            int shardType = ModContent.ProjectileType<ShatteredGlassShard>();
            List<int> result = new();

            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile p = Main.projectile[i];
                if (!p.active || p.owner != Projectile.owner || p.type != shardType)
                    continue;

                if (Vector2.DistanceSquared(p.Center, center) <= radius * radius)
                    result.Add(i);
            }

            return result;
        }

        private void FireRicochetBursts(NPC target, List<int> shardIds)
        {
            int arcType = ModContent.ProjectileType<ShatteredGlassBeamArc>();
            int arcDamage = System.Math.Max(1, (int)(Projectile.damage * 0.45f));

            // Build a constellation path by sorting shards around the struck target.
            List<Projectile> shards = new();
            for (int i = 0; i < shardIds.Count; i++)
            {
                Projectile shard = Main.projectile[shardIds[i]];
                if (shard.active)
                    shards.Add(shard);
            }

            shards.Sort((a, b) =>
            {
                float angleA = (a.Center - target.Center).ToRotation();
                float angleB = (b.Center - target.Center).ToRotation();
                return angleA.CompareTo(angleB);
            });

            if (shards.Count > 15)
                shards = shards.GetRange(0, 15);

            Vector2 from = target.Center;
            int delay = 0;
            foreach (Projectile shard in shards)
            {
                Vector2 to = shard.Center;
                SpawnArc(from, to, arcType, arcDamage, delay, -(shard.whoAmI + 2), target.whoAmI);

                from = to;
                delay += 4;
            }

            // Close the loop by returning to the initially struck enemy only if it still exists later.
            if (shards.Count > 0)
                SpawnArc(from, target.Center, arcType, arcDamage, delay, target.whoAmI, -1);
        }

        private void SpawnArc(Vector2 start, Vector2 end, int type, int damage, int delay, int trackedTarget, int ignoredTarget)
        {
            Vector2 direction = (end - start).SafeNormalize(Vector2.UnitX);
            float length = Vector2.Distance(start, end);

            int id = Projectile.NewProjectile(
                Projectile.GetSource_FromThis(),
                start,
                direction,
                type,
                damage,
                0f,
                Projectile.owner,
                length,
                delay,
                trackedTarget);

            if (id >= 0 && id < Main.maxProjectiles)
            {
                Main.projectile[id].localAI[1] = ignoredTarget;
                Main.projectile[id].timeLeft = 12 + delay;
                Main.projectile[id].netUpdate = true;
            }
        }

        private void SpawnShardsAround(NPC target)
        {
            int shardType = ModContent.ProjectileType<ShatteredGlassShard>();
            int count = Main.rand.Next(3, 5);

            for (int i = 0; i < count; i++)
            {
                Vector2 dir = Main.rand.NextVector2CircularEdge(1f, 1f);
                float dist = Main.rand.NextFloat(26f, 56f);
                Vector2 spawnPos = target.Center + dir * dist;
                Vector2 drift = dir * Main.rand.NextFloat(1.2f, 2.6f);
                float variant = Main.rand.Next(4);
                float scale = Main.rand.NextFloat(0.85f, 1.15f);

                int id = Projectile.NewProjectile(
                    Projectile.GetSource_FromThis(),
                    spawnPos,
                    drift,
                    shardType,
                    0,
                    0f,
                    Projectile.owner,
                    variant,
                    scale);

                if (id >= 0 && id < Main.maxProjectiles)
                    Main.projectile[id].netUpdate = true;
            }
        }

        private void ProduceBeamDust(Color beamColor)
        {
            if (Main.netMode == NetmodeID.Server)
                return;

            Vector2 laserEndPos = Projectile.Center + beamVector * Projectile.ai[0];
            for (int i = 0; i < 2; i++)
            {
                float dustAngle = Projectile.rotation + (Main.rand.NextBool() ? 1f : -1f) * MathHelper.PiOver2;
                Vector2 dustVel = dustAngle.ToRotationVector2() * Main.rand.NextFloat(1f, 1.8f);
                Dust d = Dust.NewDustPerfect(laserEndPos, DustID.BlueCrystalShard, dustVel, 0, beamColor, 0.8f);
                d.noGravity = true;

                if (Projectile.scale > 1f)
                {
                    d.velocity *= Projectile.scale;
                    d.scale *= Projectile.scale;
                }
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            if (beamVector == Vector2.Zero || Projectile.velocity != Vector2.Zero)
                return false;

            Texture2D tex = TextureAssets.Projectile[Type].Value;
            float beamLength = Projectile.ai[0];
            Vector2 centerFloored = new Vector2((float)System.Math.Floor(Projectile.Center.X), (float)System.Math.Floor(Projectile.Center.Y)) + beamVector * Projectile.scale * BeamRenderTileOffset;
            Vector2 scaleVec = new Vector2(Projectile.scale);

            beamLength -= BeamLengthReductionFactor * Projectile.scale * Projectile.scale;

            DelegateMethods.f_1 = 1f;
            Vector2 beamStartPos = centerFloored - Main.screenPosition;
            Vector2 beamEndPos = beamStartPos + beamVector * beamLength;
            Utils.LaserLineFraming llf = new Utils.LaserLineFraming(DelegateMethods.RainbowLaserDraw);

            Color beamColor = GetBeamColor();
            DelegateMethods.c_1 = beamColor * OuterBeamOpacityMultiplier * Projectile.Opacity;
            Utils.DrawLaser(Main.spriteBatch, tex, beamStartPos, beamEndPos, scaleVec, llf);

            for (int i = 0; i < 5; i++)
            {
                beamColor = Color.Lerp(beamColor, Color.White, 0.4f);
                scaleVec *= 0.85f;
                DelegateMethods.c_1 = beamColor * InnerBeamOpacityMultiplier * Projectile.Opacity;
                Utils.DrawLaser(Main.spriteBatch, tex, beamStartPos, beamEndPos, scaleVec, llf);
            }
            return false;
        }
    }

    public class ShatteredGlassShard : ModProjectile
    {
        public override string Texture => "DeterministicChaos/Content/Projectiles/Friendly/Shard1";

        public override void SetDefaults()
        {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 120;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.alpha = 0;
        }

        public override void AI()
        {
            if (Projectile.localAI[0] == 0f)
            {
                Projectile.localAI[0] = 1f;
                Projectile.rotation = Main.rand.NextFloat(MathHelper.TwoPi);
                if (Projectile.ai[1] > 0f)
                    Projectile.scale = Projectile.ai[1];
            }

            Projectile.velocity *= 0.97f;
            Projectile.rotation += 0.08f + Projectile.velocity.X * 0.015f;
            Lighting.AddLight(Projectile.Center, 0.02f, 0.14f, 0.32f);

            if (Projectile.timeLeft < 20)
                Projectile.Opacity = Projectile.timeLeft / 20f;
            else if (Projectile.timeLeft > 110)
                Projectile.Opacity = (120 - Projectile.timeLeft) / 10f;
            else
                Projectile.Opacity = 1f;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            int variant = Utils.Clamp((int)Projectile.ai[0], 0, 3) + 1;
            Texture2D tex = ModContent.Request<Texture2D>($"DeterministicChaos/Content/Projectiles/Friendly/Shard{variant}").Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Color color = Projectile.GetAlpha(new Color(210, 240, 255));

            Main.EntitySpriteDraw(tex, drawPos, null, color, Projectile.rotation,
                tex.Size() * 0.5f, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }

    public class ShatteredGlassBeamArc : ModProjectile
    {
        private bool soundPlayed;

        public override string Texture => "DeterministicChaos/Content/Projectiles/Friendly/DefaultBeam";

        private Vector2 Start => Projectile.Center;
        private float BeamLength => Projectile.ai[0];
        private int Delay => (int)Projectile.ai[1];
        private int TrackedTarget => (int)Projectile.ai[2];
        private int IgnoredTarget => (int)Projectile.localAI[1];

        private bool TryGetTrackedShard(out Projectile shard)
        {
            if (TrackedTarget <= -2)
            {
                int shardIndex = -TrackedTarget - 2;
                if (shardIndex >= 0 && shardIndex < Main.maxProjectiles)
                {
                    shard = Main.projectile[shardIndex];
                    return shard.active;
                }
            }

            shard = null;
            return false;
        }

        private Vector2 End
        {
            get
            {
                if (TryGetTrackedShard(out Projectile shard))
                    return shard.Center;

                if (TrackedTarget >= 0 && TrackedTarget < Main.maxNPCs)
                {
                    NPC npc = Main.npc[TrackedTarget];
                    if (npc.active && !npc.friendly && !npc.dontTakeDamage)
                        return npc.Center;
                }

                return Start + Projectile.velocity.SafeNormalize(Vector2.UnitX) * BeamLength;
            }
        }

        public override void SetDefaults()
        {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 12;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.hide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
        }

        public override bool ShouldUpdatePosition() => false;

        public override bool? CanHitNPC(NPC target)
        {
            if (Projectile.localAI[0] < Delay)
                return false;

            if (target.whoAmI == IgnoredTarget && target.whoAmI != TrackedTarget)
                return false;

            return base.CanHitNPC(target);
        }

        public override void AI()
        {
            Projectile.localAI[0]++;
            if (Projectile.localAI[0] < Delay)
                return;

            if (TrackedTarget >= 0)
            {
                if (TrackedTarget >= Main.maxNPCs)
                {
                    Projectile.Kill();
                    return;
                }

                NPC npc = Main.npc[TrackedTarget];
                if (!npc.active || npc.friendly || npc.dontTakeDamage)
                {
                    Projectile.Kill();
                    return;
                }
            }
            else if (TrackedTarget <= -2 && !TryGetTrackedShard(out _))
            {
                Projectile.Kill();
                return;
            }

            if (!soundPlayed)
            {
                soundPlayed = true;
                if (TrackedTarget < 0 && Main.netMode != NetmodeID.Server)
                {
                    SoundEngine.PlaySound(new SoundStyle("DeterministicChaos/Assets/Sounds/GlassReflect")
                    {
                        Pitch = Main.rand.NextFloat(0f, .3f),
                        Volume = 0.1f
                    }, Start);
                }

                if (TryGetTrackedShard(out Projectile shard))
                {
                    Vector2 hitDirection = (End - Start).SafeNormalize(Vector2.UnitX);
                    shard.velocity += hitDirection * Main.rand.NextFloat(2.4f, 4.2f);
                    shard.netUpdate = true;
                }
            }

            Projectile.rotation = (End - Start).ToRotation();
            Projectile.scale = 0.75f * MathHelper.Clamp(Projectile.timeLeft / 10f, 0f, 1f);
            Projectile.Opacity = MathHelper.Clamp(Projectile.timeLeft / 10f, 0f, 1f);
            Lighting.AddLight((Start + End) * 0.5f, 0.05f, 0.22f, 0.55f);

            if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(2))
            {
                Vector2 pos = Vector2.Lerp(Start, End, Main.rand.NextFloat());
                Dust d = Dust.NewDustPerfect(pos, DustID.BlueCrystalShard, Vector2.Zero, 120, new Color(90, 200, 255), 0.9f);
                d.noGravity = true;
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            if (Projectile.localAI[0] < Delay)
                return false;

            float collisionPoint = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), Start, End, 10f * Projectile.scale, ref collisionPoint);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            if (Projectile.localAI[0] < Delay)
                return false;

            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 beamVec = End - Start;
            float beamLength = beamVec.Length();
            if (beamLength <= 4f)
                return false;

            Vector2 beamDir = beamVec / beamLength;
            Vector2 beamStartPos = Start - Main.screenPosition;
            Vector2 beamEndPos = beamStartPos + beamDir * beamLength;
            Vector2 scaleVec = new Vector2(Projectile.scale);
            Utils.LaserLineFraming llf = new Utils.LaserLineFraming(DelegateMethods.RainbowLaserDraw);

            Color beamColor = new Color(55, 120, 255, 64);
            DelegateMethods.f_1 = 1f;
            DelegateMethods.c_1 = beamColor * 0.8f * Projectile.Opacity;
            Utils.DrawLaser(Main.spriteBatch, tex, beamStartPos, beamEndPos, scaleVec, llf);

            for (int i = 0; i < 4; i++)
            {
                beamColor = Color.Lerp(beamColor, Color.White, 0.4f);
                scaleVec *= 0.85f;
                DelegateMethods.c_1 = beamColor * 0.22f * Projectile.Opacity;
                Utils.DrawLaser(Main.spriteBatch, tex, beamStartPos, beamEndPos, scaleVec, llf);
            }
            return false;
        }
    }
}
