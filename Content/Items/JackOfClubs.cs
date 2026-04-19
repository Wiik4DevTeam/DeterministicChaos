using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using DeterministicChaos.Content.Projectiles.Friendly;

namespace DeterministicChaos.Content.Items
{
    public class JackOfClubs : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 40;
            Item.height = 40;
            Item.damage = 120;
            Item.DamageType = DamageClass.Melee;
            Item.knockBack = 9f;
            Item.useTime = 65;
            Item.useAnimation = 65;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.channel = false;
            Item.autoReuse = true;
            Item.rare = ItemRarityID.Orange;
            Item.value = Item.buyPrice(gold: 3);
            Item.shoot = ModContent.ProjectileType<JackOfClubsProjectile>();
            Item.shootSpeed = 0f;
        }

        public override bool CanUseItem(Player player)
        {
            // Only one swing at a time
            return player.ownedProjectileCounts[ModContent.ProjectileType<JackOfClubsProjectile>()] == 0;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            Projectile.NewProjectile(source, player.Center, Vector2.Zero, type, damage, knockback, player.whoAmI, 0f, 0f, player.direction);
            return false;
        }
    }
}
