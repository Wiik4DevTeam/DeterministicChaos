using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
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
using DeterministicChaos.Content.Items.Prefixes;
using DeterministicChaos.Content.Items.Imbued;

namespace DeterministicChaos.Content.Projectiles.Friendly
{
    // Kindness soul trait effect for Dark Shard:
    // Behaves identically to RogueSeekingKnife but orbits an ally, then dashes through and heals them.
    public class DarkShardHealingKnife : ModProjectile
    {
        public override string Texture => "DeterministicChaos/Content/Projectiles/Enemy/SeekingKnife";

        private const int FollowTime = 45;
        private const int StopTime = 10;
        private const int DashSpeed = 80;
        private const int TotalLifetime = FollowTime + StopTime + 45;
        private const float OrbitDistance = 200f;
        private const int GrowthTime = 12;

        // ai[0] = target player index
        // ai[1] = orbit angle

        private Vector2 lockedPosition;
        private bool hasStopped = false;
        private bool hasDashed = false;
        private bool prevHasDashed = false;
        private float orbitAngle;
        private bool isVisible = true;
        private bool hasHealed = false;

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults()
        {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalLifetime;
            Projectile.scale = 1.4f;
        }

        public override void SendExtraAI(System.IO.BinaryWriter writer)
        {
            writer.WriteVector2(lockedPosition);
            writer.Write(hasStopped);
            writer.Write(hasDashed);
            writer.Write(orbitAngle);
            writer.Write(Projectile.rotation);
            writer.Write(isVisible);
            writer.Write(hasHealed);
        }

        public override void ReceiveExtraAI(System.IO.BinaryReader reader)
        {
            lockedPosition = reader.ReadVector2();
            hasStopped = reader.ReadBoolean();
            hasDashed = reader.ReadBoolean();
            orbitAngle = reader.ReadSingle();
            Projectile.rotation = reader.ReadSingle();
            isVisible = reader.ReadBoolean();
            hasHealed = reader.ReadBoolean();
        }

