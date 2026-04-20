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
    public class RealKnife : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 30;
            Item.height = 30;
            Item.damage = 21;
            Item.knockBack = 3.5f;
            Item.useTime = 12;
            Item.useAnimation = 12;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.autoReuse = true;
            Item.rare = ItemRarityID.LightPurple;
            Item.value = Item.buyPrice(gold: 10);
            Item.UseSound = null;
            Item.shoot = ModContent.ProjectileType<RealKnifeSwing>();
            Item.shootSpeed = 1f;
            Item.DamageType = ModContent.GetInstance<MeleeMagicDamageClass>();
            Item.mana = 0;
        }

        public override void SetStaticDefaults()
        {
            // Register +6 Determination weapon investment (upgraded from +3)
            SoulTraitGlobalItem.RegisterWeaponInvestment(Type, 6, SoulTraitType.Determination);
        }

        public override bool AltFunctionUse(Player player)
        {
            // Only players with a Determination soul can use the timed attack
            return player.GetModPlayer<SoulTraitPlayer>().CurrentTrait == SoulTraitType.Determination;
        }

        public override bool MeleePrefix()
        {
            return true;
        }

        public override bool CanUseItem(Player player)
        {
            var knifePlayer = player.GetModPlayer<RealKnifePlayer>();

            if (player.altFunctionUse == 2)
            {
                // Alt fire: requires Determination soul
                if (player.GetModPlayer<SoulTraitPlayer>().CurrentTrait != SoulTraitType.Determination)
                    return false;

                // Alt fire: check if prompt is already active
                if (knifePlayer.PromptActive)
                    return false;

                // Need 200 mana to activate
                if (player.statMana < 200)
                    return false;

                Item.useTime = 20;
                Item.useAnimation = 20;
                Item.shoot = ModContent.ProjectileType<RealKnifeSwing>();
                Item.UseSound = null;
                Item.channel = false;
                Item.autoReuse = false;
                Item.mana = 0;
            }
            else
            {
                // Normal knife swing, requires Determination trait
                if (player.GetModPlayer<SoulTraitPlayer>().CurrentTrait != SoulTraitType.Determination)
                    return false;

                // Don't allow during prompt
                if (knifePlayer.PromptActive)
                    return false;

                Item.useTime = 12;
                Item.useAnimation = 12;
                Item.shoot = ModContent.ProjectileType<RealKnifeSwing>();
                Item.UseSound = null;
                Item.channel = false;
                Item.autoReuse = true;
                Item.mana = 0;
            }

            return base.CanUseItem(player);
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            // Only spawn on owning client
            if (player.whoAmI != Main.myPlayer)
                return false;

            if (player.altFunctionUse == 2)
            {
                // Start attack prompt
                var knifePlayer = player.GetModPlayer<RealKnifePlayer>();
                if (!knifePlayer.PromptActive)
                {
                    // Consume 200 mana
                    player.statMana -= 200;
                    if (player.statMana < 0) player.statMana = 0;
                    player.manaRegenDelay = (int)player.maxRegenDelay;

                    knifePlayer.StartPrompt(damage);

                    // Play menu popup sound
                    SoundEngine.PlaySound(new SoundStyle("DeterministicChaos/Assets/Sounds/DTMenu"), player.Center);
                }
                return false;
            }
            else
            {
                // Normal knife swing with varied combo
                float swingDirection = 1f;
                player.GetModPlayer<RealKnifePlayer>().swingCombo++;

                Vector2 toMouse = (Main.MouseWorld - player.Center).SafeNormalize(Vector2.UnitX);
                float aimAngle = toMouse.ToRotation();

                Projectile.NewProjectile(source, player.Center, Vector2.Zero,
                    ModContent.ProjectileType<RealKnifeSwing>(), damage, knockback,
                    player.whoAmI, swingDirection, aimAngle);
            }

            return false;
        }

        public override void HoldItem(Player player)
        {
            var knifePlayer = player.GetModPlayer<RealKnifePlayer>();

            // Handle second alt-fire press to lock the marker
            if (player.whoAmI == Main.myPlayer && knifePlayer.PromptActive && !knifePlayer.MarkerLocked)
            {
                if (Main.mouseRight && Main.mouseRightRelease)
                {
                    knifePlayer.LockMarker();
                }
            }
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips)
        {
            foreach (TooltipLine line in tooltips)
            {
                if (line.Name == "ItemName" && line.Mod == "Terraria")
                {
                    line.OverrideColor = new Color(200, 30, 30);
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
                .AddIngredient(ModContent.ItemType<RustyKnife>(), 1)
                .AddIngredient(ModContent.ItemType<SoulCatalyst>(), 1)
                .AddIngredient(ItemID.CobaltBar, 10)
                .AddTile(TileID.Anvils)
                .Register();

            CreateRecipe()
                .AddIngredient(ModContent.ItemType<RustyKnife>(), 1)
                .AddIngredient(ModContent.ItemType<SoulCatalyst>(), 1)
                .AddIngredient(ItemID.PalladiumBar, 10)
                .AddTile(TileID.Anvils)
                .Register();
        }
    }
}
