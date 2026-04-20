using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using DeterministicChaos.Content.Items.Prefixes;
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
using DeterministicChaos.Content.Items.Imbued;

namespace DeterministicChaos.Content.Projectiles.Friendly
{
    public enum ImbuedArrowVariant
    {
        Determination,
        Integrity,
        Patience,
        Perseverance,
        Kindness,
        Justice,
        Bravery
    }

    public abstract class ImbuedArrowProjectile : ModProjectile
    {
        protected abstract ImbuedArrowVariant Variant { get; }

        private const float BaseDashSpeed = 32f;
        private const int BasePauseTime = 30;

        private Vector2 storedVelocity;
        private bool hasDashed = false;

        // Perseverance state
        private bool hasPierced = false;
        private int reaimTimer = 0;
        private int storedTargetNPC = -1;
        private bool isReaiming = false;

        // Kindness state
        private HashSet<int> healedPlayers = new HashSet<int>();

        public override string Texture => "DeterministicChaos/Content/Projectiles/Friendly/RoaringArrowProjectile";

        protected Color GetTraitColor()
        {
            return Variant switch
            {
                ImbuedArrowVariant.Determination => new Color(255, 60, 60),
                ImbuedArrowVariant.Integrity => new Color(0, 0, 255),
                ImbuedArrowVariant.Patience => new Color(80, 255, 255),
                ImbuedArrowVariant.Perseverance => new Color(255, 80, 255),
                ImbuedArrowVariant.Kindness => new Color(80, 230, 80),
                ImbuedArrowVariant.Justice => new Color(255, 255, 80),
                ImbuedArrowVariant.Bravery => new Color(255, 190, 60),
                _ => Color.White
            };
        }

        private int GetPauseTime()
        {
            switch (Variant)
            {
                case ImbuedArrowVariant.Patience:
                    return 50;
                case ImbuedArrowVariant.Bravery:
                    Player owner = Main.player[Projectile.owner];
                    float healthPercent = (float)owner.statLife / owner.statLifeMax2;
                    return (int)(10 + 20 * healthPercent);
                default:
                    return BasePauseTime;
            }
        }

        private float GetDashSpeed()
        {
            return Variant == ImbuedArrowVariant.Integrity ? 20f : BaseDashSpeed;
        }

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailCacheLength[Type] = 10;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults()
        {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = false;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 300;
            Projectile.arrow = true;
            Projectile.aiStyle = -1;
            Projectile.DamageType = DamageClass.Ranged;
        }

        public override void SendExtraAI(BinaryWriter writer)
        {
            writer.WriteVector2(storedVelocity);
            writer.Write(hasDashed);
            writer.Write(hasPierced);
            writer.Write(reaimTimer);
            writer.Write(storedTargetNPC);
            writer.Write(isReaiming);
        }

        public override void ReceiveExtraAI(BinaryReader reader)
        {
            storedVelocity = reader.ReadVector2();
            hasDashed = reader.ReadBoolean();
            hasPierced = reader.ReadBoolean();
            reaimTimer = reader.ReadInt32();
            storedTargetNPC = reader.ReadInt32();
            isReaiming = reader.ReadBoolean();
        }

        public override void OnSpawn(Terraria.DataStructures.IEntitySource source)
        {
            storedVelocity = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            Projectile.netUpdate = true;

            if (Variant == ImbuedArrowVariant.Integrity)
            {
                Projectile.width = 16;
                Projectile.height = 16;
                Projectile.scale = 1.4f;
            }

            if (Variant == ImbuedArrowVariant.Kindness)
            {
                Projectile.penetrate = -1;
                Projectile.usesLocalNPCImmunity = true;
                Projectile.localNPCHitCooldown = 10;
            }

            if (Variant == ImbuedArrowVariant.Perseverance)
            {
                Projectile.penetrate = 3;
                Projectile.usesLocalNPCImmunity = true;
                Projectile.localNPCHitCooldown = 10;
            }
        }

