using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
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
using DeterministicChaos.Content.Items.Imbued;

namespace DeterministicChaos.Content.Projectiles.Friendly
{
    public abstract class ImbuedDarkShardProjectile : ModProjectile
    {
        protected abstract ImbuedDarkShardVariant Variant { get; }

        // ai[0]: 1 = stealth strike, 0 = normal
        protected bool IsStealthStrike => Projectile.ai[0] == 1f;

        public override string Texture => "DeterministicChaos/Content/Projectiles/Friendly/DarkShardProjectile";

        protected Color GetTraitColor()
        {
            return Variant switch
            {
                ImbuedDarkShardVariant.Determination => new Color(255, 60, 60),
                ImbuedDarkShardVariant.Integrity => new Color(0, 0, 255),
                ImbuedDarkShardVariant.Patience => new Color(80, 255, 255),
                ImbuedDarkShardVariant.Perseverance => new Color(255, 80, 255),
                ImbuedDarkShardVariant.Kindness => new Color(80, 230, 80),
                ImbuedDarkShardVariant.Justice => new Color(255, 255, 80),
                ImbuedDarkShardVariant.Bravery => new Color(255, 190, 60),
                _ => Color.White
            };
        }

        private bool integrityScaled = false;

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 8;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults()
        {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 600;
            Projectile.aiStyle = -1;
            Projectile.ignoreWater = false;
            Projectile.tileCollide = true;
        }

        public override void AI()
        {
            // Integrity: Scale up projectile once
            if (Variant == ImbuedDarkShardVariant.Integrity && !integrityScaled)
            {
                integrityScaled = true;
                Projectile.scale *= 1.5f;
                Projectile.damage = (int)(Projectile.damage * 1.25f);
                Projectile.width = (int)(Projectile.width * 1.5f);
                Projectile.height = (int)(Projectile.height * 1.5f);
            }

            // Rotate to face movement direction
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver4;

            // Apply gravity — Integrity gets double gravity
            float gravity = Variant == ImbuedDarkShardVariant.Integrity ? 0.30f : 0.15f;
            Projectile.velocity.Y += gravity;
            if (Projectile.velocity.Y > 16f)
                Projectile.velocity.Y = 16f;

            // Emit trait-colored light
            Color c = GetTraitColor();
            Lighting.AddLight(Projectile.Center, c.R / 255f * 0.5f, c.G / 255f * 0.5f, c.B / 255f * 0.5f);

            // Spawn dark dust trail
            if (Main.rand.NextBool(3))
            {
                Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Shadowflame, 0f, 0f, 100, default, 0.8f);
                dust.noGravity = true;
                dust.velocity *= 0.3f;
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = Terraria.GameContent.TextureAssets.Projectile[Projectile.type].Value;
            Vector2 drawOrigin = new Vector2(texture.Width * 0.5f, texture.Height * 0.5f);
            Color traitColor = GetTraitColor();

            // Draw afterimages
            for (int i = 0; i < Projectile.oldPos.Length; i++)
            {
                Vector2 drawPos = Projectile.oldPos[i] - Main.screenPosition + drawOrigin + new Vector2(0f, Projectile.gfxOffY);
                float trailOpacity = (Projectile.oldPos.Length - i) / (float)Projectile.oldPos.Length;
                Color afterimageColor = traitColor * trailOpacity * 0.5f;
                Main.EntitySpriteDraw(texture, drawPos, null, afterimageColor, Projectile.oldRot[i], drawOrigin, Projectile.scale, SpriteEffects.None, 0);
            }

            // Draw main projectile with trait tint
            Vector2 mainDrawPos = Projectile.position - Main.screenPosition + drawOrigin + new Vector2(0f, Projectile.gfxOffY);
            Main.EntitySpriteDraw(texture, mainDrawPos, null, traitColor, Projectile.rotation, drawOrigin, Projectile.scale, SpriteEffects.None, 0);

            return false;
        }

