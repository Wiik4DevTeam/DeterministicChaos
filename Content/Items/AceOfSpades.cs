using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using DeterministicChaos.Content.Projectiles.Friendly;

namespace DeterministicChaos.Content.Items
{
    public class AceOfSpades : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 50;
            Item.height = 12;
            Item.scale = 0.5f;
            Item.damage = 19;
            Item.knockBack = 3f;
            Item.useTime = 16;
            Item.useAnimation = 16;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.autoReuse = true;
            Item.rare = ItemRarityID.Orange;
            Item.value = Item.buyPrice(gold: 3);
            Item.UseSound = SoundID.Item38 with { Volume = 0.9f, Pitch = -0.25f, PitchVariance = 0.15f };
            Item.shoot = ProjectileID.Bullet;
            Item.shootSpeed = 10f;
            Item.useAmmo = AmmoID.Bullet;
            Item.DamageType = DamageClass.Ranged;
        }

        public override Vector2? HoldoutOffset()
        {
            return new Vector2(-6f, 0f);
        }

        public override void ModifyShootStats(Player player, ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback)
        {
            // Convert Musket Balls into Spade projectiles
            if (type == ProjectileID.Bullet)
                type = ModContent.ProjectileType<AceOfSpadesBullet>();
        }
    }
}
