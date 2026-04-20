using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using DeterministicChaos.Content.Projectiles.Friendly;
using DeterministicChaos.Content.Items.Accessories;
using DeterministicChaos.Content.Items.BossBags;
using DeterministicChaos.Content.Items.BossSummons;
using DeterministicChaos.Content.Items.Consumables;
using DeterministicChaos.Content.Items.DamageClasses;
using DeterministicChaos.Content.Items.Globals;
using DeterministicChaos.Content.Items.Materials;
using DeterministicChaos.Content.Items.Placeable;
using DeterministicChaos.Content.Items.Rarities;
using DeterministicChaos.Content.Items.Weapons;

namespace DeterministicChaos.Content.Items.Weapons
{
    public class ForthcomingWrath : ModItem
    {
        // Cone spread (radians) for the three glaives
        private const float ConeSpread = 0.20f;

        public override void SetDefaults()
        {
            Item.width = 40;
            Item.height = 40;
            Item.damage = 49;
            Item.knockBack = 4.5f;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.useTime = 18;
            Item.useAnimation = 18;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.DamageType = DamageClass.Melee;
            Item.UseSound = SoundID.Item71;
            Item.shoot = ModContent.ProjectileType<ForthcomingWrathProjectile>();
            Item.shootSpeed = 1f; // Not used for position, but required non-zero
            Item.rare = ModContent.RarityType<TitanRarity>();
            Item.value = Item.buyPrice(gold: 65);
        }

        public override void MeleeEffects(Player player, Rectangle hitbox) { }

        public override void ModifyTooltips(List<TooltipLine> tooltips)
        {
            var forthPlayer = Main.LocalPlayer.GetModPlayer<ForthcomingWrathPlayer>();
            string chargeText = forthPlayer.chargeReady
                ? "[c/47ECF7:Charge ready!, Next attack fires the Forthcoming Strike]"
                : $"[c/aaaaaa:Charge: {forthPlayer.hitCount}/{ForthcomingWrathPlayer.HitsNeeded} hits]";
            tooltips.Add(new TooltipLine(Mod, "ForthcomingCharge", chargeText));
        }

        public override bool Shoot(Player player, Terraria.DataStructures.EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            var forthPlayer = player.GetModPlayer<ForthcomingWrathPlayer>();

            if (forthPlayer.chargeReady)
            {
                // Charged strike: one massive glaive, 3× damage, aimed directly at cursor.
                // ai[2] = 999f signals the projectile to use charged behavior.
                forthPlayer.ConsumeCharge();
                Projectile.NewProjectile(
                    source, player.Center, Vector2.Zero, type,
                    damage * 3, knockback * 1.5f, player.whoAmI,
                    ai0: 0f, ai1: 0f, ai2: 999f);
                return false;
            }

            // Normal attack: fully random angles anywhere in the cone.
            float[] swingDirs = { 1f, 0f, -1f };
            int[]   delays    = { 0, 3, 6 };

            for (int i = 0; i < 3; i++)
            {
                float coneOffset = Main.rand.NextFloat(-ConeSpread, ConeSpread);
                Projectile.NewProjectile(
                    source, player.Center, Vector2.Zero, type,
                    damage, knockback, player.whoAmI,
                    ai0: coneOffset, ai1: delays[i], ai2: swingDirs[i]);
            }

            return false;
        }

        public override void AddRecipes()
        {
            // No recipe; obtained from boss loot only
        }
    }
}