        public override void OnSpawn(Terraria.DataStructures.IEntitySource source)
        {
            int targetIndex = (int)Projectile.ai[0];
            orbitAngle = Projectile.ai[1];

            if (targetIndex >= 0 && targetIndex < Main.maxPlayers)
            {
                Player target = Main.player[targetIndex];
                if (target.active && !target.dead)
                {
                    Vector2 orbitPos = target.Center + new Vector2(OrbitDistance, 0f).RotatedBy(orbitAngle);
                    Projectile.Center = orbitPos;
                    Projectile.rotation = (target.Center - Projectile.Center).ToRotation();
                }
            }

            if (Main.netMode != NetmodeID.Server)
            {
                SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.5f, Pitch = 0.8f }, Projectile.Center);
            }
        }

        public override void AI()
        {
            if (hasDashed && !prevHasDashed && Main.netMode != NetmodeID.Server)
            {
                SoundEngine.PlaySound(new SoundStyle("DeterministicChaos/Assets/Sounds/KnifeLaunch")
                {
                    Volume = 0.5f,
                    Pitch = 0.3f
                }, Projectile.Center);
            }
            prevHasDashed = hasDashed;

            int elapsed = TotalLifetime - Projectile.timeLeft;
            int targetIndex = (int)Projectile.ai[0];

            // Phase 1: Follow ally at fixed orbit position
            if (elapsed < FollowTime)
            {
                if (targetIndex >= 0 && targetIndex < Main.maxPlayers)
                {
                    Player target = Main.player[targetIndex];
                    if (target.active && !target.dead)
                    {
                        Vector2 targetPos = target.Center + new Vector2(OrbitDistance, 0f).RotatedBy(orbitAngle);

                        Vector2 toTarget = targetPos - Projectile.Center;
                        float speed = 18f;

                        if (toTarget.Length() > speed)
                            Projectile.velocity = toTarget.SafeNormalize(Vector2.Zero) * speed;
                        else
                        {
                            Projectile.Center = targetPos;
                            Projectile.velocity = Vector2.Zero;
                        }

                        Projectile.rotation = (target.Center - Projectile.Center).ToRotation();
                    }
                    else
                    {
                        FindNewAlly();
                    }
                }

                // Green light during orbit
                Lighting.AddLight(Projectile.Center, 0.2f, 0.6f, 0.2f);

                // Green dust trail
                if (Main.rand.NextBool(3))
                {
                    Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
                        DustID.GreenTorch, 0f, 0f, 100, default, 0.9f);
                    dust.noGravity = true;
                    dust.velocity *= 0.3f;
                }
            }
            // Phase 2: Stop and turn bright green
            else if (elapsed < FollowTime + StopTime)
            {
                if (!hasStopped)
                {
                    hasStopped = true;
                    lockedPosition = Projectile.Center;
                    Projectile.velocity = Vector2.Zero;
                    Projectile.netUpdate = true;
                }

                Projectile.Center = lockedPosition;
                Lighting.AddLight(Projectile.Center, 0.3f, 1.0f, 0.3f);
            }
            // Phase 3: Dash towards ally and heal
            else
            {
                if (!hasDashed)
                {
                    hasDashed = true;

                    Vector2 dashDir = new Vector2(1f, 0f).RotatedBy(Projectile.rotation);
                    Projectile.velocity = dashDir * DashSpeed;
                    Projectile.netUpdate = true;
                }

                // Check for contact with target ally during dash
                if (!hasHealed && targetIndex >= 0 && targetIndex < Main.maxPlayers)
                {
                    Player target = Main.player[targetIndex];
                    if (target.active && !target.dead)
                    {
                        float dist = Vector2.Distance(Projectile.Center, target.Center);
                        if (dist < 60f)
                        {
                            hasHealed = true;
                            RoaringGunPlayer.NotifyAllyHealed(Projectile.owner);

                            Player owner = Main.player[Projectile.owner];
                            int baseHeal = System.Math.Max(1, (int)(49 * 0.05f));
                            bool hasEmblem = owner.GetModPlayer<ImbuedEmblemPlayer>().hasKindnessEmblem;
                            int healAmount = hasEmblem ? (int)(baseHeal * 1.25f) : baseHeal;
                            if (healAmount < baseHeal) healAmount = baseHeal;
                            healAmount = owner.GetModPlayer<PrefixEffectPlayer>().ScaleHeal(healAmount);

                            target.statLife = System.Math.Min(target.statLife + healAmount, target.statLifeMax2);
                            target.HealEffect(healAmount);

                            // Green burst VFX
                            for (int i = 0; i < 12; i++)
                            {
                                Dust dust = Dust.NewDustDirect(target.position, target.width, target.height,
                                    DustID.GreenTorch, 0f, 0f, 100, default, 1.2f);
                                dust.velocity = Main.rand.NextVector2Circular(3f, 3f);
                                dust.noGravity = true;
                            }
                        }
                    }
                }

                Lighting.AddLight(Projectile.Center, 0.3f, 1.0f, 0.3f);
            }
        }

        private void FindNewAlly()
        {
            float closestDist = 600f;
            int closestPlayer = -1;

            for (int i = 0; i < Main.maxPlayers; i++)
            {
                Player p = Main.player[i];
                if (p.active && !p.dead)
                {
                    float dist = Vector2.Distance(Projectile.Center, p.Center);
                    if (dist < closestDist)
                    {
                        closestDist = dist;
                        closestPlayer = i;
                    }
                }
            }

            if (closestPlayer >= 0)
            {
                Projectile.ai[0] = closestPlayer;
                Projectile.netUpdate = true;
            }
            else
            {
                Projectile.Kill();
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            if (!isVisible)
                return false;

            Texture2D tex = TextureAssets.Projectile[ModContent.ProjectileType<Enemy.SeekingKnife>()].Value;
            if (tex == null)
                return false;

            int elapsed = TotalLifetime - Projectile.timeLeft;

            // Color based on phase — green during orbit, bright green during stop/dash
            Color drawColor;
            if (elapsed >= FollowTime)
                drawColor = new Color(100, 255, 100);
            else
                drawColor = new Color(80, 230, 80) * 0.9f;

            // Growth animation
            float drawScale = Projectile.scale;
            if (elapsed < GrowthTime)
            {
                float growthProgress = elapsed / (float)GrowthTime;
                float verticalScale = MathHelper.Lerp(0.1f, 1f, growthProgress);
                drawScale = Projectile.scale * verticalScale;
            }

            Vector2 pos = Projectile.Center - Main.screenPosition;
            Vector2 origin = tex.Size() * 0.5f;

            // Draw trail during dash
            if (hasDashed)
            {
                for (int i = 0; i < Projectile.oldPos.Length; i++)
                {
                    if (Projectile.oldPos[i] == Vector2.Zero)
                        continue;

                    float trailAlpha = 1f - (i / (float)Projectile.oldPos.Length);
                    Vector2 trailPos = Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition;

                    Main.EntitySpriteDraw(tex, trailPos, null, drawColor * trailAlpha * 0.5f,
                        Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);
                }
            }

            // Draw main sprite
            Main.EntitySpriteDraw(tex, pos, null, drawColor, Projectile.rotation, origin,
                drawScale, SpriteEffects.None, 0);

            return false;
        }
    }
}
