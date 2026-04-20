using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using System;
using Terraria.Graphics.CameraModifiers;
using DeterministicChaos.Content.Buffs;
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
using DeterministicChaos.Content.NPCs;

namespace DeterministicChaos.Content.Projectiles.Friendly
{
    // Glaive projectile for ForthcomingWrath.
    // Three are spawned per swing in a cone, each with a small individual swing animation.
    //
    // ai[0] = cone offset angle (radians, applied to aim direction)
    // ai[1] = launch delay in ticks (counts down before activating)
    // ai[2] = swing direction (+1 or -1; 0 for center glaive)
    //
    // localAI[0] = stored aim angle (captured once on first active tick)
    // localAI[1] = local animation timer (0 when still in delay)
    public class ForthcomingWrathProjectile : ModProjectile
    {
        // Animation timings (ticks)
        private const int LaunchTicks = 12;
        private const int HoldTicks = 3;
        private const int RetractTicks = 10;
        private const int TotalTicks = LaunchTicks + HoldTicks + RetractTicks;

        // Reach
        private const float MaxReach = 240f;

        // Swing amount (radians), tiny arc that settles at rest angle
        private const float SwingAmount = 0.075f;

        private bool initialized = false;

        // Convenience accessors
        private float ConeOffset  => Projectile.ai[0];
        private ref float LaunchDelay => ref Projectile.ai[1];
        private float SwingDir    => Projectile.ai[2];

        // ai[2] = 999 flags the charged Forthcoming Strike variant
        private bool IsCharged    => Projectile.ai[2] >= 100f;

        // Reach scales up for the charged version
        private float EffectiveMaxReach => IsCharged ? MaxReach * 1.5f : MaxReach;
        private ref float AimAngle => ref Projectile.localAI[0];
        private ref float AnimTimer => ref Projectile.localAI[1];

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 600;
            ProjectileID.Sets.TrailCacheLength[Type] = 14;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults()
        {
            Projectile.width = 96;
            Projectile.height = 96;
            Projectile.scale = 2f;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.ownerHitCheck = true;
            Projectile.alpha = 255; // Start invisible; drawn manually
            Projectile.DamageType = DamageClass.Melee;
            Projectile.timeLeft = 300;
        }

        public override void AI()
        {
            Player owner = Main.player[Projectile.owner];

            // While delay is counting down, sit at owner center invisibly
            if (LaunchDelay > 0)
            {
                LaunchDelay--;
                Projectile.Center = owner.Center;
                Projectile.friendly = false;
                // Pre-set rotation so oldRot cache doesn't store zeroes during delay
                Projectile.rotation = (owner.AngleTo(Main.MouseWorld) + ConeOffset) + MathHelper.PiOver4;
                return;
            }

            // First active tick: configure charged-specific overrides, then capture aim angle
            if (!initialized)
            {
                initialized = true;

                if (IsCharged)
                    Projectile.scale = 3f;

                AimAngle = owner.AngleTo(Main.MouseWorld) + ConeOffset;
                AnimTimer = 0f;
            }

            AnimTimer++;

            // Kill once animation is done
            if (AnimTimer >= TotalTicks)
            {
                Projectile.Kill();
                return;
            }

            // Disable friendly on retract phase
            Projectile.friendly = AnimTimer < LaunchTicks + HoldTicks;

            // Compute current reach
            float reach;
            if (AnimTimer <= LaunchTicks)
            {
                // Ease-out extend
                float t = AnimTimer / (float)LaunchTicks;
                reach = EffectiveMaxReach * (1f - (1f - t) * (1f - t));
            }
            else if (AnimTimer <= LaunchTicks + HoldTicks)
            {
                reach = EffectiveMaxReach;
            }
            else
            {
                // Ease-in retract
                float t = (AnimTimer - LaunchTicks - HoldTicks) / (float)RetractTicks;
                reach = EffectiveMaxReach * (1f - t * t);
            }

            // Tiny swing, charged glaive has no swing deviation
            float swingProgress = Math.Min(AnimTimer / (float)LaunchTicks, 1f);
            float swingOffset = IsCharged ? 0f : SwingAmount * SwingDir * (1f - swingProgress);
            float currentAngle = AimAngle + swingOffset;

            Vector2 aimDir = currentAngle.ToRotationVector2();
            Projectile.Center = owner.Center + aimDir * reach;
            Projectile.rotation = currentAngle + MathHelper.PiOver4;

            // Keep owner arm aimed
            owner.heldProj = Projectile.whoAmI;
            owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, currentAngle - MathHelper.PiOver2);

