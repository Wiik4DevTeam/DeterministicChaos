using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using DeterministicChaos.Content.Projectiles.Friendly;

namespace DeterministicChaos.Content.Items
{
    public class RoaringBullet : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 8;
            Item.height = 8;
            Item.damage = 15;
            Item.knockBack = 2f;
            Item.maxStack = 9999;
            Item.consumable = true;
            Item.shoot = ModContent.ProjectileType<RoaringBulletProjectile>();
            Item.shootSpeed = 5f;
            Item.ammo = AmmoID.Bullet;
            Item.rare = ModContent.RarityType<DarkWorldRarity>();
            Item.value = Item.buyPrice(copper: 15);
            Item.DamageType = DamageClass.Ranged;
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
            CreateRecipe(66)
                .AddIngredient(ModContent.ItemType<DarkFragment>(), 1)
                .AddIngredient(ItemID.MusketBall, 66)
                .AddTile(ModContent.TileType<Tiles.TitanForge>())
                .Register();
        }
    }
}
