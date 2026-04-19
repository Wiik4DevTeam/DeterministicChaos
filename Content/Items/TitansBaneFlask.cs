using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using DeterministicChaos.Content.Buffs;

namespace DeterministicChaos.Content.Items
{
    // Flask that can be loaded into the Cooking Pot OR drunk directly.
    // Drinking it grants the Titansbane Imbue buff (20 min), making melee attacks
    // inflict Titansbane on hit. Compatible with the Cooking Pot as a flask ingredient.
    public class TitansBaneFlask : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 20;
            Item.height = 26;
            Item.maxStack = Item.CommonMaxStack;
            Item.consumable = true;
            Item.rare = ModContent.RarityType<TitanRarity>();
            Item.value = Item.buyPrice(gold: 5);
            Item.useStyle = ItemUseStyleID.DrinkLiquid;
            Item.useAnimation = 17;
            Item.useTime = 17;
            Item.UseSound = SoundID.Item3;
            Item.buffType = ModContent.BuffType<TitansBaneImbue>();
            Item.buffTime = 20 * 60 * 60; // 20 minutes, same as vanilla flasks
        }

        public override bool? UseItem(Player player)
        {
            player.AddBuff(Item.buffType, Item.buffTime);
            return true;
        }

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ModContent.ItemType<Titansblood>(), 2)
                .AddIngredient(ItemID.BottledWater)
                .AddTile(TileID.ImbuingStation)
                .Register();
        }
    }
}
