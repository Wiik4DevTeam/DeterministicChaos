using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using DeterministicChaos.Content.Projectiles.Friendly;
using DeterministicChaos.Content.SoulTraits;

namespace DeterministicChaos.Content.Items
{
    public class BalletShoes : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 30;
            Item.height = 30;
            Item.damage = 18;
            Item.knockBack = 4f;
            Item.useTime = 20;
            Item.useAnimation = 20;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.autoReuse = false;
            Item.rare = ItemRarityID.LightPurple;
            Item.value = Item.buyPrice(gold: 3);
            Item.UseSound = null;
            Item.shoot = ModContent.ProjectileType<BalletShoesProjectile>();
            Item.shootSpeed = 18f;
            Item.DamageType = ModContent.GetInstance<SummonerMeleeDamageClass>();
        }

        public override void SetStaticDefaults()
        {
            // Register +3 Integrity weapon investment
            SoulTraitGlobalItem.RegisterWeaponInvestment(Type, 3);
        }

        public override bool CanUseItem(Player player)
        {
            // Requires Integrity trait to use
            var traitPlayer = player.GetModPlayer<SoulTraitPlayer>();
            if (traitPlayer.CurrentTrait != SoulTraitType.Integrity)
            {
                return false;
            }

            var shoesPlayer = player.GetModPlayer<BalletShoesPlayer>();
            
            // Check cooldown
            if (shoesPlayer.IsOnCooldown)
                return false;

            return true;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            var shoesPlayer = player.GetModPlayer<BalletShoesPlayer>();
            
            // Calculate velocity boost from stacks (0-3 stacks)
            float velocityMult = 1f + (shoesPlayer.ParryStacks * 0.25f); // 25% per stack
            
            // Calculate damage boost from stacks
            int finalDamage = damage + (shoesPlayer.ParryStacks * 5); // +5 damage per stack
            
            // Get direction to mouse
            Vector2 direction = (Main.MouseWorld - player.Center).SafeNormalize(Vector2.UnitX);
            Vector2 launchVelocity = direction * Item.shootSpeed * velocityMult;
            
            // Start the kick FIRST so parry window is active immediately (pass velocity for knockback)
            shoesPlayer.StartKick(launchVelocity);
            
            // Launch the player
            player.velocity = launchVelocity;
            
            // Spawn the projectile (hitbox follows player)
            Projectile.NewProjectile(
                source,
                player.Center,
                launchVelocity,
                type,
                finalDamage,
                knockback,
                player.whoAmI,
                shoesPlayer.ParryStacks // Pass stacks for visual/hit effects
            );
            
            // Play launch sound
            SoundEngine.PlaySound(SoundID.DD2_BookStaffCast with { Pitch = 0.3f }, player.Center);
            
            return false; // We handled the projectile ourselves
        }

        public override void ModifyTooltips(System.Collections.Generic.List<TooltipLine> tooltips)
        {
            var player = Main.LocalPlayer;
            var shoesPlayer = player.GetModPlayer<BalletShoesPlayer>();
            
            // Add stack info
            if (shoesPlayer.ParryStacks > 0)
            {
                var stackLine = new TooltipLine(Mod, "ParryStacks", $"Grace Stacks: {shoesPlayer.ParryStacks}/3 (halves next damage taken)");
                stackLine.OverrideColor = new Color(200, 150, 255); // Light purple for Integrity
                tooltips.Add(stackLine);
            }
        }
    }
}
