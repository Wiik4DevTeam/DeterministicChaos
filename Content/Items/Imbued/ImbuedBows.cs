using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using DeterministicChaos.Content.Items.Sparks;
using DeterministicChaos.Content.SoulTraits;
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

namespace DeterministicChaos.Content.Items.Imbued
{
    public enum ImbuedBowVariant
    {
        Determination,
        Integrity,
        Patience,
        Perseverance,
        Kindness,
        Justice,
        Bravery
    }

    public abstract class ImbuedBowBase : ModItem
    {
        protected abstract ImbuedBowVariant Variant { get; }
        protected abstract int GetSparkType();

        public override string Texture => "DeterministicChaos/Content/Items/Weapons/RoaringBow";

        private SoulTraitType GetSoulTraitType()
        {
            return Variant switch
            {
                ImbuedBowVariant.Determination => SoulTraitType.Determination,
                ImbuedBowVariant.Integrity => SoulTraitType.Integrity,
                ImbuedBowVariant.Patience => SoulTraitType.Patience,
                ImbuedBowVariant.Perseverance => SoulTraitType.Perseverance,
                ImbuedBowVariant.Kindness => SoulTraitType.Kindness,
                ImbuedBowVariant.Justice => SoulTraitType.Justice,
                ImbuedBowVariant.Bravery => SoulTraitType.Bravery,
                _ => SoulTraitType.None
            };
        }

        public override void SetStaticDefaults()
        {
            SoulTraitGlobalItem.RegisterWeaponInvestment(Type, 6, GetSoulTraitType());
        }

        protected Color GetTraitColor()
        {
            return Variant switch
            {
                ImbuedBowVariant.Determination => new Color(255, 60, 60),
                ImbuedBowVariant.Integrity => new Color(0, 0, 255),
                ImbuedBowVariant.Patience => new Color(80, 255, 255),
                ImbuedBowVariant.Perseverance => new Color(255, 80, 255),
                ImbuedBowVariant.Kindness => new Color(80, 230, 80),
                ImbuedBowVariant.Justice => new Color(255, 255, 80),
                ImbuedBowVariant.Bravery => new Color(255, 190, 60),
                _ => Color.White
            };
        }

        public override void SetDefaults()
        {
            Item.width = 20;
            Item.height = 40;
            Item.damage = 50;
            Item.knockBack = 2f;
            Item.useTime = 3;
            Item.useAnimation = 12;
            Item.reuseDelay = 30;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.autoReuse = true;
            Item.rare = ModContent.RarityType<DarkWorldRarity>();
            Item.value = Item.buyPrice(gold: 5);
            Item.UseSound = SoundID.Item5;
            Item.shoot = ProjectileID.WoodenArrowFriendly;
            Item.shootSpeed = 12f;
            Item.useAmmo = AmmoID.Arrow;
            Item.DamageType = DamageClass.Ranged;

            // Integrity: Double fire rate (half useTime/useAnimation/reuseDelay)
            if (Variant == ImbuedBowVariant.Integrity)
            {
                Item.useTime = 6;
                Item.useAnimation = 6;
                Item.reuseDelay = 15;
            }

            // Patience: 20% reduced fire rate
            if (Variant == ImbuedBowVariant.Patience)
            {
                Item.useTime = (int)(3 * 1.20f);
                Item.useAnimation = (int)(12 * 1.20f);
                Item.reuseDelay = (int)(30 * 1.20f);
            }
        }

        public override void ModifyWeaponDamage(Player player, ref StatModifier damage)
        {
            // Determination: +8% total damage
            if (Variant == ImbuedBowVariant.Determination)
                damage *= 1.08f;
        }

        public override bool CanConsumeAmmo(Item ammo, Player player)
        {
            // Patience: No longer consumes ammo
            if (Variant == ImbuedBowVariant.Patience)
                return false;

            return base.CanConsumeAmmo(ammo, player);
        }

