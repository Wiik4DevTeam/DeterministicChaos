using System;
using System.Collections.Generic;
using System.IO;
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
using DeterministicChaos.Content.SoulTraits.Armor;

namespace DeterministicChaos.Content.Projectiles.Friendly
{
    public enum ImbuedBulletVariant
    {
        Determination,
        Integrity,
        Patience,
        Perseverance,
        Kindness,
        Justice,
        Bravery
    }

    public abstract class ImbuedBulletProjectile : ModProjectile
    {
        protected abstract ImbuedBulletVariant Variant { get; }

        private const int BaseExplosionThreshold = 6;
        private const int BaseAttachDuration = 120;
        private const int BaseExplosionWindow = 120;

        // ai[0] = 0 flying, 1 attached
        // ai[1] = attached NPC whoAmI
        // ai[2] = stored damage
        // localAI[0] = attach timer
        // localAI[1] = attach offset X
        // localAI[2] = attach offset Y

        // Perseverance state
        private bool isEchoBullet = false;

        // Bravery state
        private Vector2 spawnPosition;

        // Kindness state
        private HashSet<int> healedPlayers = new HashSet<int>();
        private HashSet<int> healedNPCs = new HashSet<int>();

        public bool IsAttached => Projectile.ai[0] == 1f;
        public int AttachedNPC => (int)Projectile.ai[1];
        public int StoredDamage => (int)Projectile.ai[2];

        public override string Texture => "DeterministicChaos/Content/Projectiles/Friendly/RoaringBulletProjectile";

        protected Color GetTraitColor()
        {
            return Variant switch
            {
                ImbuedBulletVariant.Determination => new Color(255, 60, 60),
                ImbuedBulletVariant.Integrity => new Color(0, 0, 255),
                ImbuedBulletVariant.Patience => new Color(80, 255, 255),
                ImbuedBulletVariant.Perseverance => new Color(255, 80, 255),
                ImbuedBulletVariant.Kindness => new Color(80, 230, 80),
                ImbuedBulletVariant.Justice => new Color(255, 255, 80),
                ImbuedBulletVariant.Bravery => new Color(255, 190, 60),
                _ => Color.White
            };
        }

        private int GetExplosionThreshold()
        {
            return Variant == ImbuedBulletVariant.Patience ? 12 : BaseExplosionThreshold;
        }

        private int GetAttachDuration()
        {
            return Variant == ImbuedBulletVariant.Patience ? 300 : BaseAttachDuration;
        }

        private int GetExplosionWindow()
        {
            return Variant == ImbuedBulletVariant.Patience ? 300 : BaseExplosionWindow;
        }

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 5;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 0;
        }

