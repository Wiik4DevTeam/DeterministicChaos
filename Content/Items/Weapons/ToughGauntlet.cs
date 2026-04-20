using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria;
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
    public class ToughGauntlet : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 32;
            Item.height = 32;
            Item.damage = 35;
            Item.knockBack = 6f;
            Item.useTime = 12;
            Item.useAnimation = 12;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.autoReuse = false;
            Item.rare = ItemRarityID.LightPurple;
            Item.value = Item.buyPrice(gold: 10);
            Item.UseSound = SoundID.Item1;
            Item.shoot = ModContent.ProjectileType<ToughGauntletProjectile>();
            Item.shootSpeed = 12f;
            Item.DamageType = ModContent.GetInstance<RangedMeleeDamageClass>();
        }

        public override void SetStaticDefaults()
        {
            // Register +6 Bravery weapon investment (hardmode upgrade)
            SoulTraitGlobalItem.RegisterWeaponInvestment(Type, 6, SoulTraitType.Bravery);
        }

        public override bool CanUseItem(Player player)
        {
            // Requires Bravery trait to use
            var traitPlayer = player.GetModPlayer<SoulTraitPlayer>();
            if (traitPlayer.CurrentTrait != SoulTraitType.Bravery)
            {
                return false;
            }

            var gauntletPlayer = player.GetModPlayer<ToughGauntletPlayer>();

            // Check cooldowns
            if (gauntletPlayer.ComboCooldown > 0)
                return false;

            if (gauntletPlayer.AttackCooldown > 0)
                return false;

            return true;
        }

        public override bool AltFunctionUse(Player player)
        {
            return true;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            return false;
        }

        public override bool? UseItem(Player player)
        {
            var gauntletPlayer = player.GetModPlayer<ToughGauntletPlayer>();
            bool isRightClick = player.altFunctionUse == 2;

            gauntletPlayer.ProcessInput(isRightClick);

            return true;
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips)
        {
            // Bravery color: Orange (255, 165, 0)
            Color braveryColor = new Color(255, 165, 0);

            foreach (var line in tooltips)
            {
                if (line.Mod == "Terraria" && line.Name == "ItemName")
                {
                    line.OverrideColor = braveryColor;
                }
            }

            var player = Main.LocalPlayer;
            var gauntletPlayer = player.GetModPlayer<ToughGauntletPlayer>();

            // Add combo info
            if (gauntletPlayer.CurrentCombo.Length > 0)
            {
                string comboDisplay = gauntletPlayer.CurrentCombo.Replace("L", "[c/FFA500:L]").Replace("R", "[c/FF6600:R]");
                var comboLine = new TooltipLine(Mod, "CurrentCombo", $"Current Combo: {comboDisplay}");
                tooltips.Add(comboLine);
            }
        }

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ModContent.ItemType<ToughGlove>(), 1)
                .AddIngredient(ModContent.ItemType<SoulCatalyst>(), 1)
                .AddIngredient(ItemID.CobaltBar, 10)
                .AddTile(TileID.Anvils)
                .Register();

            CreateRecipe()
                .AddIngredient(ModContent.ItemType<ToughGlove>(), 1)
                .AddIngredient(ModContent.ItemType<SoulCatalyst>(), 1)
                .AddIngredient(ItemID.PalladiumBar, 10)
                .AddTile(TileID.Anvils)
                .Register();
        }
    }
}