        public override void HoldItem(Player player)
        {
            Lighting.AddLight(player.Center, 0.9f, 0.9f, 0.9f);
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            // --- Integrity: Only 2 arrows per shot (no V spread, just the two closest to center) ---
            if (Variant == ImbuedBowVariant.Integrity)
            {
                // Fire 2 arrows with tight spread (smallest V spread from base weapon)
                float spreadAngle = MathHelper.ToRadians(5f);
                Projectile.NewProjectile(source, position, velocity.RotatedBy(-spreadAngle), type, damage, knockback, player.whoAmI);
                Projectile.NewProjectile(source, position, velocity.RotatedBy(spreadAngle), type, damage, knockback, player.whoAmI);
                return false;
            }

            // --- Base burst calculation (same as RoaringBow) ---
            int shotsRemaining = (int)((player.itemAnimation - 1) / (float)Item.useTime);
            int maxBursts = (Item.useAnimation / Item.useTime) - 1;
            int burstNumber = maxBursts - Math.Clamp(shotsRemaining, 0, maxBursts);

            float baseSpread = 20f;
            float spreadDivisor = (float)Math.Pow(2, burstNumber);
            float spreadAngleBase = MathHelper.ToRadians(baseSpread / spreadDivisor);

            // --- Perseverance: Consume 100 mana for 4 extra arrows per volley ---
            int extraPerseveranceArrows = 0;
            if (Variant == ImbuedBowVariant.Perseverance && burstNumber == 0)
            {
                // Reset and re-check mana at the start of each volley
                var bowPlayer = player.GetModPlayer<ImbuedBowPlayer>();
                bowPlayer.perseveranceExtraArrows = false;
                if (DarkShardPlayer.TryConsumeMana(player, 100))
                {
                    bowPlayer.perseveranceExtraArrows = true;
                }
            }
            if (Variant == ImbuedBowVariant.Perseverance)
            {
                var bowPlayer = player.GetModPlayer<ImbuedBowPlayer>();
                if (bowPlayer.perseveranceExtraArrows)
                    extraPerseveranceArrows = 4;
            }

            // --- Justice: Each double jump used adds 2 arrows ---
            int justiceExtraArrows = 0;
            if (Variant == ImbuedBowVariant.Justice)
            {
                var bowPlayer = player.GetModPlayer<ImbuedBowPlayer>();
                justiceExtraArrows = bowPlayer.doubleJumpsUsed * 2;
                // Reset count on first burst
                if (burstNumber == 0)
                    bowPlayer.doubleJumpsUsed = 0;
            }

            int totalExtraArrows = extraPerseveranceArrows + justiceExtraArrows;

            // --- Bravery: All arrows converge to a single point in front of the player ---
            if (Variant == ImbuedBowVariant.Bravery)
            {
                ShootBraveryConverging(source, player, position, velocity, type, damage, knockback, burstNumber, totalExtraArrows);
                return false;
            }

            // --- Kindness: Spawn arrows behind self + all nearby allies ---
            if (Variant == ImbuedBowVariant.Kindness)
            {
                // Normal V arrows
                Projectile.NewProjectile(source, position, velocity.RotatedBy(-spreadAngleBase), type, damage, knockback, player.whoAmI);
                Projectile.NewProjectile(source, position, velocity.RotatedBy(spreadAngleBase), type, damage, knockback, player.whoAmI);

                // Extra arrows from traits
                for (int e = 0; e < totalExtraArrows; e++)
                {
                    float extraAngle = MathHelper.ToRadians(Main.rand.NextFloat(-25f, 25f));
                    Projectile.NewProjectile(source, position, velocity.RotatedBy(extraAngle), type, damage, knockback, player.whoAmI);
                }

                // Spawn ally arrows (including self)
                SpawnKindnessAllyArrows(source, player, type, damage, knockback);
                return false;
            }

            // --- Default V pattern for other variants ---
            Projectile.NewProjectile(source, position, velocity.RotatedBy(-spreadAngleBase), type, damage, knockback, player.whoAmI);
            Projectile.NewProjectile(source, position, velocity.RotatedBy(spreadAngleBase), type, damage, knockback, player.whoAmI);

            // Spawn extra arrows with random spread
            for (int e = 0; e < totalExtraArrows; e++)
            {
                float extraAngle = MathHelper.ToRadians(Main.rand.NextFloat(-25f, 25f));
                Projectile.NewProjectile(source, position, velocity.RotatedBy(extraAngle), type, damage, knockback, player.whoAmI);
            }

            return false;
        }

        private void ShootBraveryConverging(EntitySource_ItemUse_WithAmmo source, Player player, Vector2 position, Vector2 velocity, int type, int damage, float knockback, int burstNumber, int extraArrows)
        {
            // All arrows converge at a point ~150 pixels in front of the player
            float convergeDist = 150f;
            Vector2 aimDir = velocity.SafeNormalize(Vector2.UnitX);
            Vector2 convergePoint = player.Center + aimDir * convergeDist;

            int totalArrows = 2 + extraArrows;
            Vector2 perpendicular = new Vector2(-aimDir.Y, aimDir.X);

            // Spread spawn positions in an arc around the player
            float spawnRadius = 40f;
            float totalSpread = MathHelper.ToRadians(60f);
            float startAngle = -totalSpread / 2f;
            float step = totalArrows > 1 ? totalSpread / (totalArrows - 1) : 0f;

            for (int i = 0; i < totalArrows; i++)
            {
                float angle = startAngle + step * i;
                // Offset spawn position around the player perpendicular to aim
                Vector2 spawnOffset = perpendicular * (float)Math.Sin(angle) * spawnRadius;
                Vector2 spawnPos = position + spawnOffset;

                // All aim at the converge point
                Vector2 toTarget = convergePoint - spawnPos;
                Vector2 arrowVelocity = toTarget.SafeNormalize(Vector2.UnitX) * velocity.Length();

                Projectile.NewProjectile(source, spawnPos, arrowVelocity, type, damage, knockback, player.whoAmI);
            }
        }

