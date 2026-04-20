using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
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

namespace DeterministicChaos.Content.Items.Sparks
{
    public class SparkOfPatience : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 16;
            Item.height = 16;
            Item.maxStack = 999;
            Item.value = Item.sellPrice(silver: 25);
            Item.rare = ModContent.RarityType<DarkWorldRarity>();
            Item.material = true;
        }

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ModContent.ItemType<SparkOfDetermination>(), 5)
                .AddIngredient(ModContent.ItemType<Titansblood>(), 1)
                .AddTile(ModContent.TileType<Tiles.TitanForge>())
                .Register();
            CreateRecipe()
                .AddIngredient(ModContent.ItemType<SparkOfIntegrity>(), 5)
                .AddIngredient(ModContent.ItemType<Titansblood>(), 1)
                .AddTile(ModContent.TileType<Tiles.TitanForge>())
                .Register();
            CreateRecipe()
                .AddIngredient(ModContent.ItemType<SparkOfPerseverance>(), 5)
                .AddIngredient(ModContent.ItemType<Titansblood>(), 1)
                .AddTile(ModContent.TileType<Tiles.TitanForge>())
                .Register();
            CreateRecipe()
                .AddIngredient(ModContent.ItemType<SparkOfKindness>(), 5)
                .AddIngredient(ModContent.ItemType<Titansblood>(), 1)
                .AddTile(ModContent.TileType<Tiles.TitanForge>())
                .Register();
            CreateRecipe()
                .AddIngredient(ModContent.ItemType<SparkOfJustice>(), 5)
                .AddIngredient(ModContent.ItemType<Titansblood>(), 1)
                .AddTile(ModContent.TileType<Tiles.TitanForge>())
                .Register();
            CreateRecipe()
                .AddIngredient(ModContent.ItemType<SparkOfBravery>(), 5)
                .AddIngredient(ModContent.ItemType<Titansblood>(), 1)
                .AddTile(ModContent.TileType<Tiles.TitanForge>())
                .Register();
        }
    }
}
