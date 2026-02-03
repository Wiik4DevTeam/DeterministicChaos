using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using DeterministicChaos.Content.Projectiles.Friendly;

namespace DeterministicChaos.Content.Items
{
    public class BaseballBat : ModItem
    {
        private int swingCombo = 0;
        
        public override void SetDefaults()
        {
            Item.width = 40;
            Item.height = 40;
            Item.damage = 16;
            Item.knockBack = 42f;
            Item.useTime = 22;
            Item.useAnimation = 22;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.autoReuse = true;
            Item.rare = ItemRarityID.Blue;
            Item.value = Item.buyPrice(gold: 1);
            Item.UseSound = null;
            Item.shoot = ModContent.ProjectileType<BaseballBatSwing>();
            Item.shootSpeed = 1f;
            Item.DamageType = DamageClass.Melee;
        }

        public override bool MeleePrefix()
        {
            return true;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            if (player.whoAmI != Main.myPlayer)
                return false;
            
            float swingDirection = (swingCombo % 2 == 0) ? 1f : -1f;
            swingCombo++;
            
            Projectile.NewProjectile(source, player.Center, Vector2.Zero, ModContent.ProjectileType<BaseballBatSwing>(), damage, knockback, player.whoAmI, swingDirection);
            return false;
        }

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddRecipeGroup(RecipeGroupID.Wood, 15)
                .AddIngredient(ItemID.GoldBar, 5)
                .AddTile(TileID.Anvils)
                .Register();
            
            CreateRecipe()
                .AddRecipeGroup(RecipeGroupID.Wood, 15)
                .AddIngredient(ItemID.PlatinumBar, 5)
                .AddTile(TileID.Anvils)
                .Register();
        }
    }
}
