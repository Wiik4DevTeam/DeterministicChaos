using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using DeterministicChaos.Content.Projectiles.Friendly;

namespace DeterministicChaos.Content.Items
{
    // Post-Titan arrow ammo. Sticks to the first target or surface it touches.
    // Impact deals 20% of its damage; after 1 second the arrow explodes for the
    // remaining 80%, dealing AoE damage to nearby enemies.
    public class TitansArrow : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 10;
            Item.height = 28;
            Item.damage = 45;
            Item.knockBack = 3f;
            Item.maxStack = 9999;
            Item.consumable = true;
            Item.ammo = AmmoID.Arrow;
            Item.shoot = ModContent.ProjectileType<TitansArrowProjectile>();
            Item.shootSpeed = 10f;
            Item.rare = ModContent.RarityType<TitanRarity>();
            Item.value = Item.buyPrice(silver: 50);
            Item.DamageType = DamageClass.Ranged;
        }

        public override void AddRecipes() { }
    }
}
