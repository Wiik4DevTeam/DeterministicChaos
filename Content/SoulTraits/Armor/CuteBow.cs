using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.SoulTraits.Armor
{
    public class CuteBow : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 24;
            Item.height = 24;
            Item.value = Item.buyPrice(gold: 10);
            Item.rare = ItemRarityID.LightPurple;
            Item.accessory = true;
        }

        public override void ModifyTooltips(System.Collections.Generic.List<TooltipLine> tooltips)
        {
            Color patienceColor = new Color(0, 255, 255);

            foreach (var line in tooltips)
            {
                if (line.Mod == "Terraria" && line.Name == "ItemName")
                {
                    line.OverrideColor = patienceColor;
                }
            }

            // Show current status if equipped
            Player player = Main.LocalPlayer;
            if (player != null && player.active)
            {
                var ribbonPlayer = player.GetModPlayer<FadedRibbonPlayer>();

                if (ribbonPlayer.hasCuteBow)
                {
                    float healthPercent = (float)player.statLife / player.statLifeMax2;

                    if (player.statLife >= player.statLifeMax2)
                    {
                        tooltips.Add(new TooltipLine(Mod, "CurrentBonus", $"[c/00FFFF:Damage reduction ACTIVE! (-50% damage taken)]"));
                        tooltips.Add(new TooltipLine(Mod, "RegenBonus", $"[c/00FFFF:Life regeneration significantly boosted!]"));
                    }
                    else if (healthPercent >= 0.75f)
                    {
                        int missingHP = player.statLifeMax2 - player.statLife;
                        tooltips.Add(new TooltipLine(Mod, "CurrentBonus", $"[c/008080:Damage reduction inactive (need {missingHP} HP to full)]"));
                        tooltips.Add(new TooltipLine(Mod, "RegenBonus", $"[c/00FFFF:Life regeneration boosted!]"));
                    }
                    else
                    {
                        int missingHP = player.statLifeMax2 - player.statLife;
                        tooltips.Add(new TooltipLine(Mod, "CurrentBonus", $"[c/008080:Damage reduction inactive (need {missingHP} HP to full)]"));
                        tooltips.Add(new TooltipLine(Mod, "RegenBonus", $"[c/008080:Life regen bonus inactive (need 75%+ HP)]"));
                    }
                }
            }
        }

        public override void UpdateAccessory(Player player, bool hideVisual)
        {
            var traitPlayer = player.GetModPlayer<SoulTraitPlayer>();

            // Only grant trait bonuses if player has Patience trait
            if (traitPlayer.CurrentTrait == SoulTraitType.Patience)
            {
                // Grant 6 investment points to Patience trait
                traitPlayer.ArmorInvestment += 6;

                // Enhanced damage reduction + life regen
                var ribbonPlayer = player.GetModPlayer<FadedRibbonPlayer>();
                ribbonPlayer.hasFadedRibbon = true;
                ribbonPlayer.hasCuteBow = true;
            }
        }

        public override void AddRecipes()
        {
            // FadedRibbon + Jungle Rose + Cobalt Bars
            CreateRecipe()
                .AddIngredient<FadedRibbon>()
                .AddIngredient(ItemID.JungleRose)
                .AddIngredient(ItemID.CobaltBar, 10)
                .AddTile(TileID.TinkerersWorkbench)
                .Register();

            // FadedRibbon + Jungle Rose + Palladium Bars
            CreateRecipe()
                .AddIngredient<FadedRibbon>()
                .AddIngredient(ItemID.JungleRose)
                .AddIngredient(ItemID.PalladiumBar, 10)
                .AddTile(TileID.TinkerersWorkbench)
                .Register();
        }
    }
}
