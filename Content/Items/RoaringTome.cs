using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using DeterministicChaos.Content.Projectiles.Friendly;

namespace DeterministicChaos.Content.Items
{
    public class RoaringTome : ModItem
    {
        public override void SetStaticDefaults()
        {
            // Make right click take priority over tile interactions like gravestones
            ItemID.Sets.ItemsThatAllowRepeatedRightClick[Type] = true;
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
            Item.value = Item.buyPrice(gold: 5);
            Item.UseSound = SoundID.Item8;
            Item.shoot = ModContent.ProjectileType<RoaringTomeStarProjectile>();
            Item.shootSpeed = 8f;
            Item.DamageType = DamageClass.Magic;
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

        public override bool AltFunctionUse(Player player)
        {
            return true;
        }

        public override void HoldItem(Player player)
        {
            // Emit bright light when held
            Lighting.AddLight(player.Center, 0.9f, 0.9f, 0.9f);
            
            // Prevent right-click tile interaction when holding this item
            // This makes alt-fire take priority over things like gravestones
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
                // Normal fire, stars, no mana
                Item.mana = 0;
                Item.useTime = 12;
                Item.useAnimation = 12;
                Item.UseSound = SoundID.Item8;
            }
            return base.CanUseItem(player);
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            if (player.altFunctionUse == 2)
            {
                // Alt fire, spawn big slow projectile with high damage
                Vector2 slowVelocity = velocity.SafeNormalize(Vector2.Zero) * 3f;
                Projectile.NewProjectile(source, position, slowVelocity, ModContent.ProjectileType<RoaringTomeBigProjectile>(), damage * 10, knockback * 2, player.whoAmI);
            }
            else
            {
                // Normal fire, spawn star projectile
                Projectile.NewProjectile(source, position, velocity, ModContent.ProjectileType<RoaringTomeStarProjectile>(), (int)(damage/6), knockback, player.whoAmI);
            }
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
            CreateRecipe()
                .AddIngredient(ModContent.ItemType<DarkFragment>(), 15)
                .AddIngredient(ItemID.Book)
                .AddTile(ModContent.TileType<Tiles.TitanForge>())
                .Register();
        }
    }
}