        public override void OnKill(int timeLeft)
        {
            for (int i = 0; i < 10; i++)
            {
                Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Shadowflame, 0f, 0f, 100, default, 1.2f);
                dust.velocity = Main.rand.NextVector2Circular(4f, 4f);
                dust.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (Main.myPlayer != Projectile.owner)
                return;

            Player owner = Main.player[Projectile.owner];

            // Kindness: Spawn healing knife on ally instead of seeking knife
            if (Variant == ImbuedDarkShardVariant.Kindness)
            {
                SpawnKindnessHealingKnife(owner, target, damageDone);
                return;
            }

            // All other variants (and Bravery) spawn seeking knife
            SpawnSeekingKnife(target, damageDone);

            // Justice: Stealth strike crits refund 10 stealth meter
            if (Variant == ImbuedDarkShardVariant.Justice && IsStealthStrike && hit.Crit)
            {
                DarkShardPlayer.RefundCalamityStealth(owner, 0.15f);
            }
        }

        private void SpawnSeekingKnife(NPC target, int damageDone)
        {
            int knifeDamage = (int)(damageDone * 0.5f);
            float orbitAngle = Main.rand.NextFloat(MathHelper.TwoPi);

            int p = Projectile.NewProjectile(
                Projectile.GetSource_OnHit(target),
                target.Center,
                Vector2.Zero,
                ModContent.ProjectileType<RogueSeekingKnife>(),
                knifeDamage,
                2f,
                Projectile.owner,
                target.whoAmI,
                orbitAngle
            );

            if (p >= 0 && p < Main.maxProjectiles)
            {
                Main.projectile[p].localAI[0] = (int)Variant;
                Main.projectile[p].netUpdate = true;
            }
        }

        private void SpawnKindnessHealingKnife(Player owner, NPC target, int damageDone)
        {
            // Find a random nearby ally (including self)
            int chosenPlayer = -1;
            float range = 600f;
            int candidateCount = 0;

            for (int i = 0; i < Main.maxPlayers; i++)
            {
                Player p = Main.player[i];
                if (p.active && !p.dead && Vector2.Distance(target.Center, p.Center) <= range)
                {
                    candidateCount++;
                    if (Main.rand.Next(candidateCount) == 0)
                        chosenPlayer = i;
                }
            }

            if (chosenPlayer < 0)
                return;

            float orbitAngle = Main.rand.NextFloat(MathHelper.TwoPi);

            int p2 = Projectile.NewProjectile(
                Projectile.GetSource_OnHit(target),
                target.Center,
                Vector2.Zero,
                ModContent.ProjectileType<DarkShardHealingKnife>(),
                0,
                0f,
                Projectile.owner,
                chosenPlayer,
                orbitAngle
            );

            if (p2 >= 0 && p2 < Main.maxProjectiles)
                Main.projectile[p2].netUpdate = true;
        }

        public override bool OnTileCollide(Vector2 oldVelocity)
        {
            Terraria.Audio.SoundEngine.PlaySound(SoundID.Dig, Projectile.position);
            return true;
        }
    }

    // --- Concrete variants ---

    public class DeterminationDarkShardProjectile : ImbuedDarkShardProjectile
    {
        protected override ImbuedDarkShardVariant Variant => ImbuedDarkShardVariant.Determination;
    }

    public class IntegrityDarkShardProjectile : ImbuedDarkShardProjectile
    {
        protected override ImbuedDarkShardVariant Variant => ImbuedDarkShardVariant.Integrity;
    }

    public class PatienceDarkShardProjectile : ImbuedDarkShardProjectile
    {
        protected override ImbuedDarkShardVariant Variant => ImbuedDarkShardVariant.Patience;
    }

    public class PerseveranceDarkShardProjectile : ImbuedDarkShardProjectile
    {
        protected override ImbuedDarkShardVariant Variant => ImbuedDarkShardVariant.Perseverance;
    }

    public class KindnessDarkShardProjectile : ImbuedDarkShardProjectile
    {
        protected override ImbuedDarkShardVariant Variant => ImbuedDarkShardVariant.Kindness;
    }

    public class JusticeDarkShardProjectile : ImbuedDarkShardProjectile
    {
        protected override ImbuedDarkShardVariant Variant => ImbuedDarkShardVariant.Justice;
    }

    public class BraveryDarkShardProjectile : ImbuedDarkShardProjectile
    {
        protected override ImbuedDarkShardVariant Variant => ImbuedDarkShardVariant.Bravery;
    }
}
