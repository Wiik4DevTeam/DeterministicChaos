using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.Projectiles.Friendly
{
    public class LeylineProjectile : ModProjectile
    {
        private const float MaxRange = 560f;
        private const float MinRopeLength = 42f;
        private const float RopeShrinkPerTick = 0.12f;
        private const float MaxGrappleSpeed = 18f;
        private const float LaunchSpeed = 46f;
        private const float ReleaseDamageMaxMultiplier = 10f;
        private const float MaxReleaseChargeTime = 120f;
        private const int MinimumHoldTicks = 12;
        private const int Lifetime = 2;
        private const float LineCollisionWidth = 16f;

        private int holdTimer;
        private int attachedTime;
        private Vector2 launchTarget;

        private ref float RopeLength => ref Projectile.localAI[0];
        private ref float InitFlag => ref Projectile.localAI[1];
        private ref float GrappleState => ref Projectile.ai[0];
        private ref float AttachedNpc => ref Projectile.ai[1];

        public override string Texture => "DeterministicChaos/Content/Projectiles/Friendly/LeylineProjectile";

        public override void SetDefaults()
        {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.SummonMeleeSpeed;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Lifetime;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 12; // 0.2 seconds
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI()
        {
            Player player = Main.player[Projectile.owner];
            if (!player.active || player.dead)
            {
                Projectile.Kill();
                return;
            }

            Projectile.timeLeft = Lifetime;
            holdTimer++;

            if (InitFlag == 0f)
                InitializeGrapple(player);

            if (InitFlag < 2f)
            {
                UpdateLaunch(player);
                return;
            }

            if (!player.channel || player.noItems || player.CCed)
            {
                TryReleaseAttack(player);
                Projectile.Kill();
                return;
            }

            if (GrappleState == 2f)
            {
                int npcIndex = (int)AttachedNpc;
                if (npcIndex >= 0 && npcIndex < Main.maxNPCs && Main.npc[npcIndex].active)
                {
                    Projectile.Center = Main.npc[npcIndex].Center;
                    attachedTime++;
                }
                else
                {
                    GrappleState = 1f;
                    AttachedNpc = -1f;
                    attachedTime = 0;
                    launchTarget = Projectile.Center;
                    Projectile.netUpdate = true;
                }
            }
            else
            {
                attachedTime = 0;
            }

            Vector2 toAnchor = Projectile.Center - player.MountedCenter;
            float distance = toAnchor.Length();
            if (distance <= 8f)
            {
                Projectile.Kill();
                return;
            }

            Vector2 direction = toAnchor.SafeNormalize(Vector2.UnitY);

            // Once the player moves closer, that closer distance becomes the new maximum rope length.
            if (RopeLength <= 0f)
                RopeLength = distance;
            else
                RopeLength = System.Math.Min(RopeLength, distance);

            RopeLength = System.Math.Max(MinRopeLength, RopeLength - RopeShrinkPerTick);

            float speed = player.velocity.Length();
            float accelerationRamp = MathHelper.Clamp(0.12f + holdTimer * 0.008f, 0.12f, 0.75f);
            if (speed < 0.9f)
                accelerationRamp += MathHelper.Clamp(1.45f + holdTimer * 0.012f, 1.45f, 2.6f);
            else if (speed < 4f)
                accelerationRamp += 0.2f;

            player.velocity += direction * accelerationRamp;

            // Prevent the player from moving farther away than the current rope cap.
            float outwardVelocity = -Vector2.Dot(player.velocity, direction);
            if (outwardVelocity > 0f)
                player.velocity += direction * outwardVelocity;

            if (distance > RopeLength)
            {
                player.Center = Projectile.Center - direction * RopeLength;

                Vector2 tangential = player.velocity - direction * Vector2.Dot(player.velocity, direction);
                float inward = System.Math.Max(0f, Vector2.Dot(player.velocity, direction));
                player.velocity = tangential * 1.02f + direction * (inward + 0.12f);
            }
            else if (speed > 0.5f)
            {
                player.velocity *= 1.01f;
            }

            // Let the player fall through platforms while grappling.
            player.ignoreWater = true;
            player.velocity.Y += 0.01f;

            if (player.velocity.Length() > MaxGrappleSpeed)
                player.velocity = player.velocity.SafeNormalize(Vector2.Zero) * MaxGrappleSpeed;

            // End if the player touches solid ground.
            if (Collision.SolidCollision(new Vector2(player.position.X + 4f, player.position.Y + player.height), player.width - 8, 4))
            {
                Projectile.Kill();
                return;
            }

            player.heldProj = Projectile.whoAmI;
            player.itemTime = 2;
            player.itemAnimation = 2;
            player.ChangeDir(Projectile.Center.X >= player.Center.X ? 1 : -1);
            player.itemRotation = direction.ToRotation();
            Projectile.rotation = direction.ToRotation() - MathHelper.PiOver2;
            Projectile.spriteDirection = player.direction;

            Vector2 midpoint = (player.Center + Projectile.Center) * 0.5f;
            Lighting.AddLight(midpoint, 0.35f, 0.2f, 0.5f);
            Lighting.AddLight(Projectile.Center, 0.55f, 0.35f, 0.75f);
        }

        private void InitializeGrapple(Player player)
        {
            InitFlag = 1f;

            launchTarget = new Vector2(Projectile.ai[0], Projectile.ai[1]);
            Vector2 toAnchor = launchTarget - player.MountedCenter;
            if (toAnchor.LengthSquared() <= 0.001f)
                toAnchor = Vector2.UnitX * player.direction;

            if (toAnchor.Length() > MaxRange)
                launchTarget = player.MountedCenter + toAnchor.SafeNormalize(Vector2.UnitX) * MaxRange;

            GrappleState = 0f;
            AttachedNpc = -1f;
            attachedTime = 0;
            Projectile.Center = player.MountedCenter;
            Projectile.velocity = toAnchor.SafeNormalize(Vector2.UnitX) * LaunchSpeed;
            RopeLength = Vector2.Distance(player.MountedCenter, launchTarget);

            if (Main.netMode != NetmodeID.Server)
                SoundEngine.PlaySound(new SoundStyle("DeterministicChaos/Assets/Sounds/GrappleAttach") { Volume = 0.75f }, Projectile.Center);
            Projectile.netUpdate = true;
        }

        private void UpdateLaunch(Player player)
        {
            if (GrappleState == 2f)
            {
                int npcIndex = (int)AttachedNpc;
                if (npcIndex >= 0 && npcIndex < Main.maxNPCs && Main.npc[npcIndex].active)
                {
                    Projectile.Center = Main.npc[npcIndex].Center;
                }
                else
                {
                    GrappleState = 1f;
                    AttachedNpc = -1f;
                    launchTarget = Projectile.Center;
                    Projectile.netUpdate = true;
                }
            }
            else
            {
                Vector2 toTarget = launchTarget - Projectile.Center;
                float remaining = toTarget.Length();
                if (remaining > LaunchSpeed)
                    Projectile.Center += toTarget.SafeNormalize(Vector2.UnitX) * LaunchSpeed;
                else
                {
                    Projectile.Center = launchTarget;
                    GrappleState = 1f;
                }
            }

            Vector2 direction = (Projectile.Center - player.MountedCenter).SafeNormalize(Vector2.UnitY);
            player.heldProj = Projectile.whoAmI;
            player.itemTime = 2;
            player.itemAnimation = 2;
            player.ChangeDir(Projectile.Center.X >= player.Center.X ? 1 : -1);
            player.itemRotation = direction.ToRotation();
            Projectile.rotation = direction.ToRotation() - MathHelper.PiOver2;
            Projectile.spriteDirection = player.direction;

            Vector2 midpoint = (player.Center + Projectile.Center) * 0.5f;
            Lighting.AddLight(midpoint, 0.35f, 0.2f, 0.5f);
            Lighting.AddLight(Projectile.Center, 0.55f, 0.35f, 0.75f);

            if (holdTimer >= MinimumHoldTicks && (GrappleState == 1f || GrappleState == 2f))
            {
                InitFlag = 2f;
                RopeLength = Vector2.Distance(player.MountedCenter, Projectile.Center);
                Projectile.netUpdate = true;

                if (Main.netMode != NetmodeID.Server)
                {
                    for (int i = 0; i < 10; i++)
                    {
                        Vector2 dustVelocity = Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(1.5f, 4f);
                        Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.BlueTorch, dustVelocity, 40, new Color(180, 160, 255), 1.0f);
                        dust.noGravity = true;
                    }
                }
            }
        }

        private void TryReleaseAttack(Player player)
        {
            if (GrappleState != 2f)
                return;

            int npcIndex = (int)AttachedNpc;
            if (npcIndex < 0 || npcIndex >= Main.maxNPCs)
                return;

            NPC npc = Main.npc[npcIndex];
            if (!npc.active || npc.friendly || npc.dontTakeDamage)
                return;

            float chargeRatio = MathHelper.Clamp(attachedTime / MaxReleaseChargeTime, 0f, 1f);
            float damageMultiplier = MathHelper.Lerp(1f, ReleaseDamageMaxMultiplier, chargeRatio);
            int releaseDamage = System.Math.Max(1, (int)(Projectile.damage * damageMultiplier));

            player.MinionAttackTargetNPC = npc.whoAmI;
            npc.AddBuff(ModContent.BuffType<LeylineTagBuff>(), 240);

            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                int direction = npc.Center.X > player.Center.X ? 1 : -1;
                npc.immune[Projectile.owner] = 0;
                npc.StrikeNPC(npc.CalculateHitInfo(releaseDamage, direction, false, Projectile.knockBack * 2f, DamageClass.SummonMeleeSpeed));
                npc.netUpdate = true;
            }

            if (Main.netMode != NetmodeID.Server)
            {
                SoundEngine.PlaySound(new SoundStyle("DeterministicChaos/Assets/Sounds/SuccessfulGrapple") { Volume = 0.85f }, npc.Center);
                for (int i = 0; i < 14; i++)
                {
                    Vector2 dustVelocity = Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(2.5f, 6f);
                    Dust dust = Dust.NewDustPerfect(npc.Center, DustID.BlueTorch, dustVelocity, 60, new Color(180, 160, 255), 1.15f);
                    dust.noGravity = true;
                }
            }
        }

        public override void OnKill(int timeLeft)
        {
            if (Main.netMode == NetmodeID.Server)
                return;

            Vector2 center = Projectile.Center;

            // Bright flash core
            for (int i = 0; i < 6; i++)
            {
                Vector2 vel = Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(0.5f, 1.5f);
                Dust flash = Dust.NewDustPerfect(center, DustID.BlueTorch, vel, 0, new Color(220, 200, 255), 2.2f);
                flash.noGravity = true;
                flash.fadeIn = 1.4f;
            }

            // Fast expanding ring
            for (int i = 0; i < 18; i++)
            {
                Vector2 vel = Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(4f, 8f);
                Dust ring = Dust.NewDustPerfect(center, DustID.BlueTorch, vel, 40, new Color(180, 160, 255), 1.4f);
                ring.noGravity = true;
            }

            // Slower outer debris
            for (int i = 0; i < 12; i++)
            {
                Vector2 vel = Main.rand.NextVector2Circular(3f, 3f);
                Dust debris = Dust.NewDustPerfect(center + Main.rand.NextVector2Circular(8f, 8f), DustID.PurpleTorch, vel, 60, default, 1.1f);
                debris.noGravity = true;
                debris.fadeIn = 0.8f;
            }

            // Tiny sparkle scatter
            for (int i = 0; i < 10; i++)
            {
                Vector2 vel = Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(2f, 5.5f);
                Dust spark = Dust.NewDustPerfect(center, DustID.FireworksRGB, vel, 80, new Color(160, 140, 255), 0.7f);
                spark.noGravity = true;
            }

            // Screen-shake via gore trick: a few small gores for impact feel
            for (int i = 0; i < 3; i++)
            {
                Gore gore = Gore.NewGorePerfect(Projectile.GetSource_Death(), center, Main.rand.NextVector2CircularEdge(1f, 1f) * 2f, GoreID.Smoke1);
                gore.alpha = 200;
                gore.scale = 0.4f;
            }
        }

        public override bool? CanHitNPC(NPC target)
        {
            if (!target.CanBeChasedBy(Projectile))
                return false;

            return true;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            Player player = Main.player[Projectile.owner];
            if (!player.active || player.dead)
                return false;

            float collisionPoint = 0f;
            return Collision.CheckAABBvLineCollision(
                targetHitbox.TopLeft(),
                targetHitbox.Size(),
                player.MountedCenter,
                Projectile.Center,
                LineCollisionWidth,
                ref collisionPoint);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            Player owner = Main.player[Projectile.owner];
            owner.MinionAttackTargetNPC = target.whoAmI;
            target.AddBuff(ModContent.BuffType<LeylineTagBuff>(), 240);

            // Only allow a fresh entity latch before the grapple fully deploys.
            if (InitFlag < 2f && GrappleState != 2f)
            {
                GrappleState = 2f;
                AttachedNpc = target.whoAmI;
                Projectile.Center = target.Center;
                Projectile.netUpdate = true;
            }
        }

        public override Color? GetAlpha(Color lightColor)
        {
            return new Color(255, 255, 255, 230);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Player player = Main.player[Projectile.owner];
            if (!player.active)
                return false;

            Texture2D texture = TextureAssets.Projectile[Type].Value;
            int ropeSliceWidth = System.Math.Max(2, texture.Width / 5);
            int ropeSliceX = (texture.Width - ropeSliceWidth) / 2;
            Rectangle handleFrame = new Rectangle(0, 0, texture.Width, 22);
            Rectangle lineFrame = new Rectangle(ropeSliceX, 48, ropeSliceWidth, 2);
            Rectangle tipFrame = new Rectangle(0, 74, texture.Width, 18);

            Vector2 gripPos = player.MountedCenter - Main.screenPosition;
            Vector2 end = Projectile.Center - Main.screenPosition;
            Vector2 edge = end - gripPos;
            float length = edge.Length();
            float rotation = edge.ToRotation() - MathHelper.PiOver2;
            Vector2 unit = edge.SafeNormalize(Vector2.UnitY);

            Color glowColor = Projectile.GetAlpha(lightColor);
            Vector2 handleOrigin = new Vector2(handleFrame.Width * 0.5f, handleFrame.Height * 0.5f);
            Vector2 lineOrigin = new Vector2(lineFrame.Width * 0.5f, 0f);
            Vector2 tipOrigin = new Vector2(tipFrame.Width * 0.5f, tipFrame.Height * 0.5f);
            float tipScale = Projectile.scale * 2f;

            Main.EntitySpriteDraw(texture, gripPos, handleFrame, glowColor, rotation, handleOrigin, Projectile.scale, SpriteEffects.None, 0);

            float ropeBodyStart = handleFrame.Height * 0.5f * Projectile.scale;
            float tipHalfHeight = tipFrame.Height * 0.5f * tipScale;
            float ropeBodyLength = System.Math.Max(0f, length - ropeBodyStart - tipHalfHeight);
            if (ropeBodyLength > 0f)
            {
                Vector2 ropeStart = gripPos + unit * ropeBodyStart;
                Vector2 ropeEnd = end;

                float sagAmount = ropeBodyLength * 0.18f;
                Vector2 controlPoint = (ropeStart + ropeEnd) * 0.5f + new Vector2(0f, sagAmount);

                const int segments = 20;
                Vector2 prev = ropeStart;
                for (int i = 1; i <= segments; i++)
                {
                    float t = i / (float)segments;
                    float u = 1f - t;
                    Vector2 pos = u * u * ropeStart + 2f * u * t * controlPoint + t * t * ropeEnd;

                    Vector2 segEdge = pos - prev;
                    float segLength = segEdge.Length();
                    float segRotation = segEdge.ToRotation() - MathHelper.PiOver2;
                    Vector2 segScale = new Vector2(Projectile.scale, segLength / lineFrame.Height);

                    Main.EntitySpriteDraw(texture, prev, lineFrame, glowColor, segRotation, lineOrigin, segScale, SpriteEffects.None, 0);
                    prev = pos;
                }
            }

            Main.EntitySpriteDraw(texture, end, tipFrame, glowColor, 0f, tipOrigin, tipScale, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(texture, end, tipFrame, new Color(200, 170, 255, 140), 0f, tipOrigin, tipScale * 1.08f, SpriteEffects.None, 0);
            return false;
        }
    }

    public class LeylineTagBuff : ModBuff
    {
        public override string Texture => "Terraria/Images/Buff_160";

        public override void SetStaticDefaults()
        {
            Main.debuff[Type] = true;
            Main.buffNoSave[Type] = true;
        }
    }

    public class LeylineTagGlobalNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        public override void ModifyHitByProjectile(NPC npc, Projectile projectile, ref NPC.HitModifiers modifiers)
        {
            if (!npc.HasBuff(ModContent.BuffType<LeylineTagBuff>()))
                return;

            if (!projectile.minion && !ProjectileID.Sets.MinionShot[projectile.type])
                return;

            modifiers.FlatBonusDamage += 18f;
        }
    }
}