        public override void AI()
        {
            Projectile.ai[0]++;
            int timer = (int)Projectile.ai[0];

            Color traitColor = GetTraitColor();
            Lighting.AddLight(Projectile.Center, traitColor.ToVector3() * 0.6f);

            int pauseTime = GetPauseTime();

            if (timer < pauseTime && !hasDashed)
            {
                // Pause phase
                Projectile.velocity = Vector2.Zero;

                if (Main.rand.NextBool(2))
                {
                    Vector2 indicatorPos = Projectile.Center + storedVelocity * Main.rand.NextFloat(20f, 200f);
                    Dust dust = Dust.NewDustPerfect(indicatorPos, DustID.TintableDust, Vector2.Zero, 100, traitColor, 0.6f);
                    dust.noGravity = true;
                    dust.fadeIn = 0.5f;
                }

                if (Main.rand.NextBool(3))
                {
                    Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.TintableDust, 0f, 0f, 100, traitColor, 0.8f);
                    dust.noGravity = true;
                    dust.velocity *= 0.2f;
                }
            }
            else if (!hasDashed)
            {
                // Dash forward
                hasDashed = true;
                Projectile.velocity = storedVelocity * GetDashSpeed();
                Projectile.netUpdate = true;

                if (Main.netMode != NetmodeID.Server)
                {
                    SoundEngine.PlaySound(new SoundStyle("DeterministicChaos/Assets/Sounds/KnifeLaunch") { Volume = 0.7f }, Projectile.Center);
                }

                for (int i = 0; i < 8; i++)
                {
                    Dust dust = Dust.NewDustDirect(Projectile.Center, 0, 0, DustID.TintableDust, 0f, 0f, 100, traitColor, 1.2f);
                    dust.velocity = Main.rand.NextVector2Circular(4f, 4f);
                    dust.noGravity = true;
                }
            }
            else
            {
                Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

                // Kindness: heal allies the arrow passes through
                if (Variant == ImbuedArrowVariant.Kindness && Projectile.owner == Main.myPlayer)
                {
                    for (int i = 0; i < Main.maxPlayers; i++)
                    {
                        Player player = Main.player[i];
                        if (!player.active || player.dead) continue;
                        if (healedPlayers.Contains(i)) continue;

                        if (Projectile.Hitbox.Intersects(player.Hitbox))
                        {
                            healedPlayers.Add(i);
                            RoaringGunPlayer.NotifyAllyHealed(Projectile.owner);
                            Player owner = Main.player[Projectile.owner];
                            int baseHeal = System.Math.Max(1, Projectile.damage * 4 / 100);
                            bool hasEmblem = owner.GetModPlayer<ImbuedEmblemPlayer>().hasKindnessEmblem;
                            int scaledHeal = hasEmblem ? (int)(baseHeal * 1.25f) : baseHeal;
                            if (scaledHeal < baseHeal) scaledHeal = baseHeal;
                            int healAmount = player.GetModPlayer<PrefixEffectPlayer>().ScaleHeal(scaledHeal);

                            player.statLife += healAmount;
                            if (player.statLife > player.statLifeMax2)
                                player.statLife = player.statLifeMax2;
                            player.HealEffect(healAmount);
                        }
                    }
                }

                // Perseverance: pierce through, then pause, then re-aim
                if (Variant == ImbuedArrowVariant.Perseverance && isReaiming)
                {
                    reaimTimer++;

                    // Phase 1 (frames 0-29): Keep moving, pierce through
                    if (reaimTimer < 30)
                    {
                        // Arrow continues on its current trajectory
                    }
                    // Phase 2 (frames 30-59): Stop and show indicator toward target (like a newly fired arrow)
                    else if (reaimTimer < 60)
                    {
                        Projectile.velocity = Vector2.Zero;

                        if (storedTargetNPC >= 0 && storedTargetNPC < Main.maxNPCs)
                        {
                            NPC target = Main.npc[storedTargetNPC];
                            if (target.active)
                            {
                                Vector2 toTarget = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX);
                                Projectile.rotation = toTarget.ToRotation() + MathHelper.PiOver2;

                                if (Main.rand.NextBool(2))
                                {
                                    Vector2 indicatorPos = Projectile.Center + toTarget * Main.rand.NextFloat(20f, 200f);
                                    Dust dust = Dust.NewDustPerfect(indicatorPos, DustID.TintableDust, Vector2.Zero, 100, traitColor, 0.6f);
                                    dust.noGravity = true;
                                    dust.fadeIn = 0.5f;
                                }

                                if (Main.rand.NextBool(3))
                                {
                                    Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.TintableDust, 0f, 0f, 100, traitColor, 0.8f);
                                    dust.noGravity = true;
                                    dust.velocity *= 0.2f;
                                }
                            }
                        }
                    }
                    // Phase 3 (frame 60): Dash toward target
                    else
                    {
                        if (storedTargetNPC >= 0 && storedTargetNPC < Main.maxNPCs)
                        {
                            NPC target = Main.npc[storedTargetNPC];
                            if (target.active && !target.dontTakeDamage)
                            {
                                Vector2 toTarget = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX);
                                Projectile.velocity = toTarget * 24f;
                                Projectile.netUpdate = true;

                                if (Main.netMode != NetmodeID.Server)
                                {
                                    SoundEngine.PlaySound(new SoundStyle("DeterministicChaos/Assets/Sounds/KnifeLaunch") { Volume = 0.5f }, Projectile.Center);
                                }

                                for (int i = 0; i < 8; i++)
                                {
                                    Dust dust = Dust.NewDustDirect(Projectile.Center, 0, 0, DustID.TintableDust, 0f, 0f, 100, traitColor, 1.2f);
                                    dust.velocity = Main.rand.NextVector2Circular(4f, 4f);
                                    dust.noGravity = true;
                                }
                            }
                        }
                        isReaiming = false;
                    }
                }
            }
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
        {
            switch (Variant)
            {
                case ImbuedArrowVariant.Determination:
                    modifiers.FinalDamage += 0.05f;
                    break;
                case ImbuedArrowVariant.Patience:
                    modifiers.FinalDamage += 0.15f;
                    break;
                case ImbuedArrowVariant.Perseverance:
                    if (hasPierced)
                        modifiers.FinalDamage *= 0.25f;
                    break;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            // Justice: explosion on crit
            if (Variant == ImbuedArrowVariant.Justice && hit.Crit)
            {
                if (Projectile.owner == Main.myPlayer)
                {
                    Projectile.NewProjectile(
                        Projectile.GetSource_OnHit(target),
                        target.Center,
                        Vector2.Zero,
                        ModContent.ProjectileType<JusticeArrowExplosion>(),
                        Projectile.damage / 2,
                        4f,
                        Projectile.owner
                    );
                }

                for (int i = 0; i < 15; i++)
                {
                    Vector2 vel = Main.rand.NextVector2CircularEdge(5f, 5f);
                    Dust dust = Dust.NewDustPerfect(target.Center, DustID.GoldFlame, vel, 0, default, 1.5f);
                    dust.noGravity = true;
                }
            }

            // Perseverance: start re-aim on first hit
            if (Variant == ImbuedArrowVariant.Perseverance && !hasPierced)
            {
                hasPierced = true;
                isReaiming = true;
                reaimTimer = 0;
                storedTargetNPC = target.whoAmI;
                Projectile.netUpdate = true;
            }


        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Vector2 origin = new Vector2(texture.Width * 0.5f, texture.Height * 0.5f);
            Color traitColor = GetTraitColor();
            int timer = (int)Projectile.ai[0];

            // Afterimages when dashing
            if (hasDashed)
            {
                for (int i = 0; i < Projectile.oldPos.Length; i++)
                {
                    if (Projectile.oldPos[i] == Vector2.Zero) continue;

                    Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition;
                    float trailOpacity = (Projectile.oldPos.Length - i) / (float)Projectile.oldPos.Length;
                    Color trailColor = traitColor * trailOpacity * 0.5f;

                    Main.EntitySpriteDraw(texture, drawPos, null, trailColor, Projectile.oldRot[i], origin, Projectile.scale, SpriteEffects.None, 0);
                }
            }

            // Main projectile in trait color
            Vector2 mainPos = Projectile.Center - Main.screenPosition;
            Main.EntitySpriteDraw(texture, mainPos, null, traitColor, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);

            // Indicator line during pause
            if (!hasDashed)
            {
                int pauseTime = GetPauseTime();
                float progress = timer / (float)pauseTime;
                float lengthMultiplier = 1f + 2f * progress;
                float lineLength = 670f * progress * lengthMultiplier;
                Color lineColor = Color.Lerp(traitColor * 0.3f, traitColor * 0.8f, progress);

                Vector2 lineStart = Projectile.Center - Main.screenPosition;
                Vector2 lineEnd = lineStart + storedVelocity * lineLength;

                Texture2D pixel = TextureAssets.MagicPixel.Value;
                Vector2 lineVector = lineEnd - lineStart;
                float lineRotation = lineVector.ToRotation();
                float lineScale = lineVector.Length();

                Main.EntitySpriteDraw(pixel, lineStart, new Rectangle(0, 0, 1, 1), lineColor, lineRotation, Vector2.Zero, new Vector2(lineScale, 2f), SpriteEffects.None, 0);
            }

            return false;
        }

        public override void OnKill(int timeLeft)
        {
            Color traitColor = GetTraitColor();
            for (int i = 0; i < 10; i++)
            {
                Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.TintableDust, 0f, 0f, 100, traitColor, 1.0f);
                dust.velocity = Main.rand.NextVector2Circular(4f, 4f);
                dust.noGravity = true;
            }

            SoundEngine.PlaySound(SoundID.Dig, Projectile.position);
        }
    }

    // ---- Concrete variant projectiles ----

    public class DeterminationArrowProjectile : ImbuedArrowProjectile
    {
        protected override ImbuedArrowVariant Variant => ImbuedArrowVariant.Determination;
    }

    public class IntegrityArrowProjectile : ImbuedArrowProjectile
    {
        protected override ImbuedArrowVariant Variant => ImbuedArrowVariant.Integrity;
    }

    public class PatienceArrowProjectile : ImbuedArrowProjectile
    {
        protected override ImbuedArrowVariant Variant => ImbuedArrowVariant.Patience;
    }

    public class PerseveranceArrowProjectile : ImbuedArrowProjectile
    {
        protected override ImbuedArrowVariant Variant => ImbuedArrowVariant.Perseverance;
    }

    public class KindnessArrowProjectile : ImbuedArrowProjectile
    {
        protected override ImbuedArrowVariant Variant => ImbuedArrowVariant.Kindness;
    }

    public class JusticeArrowProjectile : ImbuedArrowProjectile
    {
        protected override ImbuedArrowVariant Variant => ImbuedArrowVariant.Justice;
    }

    public class BraveryArrowProjectile : ImbuedArrowProjectile
    {
        protected override ImbuedArrowVariant Variant => ImbuedArrowVariant.Bravery;
    }
}