            if (IsCharged)
            {
                // Launch burst, screen shake, and sound on very first active tick
                if (AnimTimer == 1f && Main.netMode != NetmodeID.Server)
                {
                    SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.45f, Volume = 1.1f }, Projectile.Center);
                    Main.instance.CameraModifiers.Add(
                        new PunchCameraModifier(owner.Center, aimDir, 14f, 8f, 16, 2000f, "ForthcomingStrike"));

                    // Outer radial burst
                    for (int i = 0; i < 28; i++)
                    {
                        float angle = MathHelper.TwoPi * i / 28f;
                        Dust d = Dust.NewDustDirect(owner.Center - new Vector2(4f), 8, 8, DustID.BlueTorch,
                            Scale: Main.rand.NextFloat(2.5f, 4.5f));
                        d.noGravity = true;
                        d.velocity = angle.ToRotationVector2() * Main.rand.NextFloat(8f, 16f);
                    }
                    // Bright white inner core burst
                    for (int i = 0; i < 14; i++)
                    {
                        Dust d = Dust.NewDustDirect(owner.Center - new Vector2(4f), 8, 8, DustID.WhiteTorch,
                            Scale: Main.rand.NextFloat(1.5f, 3f));
                        d.noGravity = true;
                        d.velocity = Main.rand.NextVector2Circular(12f, 12f);
                    }
                }

                // Strong light along the beam and at the tip
                Lighting.AddLight(Projectile.Center, 0.5f, 1.6f, 2f);
                Lighting.AddLight(owner.Center + aimDir * reach * 0.5f, 0.3f, 1f, 1.2f);