        private void SpawnKindnessAllyArrows(EntitySource_ItemUse_WithAmmo source, Player player, int type, int damage, float knockback)
        {
            float range = 600f;

            // Find the nearest enemy to aim at
            NPC nearestEnemy = null;
            float closestDist = 1200f;
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (npc.active && !npc.friendly && !npc.dontTakeDamage && npc.lifeMax > 5)
                {
                    float dist = Vector2.Distance(player.Center, npc.Center);
                    if (dist < closestDist)
                    {
                        closestDist = dist;
                        nearestEnemy = npc;
                    }
                }
            }

            if (nearestEnemy == null)
                return;

            // Spawn one arrow behind each nearby ally (NOT yourself) facing the enemy
            for (int i = 0; i < Main.maxPlayers; i++)
            {
                Player ally = Main.player[i];
                if (!ally.active || ally.dead)
                    continue;
                if (ally.whoAmI == player.whoAmI)
                    continue;
                if (Vector2.Distance(player.Center, ally.Center) > range)
                    continue;

                Vector2 toEnemy = (nearestEnemy.Center - ally.Center).SafeNormalize(Vector2.UnitX);
                // Spawn behind the ally (opposite of enemy direction)
                Vector2 spawnPos = ally.Center - toEnemy * 40f;
                Vector2 arrowVelocity = toEnemy * 12f;

                Projectile.NewProjectile(source, spawnPos, arrowVelocity, type, damage, knockback, player.whoAmI);
            }
        }

        public override Color? GetAlpha(Color lightColor)
        {
            return GetTraitColor();
        }

        public override bool PreDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor, ref float rotation, ref float scale, int whoAmI)
        {
            Texture2D texture = Terraria.GameContent.TextureAssets.Item[Item.type].Value;
            Vector2 pos = Item.position - Main.screenPosition + new Vector2(Item.width / 2, Item.height - texture.Height / 2);
            Vector2 origin = texture.Size() * 0.5f;
            spriteBatch.Draw(texture, pos, null, GetTraitColor(), rotation, origin, scale, SpriteEffects.None, 0f);
            return false;
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips)
        {
            Color traitColor = GetTraitColor();
            foreach (TooltipLine line in tooltips)
            {
                if (line.Name == "ItemName")
                    line.OverrideColor = traitColor;
            }
        }

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ModContent.ItemType<RoaringBow>(), 1)
                .AddIngredient(GetSparkType(), 1)
                .AddTile(ModContent.TileType<Tiles.TitanForge>())
                .Register();
        }
    }

    // --- Concrete variants ---

    public class DeterminationBow : ImbuedBowBase
    {
        protected override ImbuedBowVariant Variant => ImbuedBowVariant.Determination;
        protected override int GetSparkType() => ModContent.ItemType<SparkOfDetermination>();
    }

    public class IntegrityBow : ImbuedBowBase
    {
        protected override ImbuedBowVariant Variant => ImbuedBowVariant.Integrity;
        protected override int GetSparkType() => ModContent.ItemType<SparkOfIntegrity>();
    }

    public class PatienceBow : ImbuedBowBase
    {
        protected override ImbuedBowVariant Variant => ImbuedBowVariant.Patience;
        protected override int GetSparkType() => ModContent.ItemType<SparkOfPatience>();
    }

    public class PerseveranceBow : ImbuedBowBase
    {
        protected override ImbuedBowVariant Variant => ImbuedBowVariant.Perseverance;
        protected override int GetSparkType() => ModContent.ItemType<SparkOfPerseverance>();
    }

    public class KindnessBow : ImbuedBowBase
    {
        protected override ImbuedBowVariant Variant => ImbuedBowVariant.Kindness;
        protected override int GetSparkType() => ModContent.ItemType<SparkOfKindness>();
    }

    public class JusticeBow : ImbuedBowBase
    {
        protected override ImbuedBowVariant Variant => ImbuedBowVariant.Justice;
        protected override int GetSparkType() => ModContent.ItemType<SparkOfJustice>();
    }

    public class BraveryBow : ImbuedBowBase
    {
        protected override ImbuedBowVariant Variant => ImbuedBowVariant.Bravery;
        protected override int GetSparkType() => ModContent.ItemType<SparkOfBravery>();
    }
}
