using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using DeterministicChaos.Content.Projectiles.Friendly;

namespace DeterministicChaos.Content.Items
{
    public class ShatteredGlass : ModItem
    {
        public override void SetStaticDefaults()
        {
            Item.staff[Type] = true;
        }

        public override void SetDefaults()
        {
            Item.width = 34;
            Item.height = 34;
            Item.damage = 92;
            Item.DamageType = DamageClass.Magic;
            Item.mana = 11;
            Item.knockBack = 2.5f;
            Item.useTime = 16;
            Item.useAnimation = 16;
            Item.autoReuse = true;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.noUseGraphic = false;
            Item.UseSound = SoundID.Item72;
            Item.shoot = ModContent.ProjectileType<DefaultBeam>();
            Item.shootSpeed = 1f;
            Item.rare = ModContent.RarityType<TitanRarity>();
            Item.value = Item.buyPrice(gold: 80);
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            Vector2 aim = (Main.MouseWorld - player.MountedCenter).SafeNormalize(Vector2.UnitX * player.direction);
            Projectile.NewProjectile(source, player.MountedCenter, aim, ModContent.ProjectileType<DefaultBeam>(), damage, knockback, player.whoAmI);
            return false;
        }

        public override void AddRecipes() { }
    }
}
