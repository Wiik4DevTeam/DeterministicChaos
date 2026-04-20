using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using DeterministicChaos.Content.Projectiles.Friendly;
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

namespace DeterministicChaos.Content.Items.Weapons
{
    public class Leyline : ModItem
    {
        public override void SetStaticDefaults()
        {
            Item.staff[Type] = true;
        }

        public override void SetDefaults()
        {
            Item.width = 36;
            Item.height = 36;
            Item.damage = 39;
            Item.DamageType = DamageClass.SummonMeleeSpeed;
            Item.knockBack = 1f;
            Item.useTime = 22;
            Item.useAnimation = 22;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.autoReuse = false;
            Item.channel = true;
            Item.UseSound = SoundID.Item153;
            Item.shoot = ModContent.ProjectileType<LeylineProjectile>();
            Item.shootSpeed = 1f;
            Item.rare = ModContent.RarityType<TitanRarity>();
            Item.value = Item.buyPrice(gold: 80);
        }

        public override bool CanUseItem(Player player)
        {
            return player.ownedProjectileCounts[Item.shoot] <= 0;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            Vector2 aim = Main.MouseWorld - player.MountedCenter;
            if (aim.LengthSquared() < 0.001f)
                aim = Vector2.UnitX * player.direction;
            aim.Normalize();

            Projectile.NewProjectile(
                source,
                player.MountedCenter,
                aim,
                type,
                damage,
                knockback,
                player.whoAmI,
                Main.MouseWorld.X,
                Main.MouseWorld.Y);

            return false;
        }

        public override void AddRecipes() { }
    }
}
