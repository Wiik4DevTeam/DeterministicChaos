using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using DeterministicChaos.Content.Projectiles.Friendly;

namespace DeterministicChaos.Content.Items
{
    public class KingOfHearts : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 28;
            Item.height = 28;
            Item.damage = 24;
            Item.DamageType = DamageClass.Magic;
            Item.mana = 50;
            Item.knockBack = 2f;
            Item.useTime = 30;
            Item.useAnimation = 30;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.channel = true;
            Item.UseSound = SoundID.Item8;
            Item.autoReuse = false;
            Item.reuseDelay = 120; // 2 second cooldown after release
            Item.rare = ItemRarityID.Orange;
            Item.value = Item.buyPrice(gold: 3);
            Item.shoot = ModContent.ProjectileType<KingOfHeartsProjectile>();
            Item.shootSpeed = 0f;
        }
    }
}