        public override void SetDefaults()
        {
            Projectile.width = 6;
            Projectile.height = 6;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 600;
            Projectile.aiStyle = -1;
            Projectile.extraUpdates = 1;
            Projectile.ai[1] = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void OnSpawn(Terraria.DataStructures.IEntitySource source)
        {
            spawnPosition = Projectile.Center;

            if (Variant == ImbuedBulletVariant.Kindness)
            {
                Projectile.friendly = false;
            }

            // Justice: 30% reduced base damage to compensate for hypercrits
            if (Variant == ImbuedBulletVariant.Justice)
            {
                Projectile.damage = (int)(Projectile.damage * 0.7f);
            }

            // Perseverance: if player has 25 mana, consume it and spawn an echo bullet behind
            // Check if source is a projectile parent (echo bullet) — EntitySource_ItemUse_WithAmmo also inherits EntitySource_Parent,
            // so we must specifically check that the parent entity is a Projectile, not just any parent source.
            bool spawnedByBullet = source is Terraria.DataStructures.EntitySource_Parent parentSource
                && parentSource.Entity is Projectile;
            if (Variant == ImbuedBulletVariant.Perseverance && !spawnedByBullet && Projectile.owner == Main.myPlayer)
            {
                Player owner = Main.player[Projectile.owner];
                if (owner.statMana >= 25)
                {
                    owner.statMana -= 25;
                    owner.manaRegenDelay = (int)owner.maxRegenDelay;

                    // Spawn echo bullet behind the initial one
                    Vector2 behindOffset = -Projectile.velocity.SafeNormalize(Vector2.UnitX) * 20f;
                    int echoProjIndex = Projectile.NewProjectile(
                        Projectile.GetSource_FromThis(),
                        Projectile.Center + behindOffset,
                        Projectile.velocity,
                        Projectile.type,
                        Projectile.damage / 2,
                        Projectile.knockBack * 0.5f,
                        Projectile.owner
                    );

                    if (echoProjIndex >= 0 && echoProjIndex < Main.maxProjectiles)
                    {
                        Projectile echoProj = Main.projectile[echoProjIndex];
                        if (echoProj.ModProjectile is ImbuedBulletProjectile echoBullet)
                        {
                            echoBullet.isEchoBullet = true;
                            echoProj.netUpdate = true;
                        }
                    }
                }
            }
        }

        public override void SendExtraAI(BinaryWriter writer)
        {
            writer.Write(Projectile.localAI[0]);
            writer.Write(Projectile.localAI[1]);
            writer.Write(Projectile.localAI[2]);
            writer.Write(isEchoBullet);
            writer.WriteVector2(spawnPosition);
        }

        public override void ReceiveExtraAI(BinaryReader reader)
        {
            Projectile.localAI[0] = reader.ReadSingle();
            Projectile.localAI[1] = reader.ReadSingle();
            Projectile.localAI[2] = reader.ReadSingle();
            isEchoBullet = reader.ReadBoolean();
            spawnPosition = reader.ReadVector2();
        }

        public override bool? CanHitNPC(NPC target)
        {
            if (Variant == ImbuedBulletVariant.Kindness)
                return false;
            return null;
        }

        public override void AI()
        {
            Color traitColor = GetTraitColor();
            Lighting.AddLight(Projectile.Center, traitColor.ToVector3() * 0.4f);

            // Kindness: home toward allies and heal on contact
            if (Variant == ImbuedBulletVariant.Kindness)
            {
                KindnessBehavior();
                return;
            }

            if (IsAttached)
            {
                AttachedBehavior();
            }
            else
            {
                FlyingBehavior();
            }
        }

        private void KindnessBehavior()
        {
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            // Home toward nearest ally (non-owner players and town NPCs)
            float nearestDist = float.MaxValue;
            Vector2 targetCenter = Vector2.Zero;
            bool hasTarget = false;

            for (int i = 0; i < Main.maxPlayers; i++)
            {
                if (i == Projectile.owner) continue;
                Player player = Main.player[i];
                if (!player.active || player.dead) continue;

                float dist = player.Center.Distance(Projectile.Center);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    targetCenter = player.Center;
                    hasTarget = true;
                }
            }

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (!npc.active || !npc.friendly) continue;

                float dist = npc.Center.Distance(Projectile.Center);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    targetCenter = npc.Center;
                    hasTarget = true;
                }
            }

            if (hasTarget)
            {
                Vector2 toTarget = (targetCenter - Projectile.Center).SafeNormalize(Vector2.UnitX);
                float speed = Projectile.velocity.Length();
                Projectile.velocity = Vector2.Lerp(Projectile.velocity.SafeNormalize(Vector2.UnitX), toTarget, 0.08f) * speed;
            }

            // Dust trail
            Color traitColor = GetTraitColor();
            if (Main.rand.NextBool(3))
            {
                Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.TintableDust, 0f, 0f, 100, traitColor, 0.8f);
                dust.noGravity = true;
                dust.velocity *= 0.3f;
            }

            // Check for ally contact and heal (non-owner players and town NPCs only)
            if (Projectile.owner == Main.myPlayer)
            {
                Player owner = Main.player[Projectile.owner];
                int baseHeal = Math.Max(1, Projectile.damage * 10 / 100);
                bool hasEmblem = owner.GetModPlayer<ImbuedEmblemPlayer>().hasKindnessEmblem;
                int scaledHeal = hasEmblem ? (int)(baseHeal * 1.25f) : baseHeal;
                if (scaledHeal < baseHeal) scaledHeal = baseHeal;

                for (int i = 0; i < Main.maxPlayers; i++)
                {
                    if (i == Projectile.owner) continue;
                    Player player = Main.player[i];
                    if (!player.active || player.dead) continue;
                    if (healedPlayers.Contains(i)) continue;

                    if (Projectile.Hitbox.Intersects(player.Hitbox))
                    {
                        healedPlayers.Add(i);
                        RoaringGunPlayer.NotifyAllyHealed(Projectile.owner);
                        int healAmount = player.GetModPlayer<PrefixEffectPlayer>().ScaleHeal(scaledHeal);

                        player.statLife += healAmount;
                        if (player.statLife > player.statLifeMax2)
                            player.statLife = player.statLifeMax2;
                        player.HealEffect(healAmount);

                        Projectile.Kill();
                        return;
                    }
                }

                for (int i = 0; i < Main.maxNPCs; i++)
                {
                    NPC npc = Main.npc[i];
                    if (!npc.active || !npc.friendly) continue;
                    if (healedNPCs.Contains(i)) continue;

                    if (Projectile.Hitbox.Intersects(npc.Hitbox))
                    {
                        healedNPCs.Add(i);
                        npc.life += scaledHeal;
                        if (npc.life > npc.lifeMax)
                            npc.life = npc.lifeMax;
                        npc.HealEffect(scaledHeal);

                        Projectile.Kill();
                        return;
                    }
                }
            }
        }

        private void FlyingBehavior()
        {
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            Color traitColor = GetTraitColor();
            if (Main.rand.NextBool(3))
            {
                Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.TintableDust, 0f, 0f, 100, traitColor, 0.8f);
                dust.noGravity = true;
                dust.velocity *= 0.3f;
            }
        }

        private void AttachedBehavior()
        {
            Projectile.localAI[0]++;
            int attachDuration = GetAttachDuration();

            if (Projectile.localAI[0] >= attachDuration)
            {
                Projectile.Kill();
                return;
            }

            int npcIndex = AttachedNPC;
            if (npcIndex < 0 || npcIndex >= Main.maxNPCs || !Main.npc[npcIndex].active)
            {
                Projectile.Kill();
                return;
            }

            NPC target = Main.npc[npcIndex];
            Vector2 offset = new Vector2(Projectile.localAI[1], Projectile.localAI[2]);
            Projectile.Center = target.Center + offset;
            Projectile.velocity = Vector2.Zero;

            Color traitColor = GetTraitColor();
            float pulse = 0.5f + 0.5f * (float)Math.Sin(Projectile.localAI[0] * 0.2f);
            Lighting.AddLight(Projectile.Center, traitColor.ToVector3() * 0.4f * pulse);

            if (Main.rand.NextBool(10))
            {
                Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.TintableDust, 0f, 0f, 100, traitColor, 0.5f);
                dust.noGravity = true;
                dust.velocity = Main.rand.NextVector2Circular(1f, 1f);
            }
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
        {
            switch (Variant)
            {
                case ImbuedBulletVariant.Determination:
                    modifiers.FinalDamage += 0.08f;
                    break;
                case ImbuedBulletVariant.Bravery:
                    float distance = Vector2.Distance(spawnPosition, Projectile.Center);
                    float bonus = MathHelper.Lerp(0.25f, 0f, MathHelper.Clamp(distance / 800f, 0f, 1f));
                    modifiers.FinalDamage += bonus;
                    break;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            // All variants: attach to target
            if (!IsAttached && target.active && !target.friendly)
            {
                Projectile.ai[0] = 1f;
                Projectile.ai[1] = target.whoAmI;
                Projectile.localAI[0] = 0f;

                Projectile.localAI[1] = Main.rand.NextFloat(-target.width * 0.4f, target.width * 0.4f);
                Projectile.localAI[2] = Main.rand.NextFloat(-target.height * 0.4f, target.height * 0.4f);

                Projectile.ai[2] = Projectile.damage;

                Projectile.penetrate = -1;
                Projectile.damage = 0;
                Projectile.timeLeft = GetAttachDuration() + 10;

                Projectile.netUpdate = true;

                if (Main.netMode != NetmodeID.Server)
                    SoundEngine.PlaySound(SoundID.Item10 with { Volume = 0.5f, Pitch = 0.5f }, Projectile.Center);

                // Integrity: no explosion
                if (Variant != ImbuedBulletVariant.Integrity)
                    CheckForExplosion(target);
            }
        }

        private void CheckForExplosion(NPC target)
        {
            List<Projectile> attachedBullets = new List<Projectile>();
            int threshold = GetExplosionThreshold();
            int explosionWindow = GetExplosionWindow();

            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile proj = Main.projectile[i];
                if (proj.active && proj.type == Projectile.type && proj.owner == Projectile.owner)
                {
                    if (proj.ModProjectile is ImbuedBulletProjectile bullet && bullet.IsAttached && bullet.AttachedNPC == target.whoAmI)
                    {
                        if (proj.localAI[0] < explosionWindow)
                            attachedBullets.Add(proj);
                    }
                }
            }

            if (attachedBullets.Count >= threshold)
                TriggerExplosion(target, attachedBullets);
        }

        private void TriggerExplosion(NPC target, List<Projectile> bullets)
        {
            int explosionDamage = 0;
            foreach (Projectile bullet in bullets)
                explosionDamage += (int)bullet.ai[2];
            explosionDamage /= 2;

            // Patience: 20% increased explosion damage
            if (Variant == ImbuedBulletVariant.Patience)
                explosionDamage = (int)(explosionDamage * 1.2f);

            if (Projectile.owner == Main.myPlayer &&
                Projectile.owner >= 0 && Projectile.owner < Main.maxPlayers &&
                target.active && !target.friendly)
            {
                Player player = Main.player[Projectile.owner];
                if (player != null && player.active)
                {
                    bool isCrit = Variant == ImbuedBulletVariant.Justice
                        ? true
                        : Main.rand.Next(100) < player.GetCritChance(DamageClass.Ranged);

                    // Justice: guaranteed crit + roll for hypercrit (4x total)
                    bool isHypercrit = false;
                    if (Variant == ImbuedBulletVariant.Justice)
                    {
                        float hypercritChance = player.GetTotalCritChance(DamageClass.Ranged);
                        if (Main.rand.Next(100) < (int)hypercritChance)
                        {
                            explosionDamage = (int)(explosionDamage * 1.5f);
                            isHypercrit = true;
                        }
                    }

                    NPC.HitInfo hitInfo = new NPC.HitInfo
                    {
                        Damage = explosionDamage,
                        Knockback = 8f,
                        HitDirection = player.direction,
                        Crit = isCrit
                    };
                    target.StrikeNPC(hitInfo);

                    if (Main.netMode != NetmodeID.SinglePlayer)
                        NetMessage.SendStrikeNPC(target, hitInfo);

                    // Justice hypercrit VFX/SFX
                    if (isHypercrit)
                    {
                        for (int i = 0; i < 25; i++)
                        {
                            Vector2 vel = Main.rand.NextVector2Circular(6f, 6f);
                            Dust dust = Dust.NewDustDirect(target.Center, 1, 1, DustID.TintableDust, vel.X, vel.Y, 100, new Color(255, 255, 50), 1.5f);
                            dust.noGravity = true;
                        }

                        CombatText.NewText(target.Hitbox, new Color(255, 255, 50), explosionDamage, dramatic: true);
                        SoundEngine.PlaySound(new SoundStyle("DeterministicChaos/Assets/Sounds/Hypercrit") { Volume = 0.6f }, target.Center);

                        // Sheriff Hat synergy
                        var hatPlayer = player.GetModPlayer<CowboyHatPlayer>();
                        if (hatPlayer.hasSheriffHat)
                            hatPlayer.hypercritAttackSpeedTimer = 36;
                    }
                }
            }

            // Visual effects
            Color traitColor = GetTraitColor();
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 1.2f, Pitch = -0.3f }, target.Center);
            SoundEngine.PlaySound(SoundID.NPCDeath6 with { Volume = 0.8f }, target.Center);

            for (int i = 0; i < 40; i++)
            {
                Vector2 velocity = Main.rand.NextVector2Circular(8f, 8f);
                Dust dust = Dust.NewDustDirect(target.Center, 1, 1, DustID.TintableDust, velocity.X, velocity.Y, 100, traitColor, 2f);
                dust.noGravity = true;
            }

            for (int i = 0; i < 20; i++)
            {
                Vector2 velocity = Main.rand.NextVector2Circular(4f, 4f);
                Dust dust = Dust.NewDustDirect(target.Center, 1, 1, DustID.Smoke, velocity.X, velocity.Y, 150, traitColor, 2f);
                dust.noGravity = true;
            }

            foreach (Projectile bullet in bullets)
            {
                for (int i = 0; i < 5; i++)
                {
                    Dust dust = Dust.NewDustDirect(bullet.Center, 1, 1, DustID.TintableDust,
                        Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-2f, 2f), 100, traitColor, 1f);
                    dust.noGravity = true;
                }
                bullet.Kill();
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Color traitColor = GetTraitColor();

            if (!IsAttached)
            {
                Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
                Vector2 origin = texture.Size() * 0.5f;

                for (int i = 0; i < Projectile.oldPos.Length; i++)
                {
                    Vector2 drawPos = Projectile.oldPos[i] - Main.screenPosition + new Vector2(Projectile.width / 2, Projectile.height / 2);
                    Color trailColor = traitColor * (1f - i / (float)Projectile.oldPos.Length) * 0.5f;
                    Main.EntitySpriteDraw(texture, drawPos, null, trailColor, Projectile.rotation, origin, Projectile.scale * (1f - i * 0.1f), SpriteEffects.None, 0);
                }
            }

            return true;
        }

        public override void PostDraw(Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = texture.Size() * 0.5f;

            Color traitColor = GetTraitColor();
            float glowIntensity = IsAttached ? 0.5f + 0.3f * (float)Math.Sin(Projectile.localAI[0] * 0.2f) : 0.6f;
            Color glowColor = traitColor * glowIntensity;

            Main.EntitySpriteDraw(texture, drawPos, null, glowColor, Projectile.rotation, origin, Projectile.scale * 1.2f, SpriteEffects.None, 0);
        }

        public override Color? GetAlpha(Color lightColor)
        {
            return GetTraitColor();
        }
    }

    // GlobalNPC for Integrity bullet armor penetration
    public class IntegrityBulletGlobalNPC : GlobalNPC
    {
        public override void ModifyHitByProjectile(NPC npc, Projectile projectile, ref NPC.HitModifiers modifiers)
        {
            int count = CountAttachedIntegrityBullets(npc);
            if (count > 0)
                modifiers.ArmorPenetration += Math.Min(count, 10);
        }

        public override void ModifyHitByItem(NPC npc, Player player, Item item, ref NPC.HitModifiers modifiers)
        {
            int count = CountAttachedIntegrityBullets(npc);
            if (count > 0)
                modifiers.ArmorPenetration += Math.Min(count, 10);
        }

        private int CountAttachedIntegrityBullets(NPC npc)
        {
            int count = 0;
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile proj = Main.projectile[i];
                if (proj.active && proj.ModProjectile is IntegrityBulletProjectile bullet && bullet.IsAttached && bullet.AttachedNPC == npc.whoAmI)
                    count++;
            }
            return count;
        }
    }

    // Concrete variant classes
    public class DeterminationBulletProjectile : ImbuedBulletProjectile
    {
        protected override ImbuedBulletVariant Variant => ImbuedBulletVariant.Determination;
    }

    public class IntegrityBulletProjectile : ImbuedBulletProjectile
    {
        protected override ImbuedBulletVariant Variant => ImbuedBulletVariant.Integrity;
    }

    public class PatienceBulletProjectile : ImbuedBulletProjectile
    {
        protected override ImbuedBulletVariant Variant => ImbuedBulletVariant.Patience;
    }

    public class PerseveranceBulletProjectile : ImbuedBulletProjectile
    {
        protected override ImbuedBulletVariant Variant => ImbuedBulletVariant.Perseverance;
    }

    public class KindnessBulletProjectile : ImbuedBulletProjectile
    {
        protected override ImbuedBulletVariant Variant => ImbuedBulletVariant.Kindness;
    }

    public class JusticeBulletProjectile : ImbuedBulletProjectile
    {
        protected override ImbuedBulletVariant Variant => ImbuedBulletVariant.Justice;
    }

    public class BraveryBulletProjectile : ImbuedBulletProjectile
    {
        protected override ImbuedBulletVariant Variant => ImbuedBulletVariant.Bravery;
    }
}
