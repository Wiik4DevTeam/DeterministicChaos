using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using DeterministicChaos.Content.Buffs;

namespace DeterministicChaos.Content.Items
{
    public class RoaringLens : ModItem
    {
        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 1;
        }

        public override void SetDefaults()
        {
            Item.width = 22;
            Item.height = 22;
            Item.value = Item.buyPrice(gold: 2);
            Item.rare = ModContent.RarityType<DarkWorldRarity>();
            Item.useTime = 20;
            Item.useAnimation = 20;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.noMelee = true;
            Item.UseSound = SoundID.Item4;
            Item.shoot = ModContent.ProjectileType<Projectiles.Friendly.KnightSphere>();
            Item.buffType = ModContent.BuffType<RoaringLensBuff>();
        }

        public override bool Shoot(Player player, Terraria.DataStructures.EntitySource_ItemUse_WithAmmo source, Microsoft.Xna.Framework.Vector2 position, Microsoft.Xna.Framework.Vector2 velocity, int type, int damage, float knockback)
        {
            player.AddBuff(Item.buffType, 2);
            return false;
        }
    }
}
