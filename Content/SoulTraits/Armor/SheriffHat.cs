using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.GameContent.Achievements;

namespace DeterministicChaos.Content.SoulTraits.Armor
{
    public class SheriffHat : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 30;
            Item.height = 22;
            Item.value = Item.buyPrice(gold: 10);
            Item.rare = ItemRarityID.LightPurple;
            Item.accessory = true;
        }

        public override void ModifyTooltips(System.Collections.Generic.List<TooltipLine> tooltips)
        {
            Color justiceColor = new Color(255, 255, 0);

            foreach (var line in tooltips)
            {
                if (line.Mod == "Terraria" && line.Name == "ItemName")
                {
                    line.OverrideColor = justiceColor;
                }
            }

            // Show current attack speed buff status if equipped
            Player player = Main.LocalPlayer;
            if (player != null && player.active)
            {
                var hatPlayer = player.GetModPlayer<CowboyHatPlayer>();

                if (hatPlayer.hasSheriffHat)
                {
                    if (hatPlayer.hypercritAttackSpeedTimer > 0)
                    {
                        tooltips.Add(new TooltipLine(Mod, "HypercritBonus", $"[c/FFFF00:HYPERCRIT SURGE! Attack speed +20%!]"));
                    }
                    else if (hatPlayer.critAttackSpeedTimer > 0)
                    {
                        tooltips.Add(new TooltipLine(Mod, "CurrentBonus", $"[c/FFFF00:Attack speed buff ACTIVE! (+10%)]"));
                    }
                    else
                    {
                        tooltips.Add(new TooltipLine(Mod, "CurrentBonus", $"[c/808000:Attack speed buff ready (land a crit)]"));
                    }
                }
            }
        }

        public override void UpdateAccessory(Player player, bool hideVisual)
        {
            var traitPlayer = player.GetModPlayer<SoulTraitPlayer>();

            // Only grant trait bonuses if player has Justice trait
            if (traitPlayer.CurrentTrait == SoulTraitType.Justice)
            {
                // Grant 6 investment points to Justice trait
                traitPlayer.ArmorInvestment += 6;

                // Critical hit attack speed bonus (upgraded from CowboyHat)
                var hatPlayer = player.GetModPlayer<CowboyHatPlayer>();
                hatPlayer.hasCowboyHat = true;
                hatPlayer.hasSheriffHat = true;
            }

            // Bundle of Balloons effects (granted regardless of trait)
            player.GetJumpState(ExtraJump.CloudInABottle).Enable();
            player.GetJumpState(ExtraJump.BlizzardInABottle).Enable();
            player.GetJumpState(ExtraJump.SandstormInABottle).Enable();
            player.jumpBoost = true;

            // Horseshoe effect (negates fall damage)
            player.noFallDmg = true;
        }

        public override void AddRecipes()
        {
            // CowboyHat + Bundle of Balloons + Cobalt Bars
            CreateRecipe()
                .AddIngredient<CowboyHat>()
                .AddIngredient(ItemID.BundleofBalloons)
                .AddIngredient(ItemID.CobaltBar, 10)
                .AddTile(TileID.TinkerersWorkbench)
                .Register();

            // CowboyHat + Bundle of Balloons + Palladium Bars
            CreateRecipe()
                .AddIngredient<CowboyHat>()
                .AddIngredient(ItemID.BundleofBalloons)
                .AddIngredient(ItemID.PalladiumBar, 10)
                .AddTile(TileID.TinkerersWorkbench)
                .Register();
        }
    }
}
