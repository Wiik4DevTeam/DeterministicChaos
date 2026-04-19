using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using DeterministicChaos.Content.Projectiles.Friendly;
using DeterministicChaos.Content.SoulTraits;

namespace DeterministicChaos.Content.Items
{
    public class JusticeDecree : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 20;
            Item.height = 12;
            Item.scale = 0.7f;
            Item.damage = 23;
            Item.knockBack = 3f;
            Item.useTime = 30;
            Item.useAnimation = 30;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.autoReuse = true;
            Item.rare = ItemRarityID.LightPurple;
            Item.value = Item.buyPrice(gold: 10);
            Item.UseSound = SoundID.Item11 with { Volume = 0.6f };
            Item.shoot = ProjectileID.Bullet;
            Item.shootSpeed = 10f;
            Item.useAmmo = AmmoID.Bullet;
            Item.DamageType = ModContent.GetInstance<RangedSummonDamageClass>();
        }

        public override void SetStaticDefaults()
        {
            SoulTraitGlobalItem.RegisterWeaponInvestment(Type, 6, SoulTraitType.Justice);
        }

        public override bool CanUseItem(Player player)
        {
            var traitPlayer = player.GetModPlayer<SoulTraitPlayer>();
            return traitPlayer.CurrentTrait == SoulTraitType.Justice;
        }

        public override Vector2? HoldoutOffset()
        {
            return new Vector2(-6f, 0f);
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            var decreePlayer = player.GetModPlayer<JusticeDecreePlayer>();

            // If currently burst-firing, suppress normal shots
            if (decreePlayer.IsBurstFiring)
                return false;

            // Check if we have any marked targets, start burst fire
            if (decreePlayer.HasMarkedTargets)
            {
                decreePlayer.StartBurst(source, damage, knockback);
                return false;
            }

            // Normal bullet shot with tag tracking
            int bulletProj = Projectile.NewProjectile(
                source,
                position,
                velocity,
                type,
                damage,
                knockback,
                player.whoAmI
            );

            if (bulletProj >= 0 && bulletProj < Main.maxProjectiles)
            {
                var globalProj = Main.projectile[bulletProj].GetGlobalProjectile<HollowGunGlobalProjectile>();
                globalProj.isHollowGunBullet = true;
                Main.projectile[bulletProj].netUpdate = true;
            }

            return false;
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips)
        {
            foreach (TooltipLine line in tooltips)
            {
                if (line.Name == "ItemName" && line.Mod == "Terraria")
                {
                    line.OverrideColor = new Color(255, 255, 0);
                }
            }
        }

        public override bool PreDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor, ref float rotation, ref float scale, int whoAmI)
        {
            return true;
        }

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ModContent.ItemType<HollowGun>(), 1)
                .AddIngredient(ModContent.ItemType<SoulCatalyst>(), 1)
                .AddIngredient(ItemID.CobaltBar, 10)
                .AddTile(TileID.Anvils)
                .Register();

            CreateRecipe()
                .AddIngredient(ModContent.ItemType<HollowGun>(), 1)
                .AddIngredient(ModContent.ItemType<SoulCatalyst>(), 1)
                .AddIngredient(ItemID.PalladiumBar, 10)
                .AddTile(TileID.Anvils)
                .Register();
        }
    }
}
