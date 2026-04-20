using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using DeterministicChaos.Content.Buffs;
using DeterministicChaos.Content.Items.Sparks;
using DeterministicChaos.Content.Projectiles.Friendly;
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
    public enum ImbuedSummonVariant
    {
        Determination,
        Integrity,
        Patience,
        Perseverance,
        Kindness,
        Justice,
        Bravery
    }

    public abstract class ImbuedSummonBase : ModItem
    {
        protected abstract ImbuedSummonVariant Variant { get; }
        protected abstract int GetSparkType();
        protected abstract int GetBuffType();
        protected abstract int GetProjectileType();

        public override string Texture => "DeterministicChaos/Content/Items/Weapons/RoaringSummon";

        private SoulTraitType GetSoulTraitType()
        {
            return Variant switch
            {
                ImbuedSummonVariant.Determination => SoulTraitType.Determination,
                ImbuedSummonVariant.Integrity => SoulTraitType.Integrity,
                ImbuedSummonVariant.Patience => SoulTraitType.Patience,
                ImbuedSummonVariant.Perseverance => SoulTraitType.Perseverance,
                ImbuedSummonVariant.Kindness => SoulTraitType.Kindness,
                ImbuedSummonVariant.Justice => SoulTraitType.Justice,
                ImbuedSummonVariant.Bravery => SoulTraitType.Bravery,
                _ => SoulTraitType.None
            };
        }

        protected Color GetTraitColor()
        {
            return Variant switch
            {
                ImbuedSummonVariant.Determination => new Color(255, 60, 60),
                ImbuedSummonVariant.Integrity => new Color(0, 0, 255),
                ImbuedSummonVariant.Patience => new Color(80, 255, 255),
                ImbuedSummonVariant.Perseverance => new Color(255, 80, 255),
                ImbuedSummonVariant.Kindness => new Color(80, 230, 80),
                ImbuedSummonVariant.Justice => new Color(255, 255, 80),
                ImbuedSummonVariant.Bravery => new Color(255, 190, 60),
                _ => Color.White
            };
        }

        public override void SetStaticDefaults()
        {
            ItemID.Sets.GamepadWholeScreenUseRange[Item.type] = true;
            ItemID.Sets.LockOnIgnoresCollision[Item.type] = true;
            SoulTraitGlobalItem.RegisterWeaponInvestment(Type, 6, GetSoulTraitType());
        }

        public override void SetDefaults()
        {
            Item.width = 28;
            Item.height = 28;
            Item.damage = RoaringSummon.BaseDamage;
            Item.knockBack = RoaringSummon.BaseKnockback;
            Item.mana = 10;
            Item.useTime = 36;
            Item.useAnimation = 36;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.noMelee = true;
            Item.DamageType = DamageClass.Summon;
            Item.buffType = GetBuffType();
            Item.shoot = GetProjectileType();
            Item.rare = ModContent.RarityType<DarkWorldRarity>();
            Item.value = Item.buyPrice(gold: 5);
            Item.UseSound = SoundID.Item44;

            if (Variant == ImbuedSummonVariant.Perseverance)
                Item.DamageType = DamageClass.Magic;

            if (Variant == ImbuedSummonVariant.Bravery)
                Item.DamageType = DamageClass.Melee;
        }

        public override void ModifyWeaponDamage(Player player, ref StatModifier damage)
        {
            if (Variant == ImbuedSummonVariant.Determination)
                damage *= 1.08f;
        }

        public override void HoldItem(Player player)
        {
            Lighting.AddLight(player.Center, 0.9f, 0.9f, 0.9f);
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            player.AddBuff(Item.buffType, 2);

            int projIndex = Projectile.NewProjectile(source, player.Center, Vector2.Zero, type, damage, knockback, player.whoAmI);

            if (projIndex >= 0 && projIndex < Main.maxProjectiles)
            {
                Main.projectile[projIndex].originalDamage = Item.damage;
            }

            return false;
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
                .AddIngredient(ModContent.ItemType<RoaringSummon>(), 1)
                .AddIngredient(GetSparkType(), 1)
                .AddTile(ModContent.TileType<Tiles.TitanForge>())
                .Register();
        }
    }

    // --- Concrete variants ---

    public class DeterminationSummon : ImbuedSummonBase
    {
        protected override ImbuedSummonVariant Variant => ImbuedSummonVariant.Determination;
        protected override int GetSparkType() => ModContent.ItemType<SparkOfDetermination>();
        protected override int GetBuffType() => ModContent.BuffType<DeterminationSummonBuff>();
        protected override int GetProjectileType() => ModContent.ProjectileType<DeterminationSummonProjectile>();
    }

    public class IntegritySummon : ImbuedSummonBase
    {
        protected override ImbuedSummonVariant Variant => ImbuedSummonVariant.Integrity;
        protected override int GetSparkType() => ModContent.ItemType<SparkOfIntegrity>();
        protected override int GetBuffType() => ModContent.BuffType<IntegritySummonBuff>();
        protected override int GetProjectileType() => ModContent.ProjectileType<IntegritySummonProjectile>();
    }

    public class PatienceSummon : ImbuedSummonBase
    {
        protected override ImbuedSummonVariant Variant => ImbuedSummonVariant.Patience;
        protected override int GetSparkType() => ModContent.ItemType<SparkOfPatience>();
        protected override int GetBuffType() => ModContent.BuffType<PatienceSummonBuff>();
        protected override int GetProjectileType() => ModContent.ProjectileType<PatienceSummonProjectile>();
    }

    public class PerseveranceSummon : ImbuedSummonBase
    {
        protected override ImbuedSummonVariant Variant => ImbuedSummonVariant.Perseverance;
        protected override int GetSparkType() => ModContent.ItemType<SparkOfPerseverance>();
        protected override int GetBuffType() => ModContent.BuffType<PerseveranceSummonBuff>();
        protected override int GetProjectileType() => ModContent.ProjectileType<PerseveranceSummonProjectile>();
    }

    public class KindnessSummon : ImbuedSummonBase
    {
        protected override ImbuedSummonVariant Variant => ImbuedSummonVariant.Kindness;
        protected override int GetSparkType() => ModContent.ItemType<SparkOfKindness>();
        protected override int GetBuffType() => ModContent.BuffType<KindnessSummonBuff>();
        protected override int GetProjectileType() => ModContent.ProjectileType<KindnessSummonProjectile>();
    }

    public class JusticeSummon : ImbuedSummonBase
    {
        protected override ImbuedSummonVariant Variant => ImbuedSummonVariant.Justice;
        protected override int GetSparkType() => ModContent.ItemType<SparkOfJustice>();
        protected override int GetBuffType() => ModContent.BuffType<JusticeSummonBuff>();
        protected override int GetProjectileType() => ModContent.ProjectileType<JusticeSummonProjectile>();
    }

    public class BraverySummon : ImbuedSummonBase
    {
        protected override ImbuedSummonVariant Variant => ImbuedSummonVariant.Bravery;
        protected override int GetSparkType() => ModContent.ItemType<SparkOfBravery>();
        protected override int GetBuffType() => ModContent.BuffType<BraverySummonBuff>();
        protected override int GetProjectileType() => ModContent.ProjectileType<BraverySummonProjectile>();
    }
}
