using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using DeterministicChaos.Content.Projectiles.Friendly;
using DeterministicChaos.Content.SoulTraits;

namespace DeterministicChaos.Content.Items
{
    public class ToyKnife : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 28;
            Item.height = 28;
            Item.damage = 13;
            Item.knockBack = 2f;
            Item.useTime = 18;
            Item.useAnimation = 18;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.autoReuse = true;
            Item.rare = ItemRarityID.LightPurple;
            Item.value = Item.buyPrice(gold: 2);
            Item.UseSound = SoundID.Item1;
            Item.shoot = ModContent.ProjectileType<ToyKnifeProjectile>();
            Item.shootSpeed = 12f;
            Item.DamageType = ModContent.GetInstance<MagicRogueDamageClass>();
        }

        public override void SetStaticDefaults()
        {
            // Register +3 Patience weapon investment
            SoulTraitGlobalItem.RegisterWeaponInvestment(Type, 3, SoulTraitType.Patience);
        }

        public override bool CanUseItem(Player player)
        {
            // Requires Patience trait to use
            var traitPlayer = player.GetModPlayer<SoulTraitPlayer>();
            if (traitPlayer.CurrentTrait != SoulTraitType.Patience)
            {
                return false;
            }

            return true;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            // Preserve rogue stealth - Calamity doesn't auto-consume for non-rogue classes,
            // but we ensure no consumption by storing and restoring stealth
            var toyKnifePlayer = player.GetModPlayer<ToyKnifePlayer>();
            toyKnifePlayer.PreserveStealthOnShoot();

            // Spawn the knife projectile
            Projectile.NewProjectile(
                source,
                position,
                velocity,
                type,
                damage,
                knockback,
                player.whoAmI
            );

            // Restore rogue stealth after shooting
            toyKnifePlayer.RestoreStealthAfterShoot();

            return false;
        }

        public override void ModifyTooltips(System.Collections.Generic.List<TooltipLine> tooltips)
        {
            // Patience color: Cyan (0, 255, 255)
            Color patienceColor = new Color(0, 255, 255);
            
            foreach (var line in tooltips)
            {
                if (line.Mod == "Terraria" && line.Name == "ItemName")
                {
                    line.OverrideColor = patienceColor;
                }
            }
        }
    }
}
