using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using DeterministicChaos.Content.Items.Sparks;
using DeterministicChaos.Content.SoulTraits;
using DeterministicChaos.Content.Tiles;
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
    public abstract class ImbuedEmblemBase : ModItem
    {
        protected abstract SoulTraitType RequiredTrait { get; }
        protected abstract int GetSparkType();

        public override string Texture => "DeterministicChaos/Content/Items/Accessories/EmptyEmblem";

        protected Color GetTraitColor()
        {
            return RequiredTrait switch
            {
                SoulTraitType.Determination => new Color(255, 60, 60),
                SoulTraitType.Integrity => new Color(0, 0, 255),
                SoulTraitType.Patience => new Color(80, 255, 255),
                SoulTraitType.Perseverance => new Color(255, 80, 255),
                SoulTraitType.Kindness => new Color(80, 230, 80),
                SoulTraitType.Justice => new Color(255, 255, 80),
                SoulTraitType.Bravery => new Color(255, 190, 60),
                _ => Color.White
            };
        }

        public override void SetDefaults()
        {
            Item.width = 28;
            Item.height = 28;
            Item.accessory = true;
            Item.rare = ModContent.RarityType<DarkWorldRarity>();
            Item.value = Item.buyPrice(gold: 5);
            Item.defense = 2;
        }

        public override void UpdateAccessory(Player player, bool hideVisual)
        {
            var traitPlayer = player.GetModPlayer<SoulTraitPlayer>();

            if (traitPlayer.CurrentTrait != RequiredTrait)
                return;

            traitPlayer.ArmorInvestment += 5;
            player.statLifeMax2 += 25;

            var emblemPlayer = player.GetModPlayer<ImbuedEmblemPlayer>();
            ApplyEmblemEffect(player, emblemPlayer);
        }

        protected abstract void ApplyEmblemEffect(Player player, ImbuedEmblemPlayer emblemPlayer);

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
            CreateRecipe()
                .AddIngredient(ModContent.ItemType<EmptyEmblem>(), 1)
                .AddIngredient(GetSparkType(), 1)
                .AddTile(ModContent.TileType<TitanForge>())
                .Register();
        }
    }

    public class DeterminationEmblem : ImbuedEmblemBase
    {
        protected override SoulTraitType RequiredTrait => SoulTraitType.Determination;
        protected override int GetSparkType() => ModContent.ItemType<SparkOfDetermination>();

        protected override void ApplyEmblemEffect(Player player, ImbuedEmblemPlayer emblemPlayer)
        {
            player.GetDamage(DamageClass.Generic) += 0.06f;
            emblemPlayer.hasDeterminationEmblem = true;
        }
    }

    public class IntegrityEmblem : ImbuedEmblemBase
    {
        protected override SoulTraitType RequiredTrait => SoulTraitType.Integrity;
        protected override int GetSparkType() => ModContent.ItemType<SparkOfIntegrity>();

        protected override void ApplyEmblemEffect(Player player, ImbuedEmblemPlayer emblemPlayer)
        {
            player.statDefense += 8;
            emblemPlayer.hasIntegrityEmblem = true;
        }
    }

    public class PatienceEmblem : ImbuedEmblemBase
    {
        protected override SoulTraitType RequiredTrait => SoulTraitType.Patience;
        protected override int GetSparkType() => ModContent.ItemType<SparkOfPatience>();

        protected override void ApplyEmblemEffect(Player player, ImbuedEmblemPlayer emblemPlayer)
        {
            emblemPlayer.hasPatienceEmblem = true;
        }
    }

    public class PerseveranceEmblem : ImbuedEmblemBase
    {
        protected override SoulTraitType RequiredTrait => SoulTraitType.Perseverance;
        protected override int GetSparkType() => ModContent.ItemType<SparkOfPerseverance>();

        protected override void ApplyEmblemEffect(Player player, ImbuedEmblemPlayer emblemPlayer)
        {
            player.statManaMax2 += 50;
            emblemPlayer.hasPerseveranceEmblem = true;
        }
    }

    public class KindnessEmblem : ImbuedEmblemBase
    {
        protected override SoulTraitType RequiredTrait => SoulTraitType.Kindness;
        protected override int GetSparkType() => ModContent.ItemType<SparkOfKindness>();

        protected override void ApplyEmblemEffect(Player player, ImbuedEmblemPlayer emblemPlayer)
        {
            emblemPlayer.hasKindnessEmblem = true;
        }
    }

    public class JusticeEmblem : ImbuedEmblemBase
    {
        protected override SoulTraitType RequiredTrait => SoulTraitType.Justice;
        protected override int GetSparkType() => ModContent.ItemType<SparkOfJustice>();

        protected override void ApplyEmblemEffect(Player player, ImbuedEmblemPlayer emblemPlayer)
        {
            emblemPlayer.hasJusticeEmblem = true;
        }
    }

    public class BraveryEmblem : ImbuedEmblemBase
    {
        protected override SoulTraitType RequiredTrait => SoulTraitType.Bravery;
        protected override int GetSparkType() => ModContent.ItemType<SparkOfBravery>();

        protected override void ApplyEmblemEffect(Player player, ImbuedEmblemPlayer emblemPlayer)
        {
            player.statLifeMax2 += 25;
            emblemPlayer.hasBraveryEmblem = true;
        }
    }
}
