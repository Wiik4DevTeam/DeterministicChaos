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
    public class HollowGun : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 20;
            Item.height = 12;
            Item.scale = 0.5f;
            Item.damage = 10;
            Item.knockBack = 3f;
            Item.useTime = 30;
            Item.useAnimation = 30;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.autoReuse = true;
            Item.rare = ItemRarityID.Orange;
            Item.value = Item.buyPrice(gold: 3);
            Item.UseSound = SoundID.Item11;
            Item.shoot = ProjectileID.Bullet;
            Item.shootSpeed = 10f;
            Item.useAmmo = AmmoID.Bullet;
            Item.DamageType = ModContent.GetInstance<RangedSummonDamageClass>();
        }

        public override void SetStaticDefaults()
        {
            // Register +3 Justice weapon investment
            SoulTraitGlobalItem.RegisterWeaponInvestment(Type, 3);
        }

        public override bool CanUseItem(Player player)
        {
            // Requires Justice trait to use
            var traitPlayer = player.GetModPlayer<SoulTraitPlayer>();
            return traitPlayer.CurrentTrait == SoulTraitType.Justice;
        }

        public override Vector2? HoldoutOffset()
        {
            return new Vector2(-6f, 0f);
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            var hollowPlayer = player.GetModPlayer<HollowGunPlayer>();

            // Check if we have a marked target for auto-homing
            if (hollowPlayer.HasMarkedTarget && hollowPlayer.MarkedTargetIndex >= 0)
            {
                NPC target = Main.npc[hollowPlayer.MarkedTargetIndex];
                if (target != null && target.active && !target.friendly && target.CanBeChasedBy())
                {
                    // Fire hitscan Justice Beam (instant line to target)
                    int proj = Projectile.NewProjectile(
                        source,
                        player.Center,
                        Vector2.Zero,
                        ModContent.ProjectileType<JusticeBeam>(),
                        damage, // Full damage
                        knockback,
                        player.whoAmI,
                        hollowPlayer.MarkedTargetIndex, // ai[0] = target index
                        player.Center.X // ai[1] = start X for line drawing
                    );

                    // Set localAI[0] for start Y position
                    if (proj >= 0 && proj < Main.maxProjectiles)
                    {
                        Main.projectile[proj].localAI[0] = player.Center.Y;
                    }

                    // Consume the mark
                    hollowPlayer.ClearMarkedTarget();
                    
                    // Play ChargeShot sound for justice beam
                    SoundEngine.PlaySound(new SoundStyle("DeterministicChaos/Assets/Sounds/ChargeShot"), position);
                    
                    return false; // Don't fire normal bullet
                }
                else
                {
                    // Target is gone, clear mark
                    hollowPlayer.ClearMarkedTarget();
                }
            }

            // Normal bullet shot - fire the actual bullet type with tag tracking
            int bulletProj = Projectile.NewProjectile(
                source,
                position,
                velocity,
                type, // Use the actual ammo's projectile type
                damage,
                knockback,
                player.whoAmI
            );

            // Mark this projectile as a HollowGun bullet for tag damage
            if (bulletProj >= 0 && bulletProj < Main.maxProjectiles)
            {
                var globalProj = Main.projectile[bulletProj].GetGlobalProjectile<HollowGunGlobalProjectile>();
                globalProj.isHollowGunBullet = true;
                Main.projectile[bulletProj].netUpdate = true; // Sync to other clients
            }

            return false; // We handled spawning manually
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips)
        {
            foreach (TooltipLine line in tooltips)
            {
                if (line.Name == "ItemName" && line.Mod == "Terraria")
                {
                    // Yellow color for Justice
                    line.OverrideColor = new Color(255, 255, 0);
                }
            }
        }

        public override bool PreDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor, ref float rotation, ref float scale, int whoAmI)
        {
            return true;
        }
    }
}
