using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using DeterministicChaos.Content.Projectiles.Friendly;
using DeterministicChaos.Content.Buffs;

namespace DeterministicChaos.Content.Items
{
    public class RoaringSword : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 40;
            Item.height = 40;
            Item.damage = 53;
            Item.knockBack = 5f;
            Item.useTime = 14;
            Item.useAnimation = 14;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.autoReuse = true;
            Item.rare = ModContent.RarityType<DarkWorldRarity>();
            Item.value = Item.buyPrice(gold: 8);
            Item.UseSound = null;
            Item.shoot = ModContent.ProjectileType<RoaringSwordSwing>();
            Item.shootSpeed = 1f;
            Item.DamageType = DamageClass.Melee;
        }

        public override bool AltFunctionUse(Player player)
        {
            return true;
        }

        public override bool MeleePrefix()
        {
            return true;
        }

        public override bool CanUseItem(Player player)
        {
            // Check if any lunge-related projectile exists
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                if (Main.projectile[i].active && Main.projectile[i].owner == player.whoAmI)
                {
                    int projType = Main.projectile[i].type;
                    if (projType == ModContent.ProjectileType<RoaringSwordLungeCharge>() ||
                        projType == ModContent.ProjectileType<RoaringSwordLunge>() ||
                        projType == ModContent.ProjectileType<RoaringSwordChainLunge>())
                    {
                        return false;
                    }
                }
            }
            
            // Check lunge cooldown
            if (player.GetModPlayer<RoaringSwordPlayer>().lungeCooldown > 0)
            {
                if (player.altFunctionUse == 2)
                    return false;
            }
            
            if (player.altFunctionUse == 2)
            {
                Item.useTime = 10;
                Item.useAnimation = 10;
                Item.shoot = ModContent.ProjectileType<RoaringSwordLungeCharge>();
                Item.UseSound = null;
                Item.channel = true;
                Item.autoReuse = false;
            }
            else
            {
                Item.useTime = 14;
                Item.useAnimation = 14;
                Item.shoot = ModContent.ProjectileType<RoaringSwordSwing>();
                Item.UseSound = SoundID.Item1;
                Item.channel = false;
                Item.autoReuse = true;
            }
            return base.CanUseItem(player);
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            // Only spawn projectiles on the owning client
            if (player.whoAmI != Main.myPlayer)
                return false;
            
            if (player.altFunctionUse == 2)
            {
                Projectile.NewProjectile(source, player.Center, velocity, ModContent.ProjectileType<RoaringSwordLungeCharge>(), damage, knockback, player.whoAmI);
            }
            else
            {
                int combo = player.GetModPlayer<RoaringSwordPlayer>().swingCombo;
                float swingDirection = (combo % 2 == 0) ? 1f : -1f;
                player.GetModPlayer<RoaringSwordPlayer>().swingCombo++;
                
                // Calculate aim angle and pass it in ai[1] for multiplayer sync
                Vector2 toMouse = (Main.MouseWorld - player.Center).SafeNormalize(Vector2.UnitX);
                float aimAngle = toMouse.ToRotation();
                
                Projectile.NewProjectile(source, player.Center, Vector2.Zero, ModContent.ProjectileType<RoaringSwordSwing>(), damage, knockback, player.whoAmI, swingDirection, aimAngle);
            }
            return false;
        }

        public override void HoldItem(Player player)
        {
            Lighting.AddLight(player.Center, 0.6f, 0.2f, 0.6f);
            
            // Check if any lunge-related projectile exists
            bool lungeActive = false;
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                if (Main.projectile[i].active && Main.projectile[i].owner == player.whoAmI)
                {
                    int projType = Main.projectile[i].type;
                    if (projType == ModContent.ProjectileType<RoaringSwordLungeCharge>() ||
                        projType == ModContent.ProjectileType<RoaringSwordLunge>() ||
                        projType == ModContent.ProjectileType<RoaringSwordChainLunge>())
                    {
                        lungeActive = true;
                        break;
                    }
                }
            }
            
            // Start charging if right-click is held and no lunge active and cooldown is ready
            if (Main.mouseRight && player.itemAnimation == 0 && player.whoAmI == Main.myPlayer && 
                !lungeActive && player.GetModPlayer<RoaringSwordPlayer>().lungeCooldown <= 0)
            {
                Vector2 velocity = (Main.MouseWorld - player.Center).SafeNormalize(Vector2.UnitX);
                Projectile.NewProjectile(
                    player.GetSource_ItemUse(Item),
                    player.Center,
                    velocity,
                    ModContent.ProjectileType<RoaringSwordLungeCharge>(),
                    player.GetWeaponDamage(Item),
                    player.GetWeaponKnockback(Item, Item.knockBack),
                    player.whoAmI
                );
            }
        }

        public override bool PreDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor, ref float rotation, ref float scale, int whoAmI)
        {
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
            CreateRecipe()
                .AddIngredient(ModContent.ItemType<DarkFragment>(), 15)
                .AddIngredient(ModContent.ItemType<BaseballBat>())
                .AddTile(ModContent.TileType<Tiles.TitanForge>())
                .Register();
        }
    }
}
