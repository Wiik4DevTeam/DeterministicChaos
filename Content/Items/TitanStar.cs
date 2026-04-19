using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using DeterministicChaos.Content.Buffs;
using DeterministicChaos.Content.Projectiles.Friendly;

namespace DeterministicChaos.Content.Items
{
    public class TitanStar : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 20;
            Item.height = 20;
            Item.value = Item.buyPrice(gold: 18);
            Item.rare = ModContent.RarityType<TitanRarity>();
            Item.useTime = 20;
            Item.useAnimation = 20;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.noMelee = true;
            Item.UseSound = SoundID.Item4;
            Item.shoot = ModContent.ProjectileType<TitanStarProjectile>();
            Item.buffType = ModContent.BuffType<TitanStarBuff>();
        }

        public override bool Shoot(Player player, Terraria.DataStructures.EntitySource_ItemUse_WithAmmo source, Microsoft.Xna.Framework.Vector2 position, Microsoft.Xna.Framework.Vector2 velocity, int type, int damage, float knockback)
        {
            player.AddBuff(Item.buffType, 2);
            return false;
        }
    }
}
