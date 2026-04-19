using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.SoulTraits.Armor
{
    public class PrismaticGlasses : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 28;
            Item.height = 20;
            Item.value = Item.buyPrice(gold: 10);
            Item.rare = ItemRarityID.LightPurple;
            Item.accessory = true;
        }

        public override void ModifyTooltips(System.Collections.Generic.List<TooltipLine> tooltips)
        {
            Color perseveranceColor = new Color(255, 0, 255);

            foreach (var line in tooltips)
            {
                if (line.Mod == "Terraria" && line.Name == "ItemName")
                {
                    line.OverrideColor = perseveranceColor;
                }
            }

            // Show status if equipped
            Player player = Main.LocalPlayer;
            if (player != null && player.active)
            {
                var glassesPlayer = player.GetModPlayer<CloudyGlassesPlayer>();

                if (glassesPlayer.hasPrismaticGlasses)
                {
                    if (glassesPlayer.tempMaxManaTimer > 0)
                    {
                        float seconds = glassesPlayer.tempMaxManaTimer / 60f;
                        tooltips.Add(new TooltipLine(Mod, "TempMana", $"[c/FF00FF:+{glassesPlayer.tempMaxManaBonus} temporary max mana ({seconds:F1}s)]"));
                    }

                    if (player.immune)
                    {
                        float immuneSeconds = player.immuneTime / 60f;
                        tooltips.Add(new TooltipLine(Mod, "CurrentBonus", $"[c/FF00FF:Currently immune! ({immuneSeconds:F1}s remaining)]"));
                    }
                    else
                    {
                        tooltips.Add(new TooltipLine(Mod, "CurrentBonus", $"[c/800080:+0.3s immunity extension ready]"));
                    }
                }
            }
        }

        public override void UpdateAccessory(Player player, bool hideVisual)
        {
            var traitPlayer = player.GetModPlayer<SoulTraitPlayer>();

            // Only grant trait bonuses if player has Perseverance trait
            if (traitPlayer.CurrentTrait == SoulTraitType.Perseverance)
            {
                // Grant 6 investment points to Perseverance trait
                traitPlayer.ArmorInvestment += 6;

                // Cloudy Glasses effect (extended iframes on hit)
                var glassesPlayer = player.GetModPlayer<CloudyGlassesPlayer>();
                glassesPlayer.hasCloudyGlasses = true;
                glassesPlayer.hasPrismaticGlasses = true;
            }

            // Celestial Cuffs effects (granted regardless of trait)
            // Restores mana when damaged (Magic Cuffs)
            player.magicCuffs = true;
            // Increases mana star pickup range (Celestial Magnets)
            player.manaMagnet = true;
        }

        public override void AddRecipes()
        {
            // CloudyGlasses + Celestial Cuffs + Cobalt Bars
            CreateRecipe()
                .AddIngredient<CloudyGlasses>()
                .AddIngredient(ItemID.CelestialCuffs)
                .AddIngredient(ItemID.CobaltBar, 10)
                .AddTile(TileID.TinkerersWorkbench)
                .Register();

            // CloudyGlasses + Celestial Cuffs + Palladium Bars
            CreateRecipe()
                .AddIngredient<CloudyGlasses>()
                .AddIngredient(ItemID.CelestialCuffs)
                .AddIngredient(ItemID.PalladiumBar, 10)
                .AddTile(TileID.TinkerersWorkbench)
                .Register();
        }
    }
}
