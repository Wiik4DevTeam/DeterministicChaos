using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.Items.Armor
{
    // Breastplate for the Roaring armor set
    // Provides defense and stat bonuses
    [AutoloadEquip(EquipType.Body)]
    public class RoaringBreastplate : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 30;
            Item.height = 20;
            Item.value = Item.buyPrice(gold: 10);
            Item.rare = ModContent.RarityType<DarkWorldRarity>();
            Item.defense = 20;
        }

        public override void UpdateEquip(Player player)
        {
            // Life and mana bonuses
            player.statLifeMax2 += 40;
            player.statManaMax2 += 60;
            // Damage reduction
            player.endurance += 0.05f;
            // Knockback immunity
            player.noKnockback = true;
            // Emit bright light
            Lighting.AddLight(player.Center, 1.0f, 1.0f, 1.0f);
        }

        public override bool PreDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor, ref float rotation, ref float scale, int whoAmI)
        {
            // Draw item at full brightness, unaffected by lighting
            Texture2D texture = Terraria.GameContent.TextureAssets.Item[Item.type].Value;
            Vector2 position = Item.position - Main.screenPosition + new Vector2(Item.width / 2, Item.height - texture.Height / 2);
            Vector2 origin = texture.Size() * 0.5f;

            spriteBatch.Draw(texture, position, null, Color.White, rotation, origin, scale, SpriteEffects.None, 0f);
            return false;
        }

        public override void ModifyTooltips(System.Collections.Generic.List<TooltipLine> tooltips)
        {
            Color statGray = new Color(60, 60, 60);
            foreach (TooltipLine line in tooltips)
            {
                // Stats get a grayer color to differentiate from descriptions
                if (line.Name == "Damage" || line.Name == "Speed" || line.Name == "Knockback" || 
                    line.Name == "CritChance" || line.Name == "Defense" || line.Name == "UseMana" ||
                    line.Name == "Consumable" || line.Name == "Material")
                {
                    line.OverrideColor = statGray;
                }
                else
                {
                    line.OverrideColor = Color.Black;
                }
            }
        }

        public override bool PreDrawTooltipLine(DrawableTooltipLine line, ref int yOffset)
        {
            Vector2 position = new Vector2(line.X, line.Y);

            // Draw white shadow outline
            for (int x = -2; x <= 2; x++)
            {
                for (int y = -2; y <= 2; y++)
                {
                    if (x == 0 && y == 0) continue;

                    Terraria.UI.Chat.ChatManager.DrawColorCodedString(
                        Main.spriteBatch,
                        line.Font,
                        line.Text,
                        position + new Vector2(x, y),
                        Color.White,
                        line.Rotation,
                        line.Origin,
                        line.BaseScale
                    );
                }
            }

            // Draw black text on top
            Terraria.UI.Chat.ChatManager.DrawColorCodedString(
                Main.spriteBatch,
                line.Font,
                line.Text,
                position,
                Color.Black,
                line.Rotation,
                line.Origin,
                line.BaseScale
            );

            return false;
        }

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ModContent.ItemType<DarkFragment>(), 20)
                .AddIngredient(ItemID.GoldChainmail)
                .AddTile(ModContent.TileType<Tiles.TitanForge>())
                .Register();
            
            CreateRecipe()
                .AddIngredient(ModContent.ItemType<DarkFragment>(), 20)
                .AddIngredient(ItemID.PlatinumChainmail)
                .AddTile(ModContent.TileType<Tiles.TitanForge>())
                .Register();
        }
    }
}
