using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
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
    public enum ImbuedClarityVariant
    {
        None,
        Determination,
        Integrity,
        Patience,
        Perseverance,
        Kindness,
        Justice,
        Bravery
    }

    public abstract class ImbuedClarityBase : ModItem
    {
        protected abstract ImbuedClarityVariant Variant { get; }
        protected abstract int GetSparkType();

        public override string Texture => "DeterministicChaos/Content/Items/Weapons/RoaringTome";

        private SoulTraitType GetSoulTraitType()
        {
            return Variant switch
            {
                ImbuedClarityVariant.Determination => SoulTraitType.Determination,
                ImbuedClarityVariant.Integrity => SoulTraitType.Integrity,
                ImbuedClarityVariant.Patience => SoulTraitType.Patience,
                ImbuedClarityVariant.Perseverance => SoulTraitType.Perseverance,
                ImbuedClarityVariant.Kindness => SoulTraitType.Kindness,
                ImbuedClarityVariant.Justice => SoulTraitType.Justice,
                ImbuedClarityVariant.Bravery => SoulTraitType.Bravery,
                _ => SoulTraitType.None
            };
        }

        protected Color GetTraitColor()
        {
            return Variant switch
            {
                ImbuedClarityVariant.Determination => new Color(255, 60, 60),
                ImbuedClarityVariant.Integrity => new Color(0, 0, 255),
                ImbuedClarityVariant.Patience => new Color(80, 255, 255),
                ImbuedClarityVariant.Perseverance => new Color(255, 80, 255),
                ImbuedClarityVariant.Kindness => new Color(80, 230, 80),
                ImbuedClarityVariant.Justice => new Color(255, 255, 80),
                ImbuedClarityVariant.Bravery => new Color(255, 190, 60),
                _ => Color.White
            };
        }

        public override void SetStaticDefaults()
        {
            ItemID.Sets.ItemsThatAllowRepeatedRightClick[Type] = true;
            SoulTraitGlobalItem.RegisterWeaponInvestment(Type, 6, GetSoulTraitType());
        }

        public override void SetDefaults()
        {
            Item.width = 28;
            Item.height = 32;
            Item.damage = 55;
            Item.knockBack = 2f;
            Item.mana = 0;
            Item.useTime = 12;
            Item.useAnimation = 12;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.autoReuse = true;
            Item.rare = ModContent.RarityType<DarkWorldRarity>();
            Item.value = Item.buyPrice(gold: 8);
            Item.UseSound = SoundID.Item8;
            Item.shoot = ModContent.ProjectileType<RoaringTomeStarProjectile>();
            Item.shootSpeed = 8f;
            Item.DamageType = DamageClass.Magic;
        }

        public override bool AltFunctionUse(Player player) => true;

        public override void HoldItem(Player player)
        {
            var tomePlayer = player.GetModPlayer<RoaringTomePlayer>();
            tomePlayer.isHoldingClarity = true;
            tomePlayer.imbuedClarityVariant = Variant;

            Vector3 light = GetTraitColor().ToVector3() * 0.6f;
            Lighting.AddLight(player.Center, light);

            if (Main.mouseRight && Main.mouseRightRelease)
            {
                player.noBuilding = true;
                player.tileInteractionHappened = true;
            }
        }

        public override bool CanUseItem(Player player)
        {
            if (player.altFunctionUse == 2)
            {
                Item.mana = player.statManaMax2;
                Item.useTime = 45;
                Item.useAnimation = 45;
                Item.UseSound = SoundID.Item117;
            }
            else
            {
                Item.mana = 0;
                Item.useTime = 12;
                Item.useAnimation = 12;
                Item.UseSound = SoundID.Item8;
            }
            return base.CanUseItem(player);
        }

        public override float UseSpeedMultiplier(Player player)
        {
            // Perseverance: 50% increased fire rate (for stars only, but alt fire useTime is set in CanUseItem)
            if (Variant == ImbuedClarityVariant.Perseverance && player.altFunctionUse != 2)
                return 1.5f;
            return 1f;
        }

        public override void ModifyWeaponCrit(Player player, ref float crit)
        {
            if (Variant == ImbuedClarityVariant.Determination)
                crit += 8;
        }

        public override void ModifyWeaponDamage(Player player, ref StatModifier damage)
        {
            // Justice: base 35% damage penalty to counteract guaranteed crit + hypercrit conversion
            if (Variant == ImbuedClarityVariant.Justice)
                damage *= 0.65f;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            var tomePlayer = player.GetModPlayer<RoaringTomePlayer>();

            if (player.altFunctionUse == 2)
            {
                Vector2 slowVelocity = velocity.SafeNormalize(Vector2.Zero) * 3f;
                Projectile.NewProjectile(source, position, slowVelocity, ModContent.ProjectileType<RoaringTomeBigProjectile>(), damage * 10, knockback * 2, player.whoAmI);
            }
            else
            {
                Projectile.NewProjectile(source, position, velocity, ModContent.ProjectileType<RoaringTomeStarProjectile>(), (int)(damage / 6), knockback, player.whoAmI);
            }
            return false;
        }

        public override bool MeleePrefix() => false;

        public override Color? GetAlpha(Color lightColor) => GetTraitColor();

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
                .AddIngredient(ModContent.ItemType<RoaringTome>(), 1)
                .AddIngredient(GetSparkType(), 1)
                .AddTile(ModContent.TileType<Tiles.TitanForge>())
                .Register();
        }
    }

    // --- Concrete variants ---

    public class DeterminationClarity : ImbuedClarityBase
    {
        protected override ImbuedClarityVariant Variant => ImbuedClarityVariant.Determination;
        protected override int GetSparkType() => ModContent.ItemType<SparkOfDetermination>();
    }

    public class IntegrityClarity : ImbuedClarityBase
    {
        protected override ImbuedClarityVariant Variant => ImbuedClarityVariant.Integrity;
        protected override int GetSparkType() => ModContent.ItemType<SparkOfIntegrity>();
    }

    public class PatienceClarity : ImbuedClarityBase
    {
        protected override ImbuedClarityVariant Variant => ImbuedClarityVariant.Patience;
        protected override int GetSparkType() => ModContent.ItemType<SparkOfPatience>();
    }

    public class PerseveranceClarity : ImbuedClarityBase
    {
        protected override ImbuedClarityVariant Variant => ImbuedClarityVariant.Perseverance;
        protected override int GetSparkType() => ModContent.ItemType<SparkOfPerseverance>();
    }

    public class KindnessClarity : ImbuedClarityBase
    {
        protected override ImbuedClarityVariant Variant => ImbuedClarityVariant.Kindness;
        protected override int GetSparkType() => ModContent.ItemType<SparkOfKindness>();
    }

    public class JusticeClarity : ImbuedClarityBase
    {
        protected override ImbuedClarityVariant Variant => ImbuedClarityVariant.Justice;
        protected override int GetSparkType() => ModContent.ItemType<SparkOfJustice>();
    }

    public class BraveryClarity : ImbuedClarityBase
    {
        protected override ImbuedClarityVariant Variant => ImbuedClarityVariant.Bravery;
        protected override int GetSparkType() => ModContent.ItemType<SparkOfBravery>();
    }
}