                // Dense particle trail the full length of the beam during extend/hold
                if (AnimTimer <= LaunchTicks + HoldTicks && Main.netMode != NetmodeID.Server)
                {
                    const int Steps = 6;
                    for (int s = 0; s < Steps; s++)
                    {
                        float frac = (s + 1f) / (Steps + 1f);
                        Vector2 beamPos = owner.Center + aimDir * (reach * frac);
                        if (Main.rand.NextBool(2))
                        {
                            Dust d = Dust.NewDustDirect(beamPos - new Vector2(8f), 16, 16, DustID.BlueTorch,
                                Scale: Main.rand.NextFloat(1f, 2.5f));
                            d.noGravity = true;
                            d.velocity = Main.rand.NextVector2Circular(1.5f, 1.5f);
                        }
                    }
                    // Tip flare
                    if (Main.rand.NextBool(2))
                    {
                        Dust tip = Dust.NewDustDirect(Projectile.Center - new Vector2(14f), 28, 28, DustID.BlueTorch,
                            Scale: Main.rand.NextFloat(3f, 5f));
                        tip.noGravity = true;
                        tip.velocity = Main.rand.NextVector2Circular(3f, 3f);
                    }
                }
            }
            else
            {
                Lighting.AddLight(Projectile.Center, 0.28f, 0.93f, 1f);
                if (AnimTimer <= LaunchTicks + HoldTicks && Main.netMode != NetmodeID.Server && Main.rand.NextBool(2))
                {
                    Dust d = Dust.NewDustDirect(Projectile.Center - new Vector2(12f), 24, 24, DustID.BlueTorch,
                        Scale: Main.rand.NextFloat(1.2f, 2.2f));
                    d.noGravity = true;
                    d.velocity = Main.rand.NextVector2Circular(2.5f, 2.5f);
                }
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            // Blue torch dust burst on hit
            for (int i = 0; i < 6; i++)
            {
                Dust d = Dust.NewDustDirect(target.Center, 1, 1, DustID.BlueTorch, Scale: Main.rand.NextFloat(0.8f, 1.5f));
                d.noGravity = true;
                d.velocity = Main.rand.NextVector2Circular(4f, 4f);
            }

            if (IsCharged)
            {
                // Charged strike: inflict Titansbane for 10 seconds.
                // forceReapply ensures the damage burst fires even if the buff was already active.
                target.AddBuff(ModContent.BuffType<TitansBane>(), 10 * 60);
                target.GetGlobalNPC<TitansBaneGlobalNPC>().forceReapply = true;

                // Extra burst of larger particles on charged hit
                for (int i = 0; i < 14; i++)
                {
                    Dust d = Dust.NewDustDirect(target.Center, 1, 1, DustID.BlueTorch,
                        Scale: Main.rand.NextFloat(1.5f, 3f));
                    d.noGravity = true;
                    d.velocity = Main.rand.NextVector2Circular(8f, 8f);
                }
            }
            else
            {
                // Normal hit: increment charge counter
                Main.player[Projectile.owner].GetModPlayer<ForthcomingWrathPlayer>().RegisterHit();
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            if (!initialized || AnimTimer <= 0)
                return false;

            Player owner = Main.player[Projectile.owner];
            SpriteBatch sb = Main.spriteBatch;
            Texture2D tex = ModContent.Request<Texture2D>(Texture).Value;
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Rectangle pixelSrc = new Rectangle(0, 0, 1, 1);

            float alpha = 1f;
            if (AnimTimer > LaunchTicks + HoldTicks)
            {
                float t = (AnimTimer - LaunchTicks - HoldTicks) / (float)RetractTicks;
                alpha = 1f - t;
            }

            Vector2 mainPos = Projectile.Center - Main.screenPosition;

            if (IsCharged)
            {
                // All glow effects use Color(r,g,b,0)*opacity which is additive in Terraria's
                // default premultiplied AlphaBlend (rgb contributes directly, alpha=0 → no dest attenuation).

                // Energy beam: three layered quads from player center to glaive tip
                Vector2 ownerScreen = owner.Center - Main.screenPosition;
                Vector2 beamVec = mainPos - ownerScreen;
                float beamLength = beamVec.Length();
                if (beamLength > 2f)
                {
                    float beamRot = beamVec.ToRotation();
                    Vector2 beamOrigin = new Vector2(0f, 0.5f);

                    // Wide outer glow
                    sb.Draw(pixel, ownerScreen, pixelSrc,
                        new Color(0.1f, 0.55f, 1f, 0f) * 0.35f * alpha,
                        beamRot, beamOrigin, new Vector2(beamLength, 22f), SpriteEffects.None, 0f);
                    // Mid beam
                    sb.Draw(pixel, ownerScreen, pixelSrc,
                        new Color(0.25f, 0.92f, 1f, 0f) * 0.55f * alpha,
                        beamRot, beamOrigin, new Vector2(beamLength, 9f), SpriteEffects.None, 0f);
                    // Bright inner core
                    sb.Draw(pixel, ownerScreen, pixelSrc,
                        new Color(0.85f, 1f, 1f, 0f) * 0.75f * alpha,
                        beamRot, beamOrigin, new Vector2(beamLength, 3f), SpriteEffects.None, 0f);
                }

                // Expanding ring echoes during launch phase
                if (AnimTimer <= LaunchTicks)
                {
                    float launchT = AnimTimer / (float)LaunchTicks;
                    for (int ring = 0; ring < 3; ring++)
                    {
                        float ringT = (launchT + ring / 3f) % 1f;
                        float ringScale = Projectile.scale * (0.9f + ringT * 3.8f);
                        float ringAlpha = (1f - ringT) * 0.32f * alpha;
                        sb.Draw(tex, mainPos, null,
                            new Color(0.28f, 0.93f, 1f, 0f) * ringAlpha,
                            Projectile.rotation, tex.Size() * 0.5f, ringScale, SpriteEffects.None, 0f);
                    }
                }

                // Big additive trail along old positions
                for (int i = Projectile.oldPos.Length - 1; i >= 1; i--)
                {
                    if (Projectile.oldPos[i] == Vector2.Zero) continue;
                    // Skip positions that were captured during the delay (at owner center)
                    Vector2 oldCenter = Projectile.oldPos[i] + new Vector2(Projectile.width, Projectile.height) * 0.5f;
                    if (Vector2.DistanceSquared(oldCenter, owner.Center) < 100f) continue;
                    float frac = 1f - (float)i / Projectile.oldPos.Length;
                    Color afterColor = new Color(0.3f, 0.95f, 1f, 0f) * frac * 0.85f * alpha;
                    Vector2 afterPos = oldCenter - Main.screenPosition;
                    sb.Draw(tex, afterPos, null, afterColor, Projectile.oldRot[i],
                        tex.Size() * 0.5f, Projectile.scale * (0.9f + frac * 0.25f), SpriteEffects.None, 0f);
                }

                // Ghost glaive fan: random offsets within ±45°, centre-biased.
                // Use a seeded RNG (whoAmI + index) so the layout is stable frame-to-frame.
                float currentReach = Vector2.Distance(Projectile.Center, owner.Center);
                const int GhostCount = 16;
                const float FanHalfAngle = MathHelper.PiOver4;
                for (int g = 0; g < GhostCount; g++)
                {
                    // Box-Muller to get a centre-biased (normal-ish) value in [-1,1].
                    // Two deterministic pseudo-random values seeded by whoAmI + g.
                    uint seed1 = (uint)(Projectile.whoAmI * 1664525 + g * 22695477 + 1013904223);
                    uint seed2 = (uint)(seed1 * 1664525 + 1013904223);
                    float u1 = (seed1 % 10000 + 1) / 10001f; // (0,1)
                    float u2 = (seed2 % 10000 + 1) / 10001f;
                    float normal = (float)Math.Sqrt(-2.0 * Math.Log(u1)) * (float)Math.Cos(2.0 * Math.PI * u2);
                    // Clamp to ±2σ then remap to [-1,1]
                    float clamped = Math.Max(-2f, Math.Min(2f, normal)) * 0.5f;

                    float offset = clamped * FanHalfAngle;
                    float edgeFactor = 1f - Math.Abs(offset) / FanHalfAngle;
                    float ghostOpacity = MathHelper.Lerp(0.10f, 0.45f, edgeFactor);
                    float ghostScaleMul = MathHelper.Lerp(0.55f, 0.90f, edgeFactor);

                    float ghostAngle = AimAngle + offset;
                    Vector2 ghostCenter = owner.Center + ghostAngle.ToRotationVector2() * currentReach;
                    Vector2 ghostScreenPos = ghostCenter - Main.screenPosition;
                    float ghostRot = ghostAngle + MathHelper.PiOver4;
                    sb.Draw(tex, ghostScreenPos, null,
                        new Color(0.35f, 0.95f, 1f, 0f) * ghostOpacity * alpha,
                        ghostRot, tex.Size() * 0.5f, Projectile.scale * ghostScaleMul,
                        SpriteEffects.None, 0f);
                }

                // Pulsing glow layer over main sprite
                float pulse = 0.45f + 0.2f * (float)Math.Sin(AnimTimer * 0.5f);
                sb.Draw(tex, mainPos, null,
                    new Color(0.28f, 0.93f, 1f, 0f) * pulse * alpha,
                    Projectile.rotation, tex.Size() * 0.5f, Projectile.scale * 1.22f, SpriteEffects.None, 0f);

                // Bloom dot at the tip
                float bloomSize = 40f * alpha;
                sb.Draw(pixel, mainPos, pixelSrc,
                    new Color(0.65f, 1f, 1f, 0f) * 0.75f * alpha,
                    0f, new Vector2(0.5f), new Vector2(bloomSize, bloomSize), SpriteEffects.None, 0f);
                // Bright pin-point core
                sb.Draw(pixel, mainPos, pixelSrc,
                    new Color(1f, 1f, 1f, 0f) * 0.9f * alpha,
                    0f, new Vector2(0.5f), new Vector2(10f * alpha, 10f * alpha), SpriteEffects.None, 0f);

                // Solid main sprite on top of all the glow
                sb.Draw(tex, mainPos, null, Color.White * alpha,
                    Projectile.rotation, tex.Size() * 0.5f, Projectile.scale, SpriteEffects.None, 0f);

                return false;
            }

            // ── Normal glaive ───────────────────────────────────────────────────────────
            Color trailTint = new Color(100, 160, 255, 0);
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--)
            {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float trailAlpha = alpha * (1f - (float)i / Projectile.oldPos.Length) * 0.35f;
                Vector2 drawPos = Projectile.oldPos[i] + new Vector2(Projectile.width, Projectile.height) * 0.5f - Main.screenPosition;
                sb.Draw(tex, drawPos, null, trailTint * trailAlpha, Projectile.oldRot[i],
                    tex.Size() * 0.5f, Projectile.scale, SpriteEffects.None, 0f);
            }

            sb.Draw(tex, mainPos, null, Color.White * alpha,
                Projectile.rotation, tex.Size() * 0.5f, Projectile.scale, SpriteEffects.None, 0f);

            return false;
        }
    }
}
