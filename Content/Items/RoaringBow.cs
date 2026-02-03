using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.Items
{
    // Bow weapon for the Roaring Knight fight
    // Fires two arrows in a V pattern
    public class RoaringBow : ModItem
    {
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
        }

        public override void HoldItem(Player player)
        {
            // Emit bright light when held
            Lighting.AddLight(player.Center, 0.9f, 0.9f, 0.9f);
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

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            int shotsRemaining = (int)((player.itemAnimation - 1) / (float)Item.useTime);
            int burstNumber = 2 - System.Math.Clamp(shotsRemaining, 0, 2);

            float baseSpread = 20f;
            float spreadDivisor = (float)System.Math.Pow(2, burstNumber);
            float spreadAngle = MathHelper.ToRadians(baseSpread / spreadDivisor);

            // Arrow shooting slightly upward
            Vector2 upVelocity = velocity.RotatedBy(-spreadAngle);
            Projectile.NewProjectile(source, position, upVelocity, type, damage, knockback, player.whoAmI);

            // Arrow shooting slightly downward
            Vector2 downVelocity = velocity.RotatedBy(spreadAngle);
            Projectile.NewProjectile(source, position, downVelocity, type, damage, knockback, player.whoAmI);

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
                .AddIngredient(ModContent.ItemType<DarkFragment>(), 15)
                .AddIngredient(ItemID.GoldBow)
                .AddTile(ModContent.TileType<Tiles.TitanForge>())
                .Register();
            
            CreateRecipe()
                .AddIngredient(ModContent.ItemType<DarkFragment>(), 15)
                .AddIngredient(ItemID.PlatinumBow)
                .AddTile(ModContent.TileType<Tiles.TitanForge>())
                .Register();
        }
    }
}
