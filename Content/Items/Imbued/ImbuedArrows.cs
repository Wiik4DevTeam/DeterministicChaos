using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using DeterministicChaos.Content.Items.Sparks;
using DeterministicChaos.Content.Projectiles.Friendly;
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
    public abstract class ImbuedArrowBase : ModItem
    {
        protected abstract ImbuedArrowVariant Variant { get; }
        protected abstract int GetSparkType();
        protected abstract int GetProjectileType();

        public override string Texture => "DeterministicChaos/Content/Items/Materials/RoaringArrow";

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

        public override void SetDefaults()
        {
            Item.width = 14;
            Item.height = 32;
            Item.damage = 20;
            Item.knockBack = 2f;
            Item.maxStack = 9999;
            Item.consumable = true;
            Item.ammo = AmmoID.Arrow;
            Item.shoot = GetProjectileType();
            Item.shootSpeed = 1f;
            Item.rare = ModContent.RarityType<DarkWorldRarity>();
            Item.value = Item.buyPrice(copper: 30);
            Item.DamageType = DamageClass.Ranged;
        }

        public override Color? GetAlpha(Color lightColor)
        {
            return GetTraitColor();
        }

        public override bool PreDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor, ref float rotation, ref float scale, int whoAmI)
        {
            Texture2D texture = Terraria.GameContent.TextureAssets.Item[Item.type].Value;
            Vector2 position = Item.position - Main.screenPosition + new Vector2(Item.width / 2, Item.height - texture.Height / 2);
            Vector2 origin = texture.Size() * 0.5f;

            spriteBatch.Draw(texture, position, null, GetTraitColor(), rotation, origin, scale, SpriteEffects.None, 0f);
            return false;
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips)
        {
            Color traitColor = GetTraitColor();
            foreach (TooltipLine line in tooltips)
            {
                if (line.Name == "ItemName")
                {
                    line.OverrideColor = traitColor;
                }
            }
        }

        public override void AddRecipes()
        {
            CreateRecipe(66)
                .AddIngredient(ModContent.ItemType<RoaringArrow>(), 66)
                .AddIngredient(GetSparkType(), 1)
                .AddTile(ModContent.TileType<Tiles.TitanForge>())
                .Register();
        }
    }

    public class DeterminationArrow : ImbuedArrowBase
    {
        protected override ImbuedArrowVariant Variant => ImbuedArrowVariant.Determination;
        protected override int GetSparkType() => ModContent.ItemType<SparkOfDetermination>();
        protected override int GetProjectileType() => ModContent.ProjectileType<DeterminationArrowProjectile>();
    }

    public class IntegrityArrow : ImbuedArrowBase
    {
        protected override ImbuedArrowVariant Variant => ImbuedArrowVariant.Integrity;
        protected override int GetSparkType() => ModContent.ItemType<SparkOfIntegrity>();
        protected override int GetProjectileType() => ModContent.ProjectileType<IntegrityArrowProjectile>();
    }

    public class PatienceArrow : ImbuedArrowBase
    {
        protected override ImbuedArrowVariant Variant => ImbuedArrowVariant.Patience;
        protected override int GetSparkType() => ModContent.ItemType<SparkOfPatience>();
        protected override int GetProjectileType() => ModContent.ProjectileType<PatienceArrowProjectile>();
    }

    public class PerseveranceArrow : ImbuedArrowBase
    {
        protected override ImbuedArrowVariant Variant => ImbuedArrowVariant.Perseverance;
        protected override int GetSparkType() => ModContent.ItemType<SparkOfPerseverance>();
        protected override int GetProjectileType() => ModContent.ProjectileType<PerseveranceArrowProjectile>();
    }

    public class KindnessArrow : ImbuedArrowBase
    {
        protected override ImbuedArrowVariant Variant => ImbuedArrowVariant.Kindness;
        protected override int GetSparkType() => ModContent.ItemType<SparkOfKindness>();
        protected override int GetProjectileType() => ModContent.ProjectileType<KindnessArrowProjectile>();
    }

    public class JusticeArrow : ImbuedArrowBase
    {
        protected override ImbuedArrowVariant Variant => ImbuedArrowVariant.Justice;
        protected override int GetSparkType() => ModContent.ItemType<SparkOfJustice>();
        protected override int GetProjectileType() => ModContent.ProjectileType<JusticeArrowProjectile>();
    }

    public class BraveryArrow : ImbuedArrowBase
    {
        protected override ImbuedArrowVariant Variant => ImbuedArrowVariant.Bravery;
        protected override int GetSparkType() => ModContent.ItemType<SparkOfBravery>();
        protected override int GetProjectileType() => ModContent.ProjectileType<BraveryArrowProjectile>();
    }
}
