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
    public class FryingPan : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 40;
            Item.height = 40;
            Item.damage = 10;
            Item.knockBack = 50f; // Massive knockback for melee hits
            Item.useTime = 32;
            Item.useAnimation = 32;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.autoReuse = true;
            Item.rare = ItemRarityID.Orange;
            Item.value = Item.buyPrice(gold: 2);
            Item.UseSound = null;
            Item.shoot = ModContent.ProjectileType<FryingPanSwing>();
            Item.shootSpeed = 14f;
            Item.DamageType = ModContent.GetInstance<RangedMeleeDamageClass>();
            Item.useAmmo = AmmoID.Dart; // Uses Seeds (dart ammo)
        }

        public override void SetStaticDefaults()
        {
            // Register +3 Kindness weapon investment
            SoulTraitGlobalItem.RegisterWeaponInvestment(Type, 3);
        }

        public override bool MeleePrefix()
        {
            // Take melee reforges despite being a hybrid
            return true;
        }

        public override bool CanUseItem(Player player)
        {
            // Requires Kindness trait to use the egg-launching feature
            var traitPlayer = player.GetModPlayer<SoulTraitPlayer>();
            if (traitPlayer.CurrentTrait != SoulTraitType.Kindness)
            {
                // Can still swing but won't shoot eggs
                Item.useAmmo = AmmoID.None;
            }
            else
            {
                Item.useAmmo = AmmoID.Dart; // Seeds
            }
            return base.CanUseItem(player);
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            // Only spawn on owning client
            if (player.whoAmI != Main.myPlayer)
                return false;

            Vector2 toMouse = (Main.MouseWorld - player.Center).SafeNormalize(Vector2.UnitX);
            float aimAngle = toMouse.ToRotation();

            // Spawn the swing projectile (upward swing)
            Projectile.NewProjectile(source, player.Center, Vector2.Zero,
                ModContent.ProjectileType<FryingPanSwing>(), damage, knockback,
                player.whoAmI, 1f, aimAngle);

            // If player has Kindness trait, also spawn an egg
            var traitPlayer = player.GetModPlayer<SoulTraitPlayer>();
            if (traitPlayer.CurrentTrait == SoulTraitType.Kindness)
            {
                // Egg direction: upward arc toward mouse
                Vector2 eggVelocity = toMouse * Item.shootSpeed;
                // Add slight upward arc
                eggVelocity.Y -= 2f;

                Projectile.NewProjectile(source, player.Center + toMouse * 30f, eggVelocity,
                    ModContent.ProjectileType<EggProjectile>(), damage, 2f,
                    player.whoAmI);

                SoundEngine.PlaySound(SoundID.Item1 with { Pitch = 0.5f, Volume = 0.8f }, player.Center);
            }
            else
            {
                // Just the swing sound without egg
                SoundEngine.PlaySound(SoundID.Item1 with { Pitch = 0.2f, Volume = 0.7f }, player.Center);
            }

            return false; // We handled spawning manually
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips)
        {
            foreach (TooltipLine line in tooltips)
            {
                if (line.Name == "ItemName" && line.Mod == "Terraria")
                {
                    // Green color for Kindness
                    line.OverrideColor = new Color(50, 205, 50);
                }
            }
        }

        public override bool PreDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor, ref float rotation, ref float scale, int whoAmI)
        {
            return true;
        }
    }
}
