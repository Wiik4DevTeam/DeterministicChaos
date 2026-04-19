using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.SoulTraits.Armor
{
    public class BurningBandana : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 28;
            Item.height = 20;
            Item.value = Item.buyPrice(gold: 10);
            Item.rare = ItemRarityID.LightPurple;
            Item.accessory = true;
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips)
        {
            Color braveryColor = new Color(255, 165, 0);

            foreach (var line in tooltips)
            {
                if (line.Mod == "Terraria" && line.Name == "ItemName")
                {
                    line.OverrideColor = braveryColor;
                }
            }

            // Show current velocity damage bonus if equipped
            Player player = Main.LocalPlayer;
            if (player != null && player.active)
            {
                var bandanaPlayer = player.GetModPlayer<ManlyBandanaPlayer>();

                if (bandanaPlayer.hasBurningBandana)
                {
                    float damageBonus = bandanaPlayer.currentDamageBonus * 100f;
                    tooltips.Add(new TooltipLine(Mod, "CurrentBonus", $"[c/FFA500:Current velocity bonus: +{damageBonus:F1}% damage]"));
                }
            }
        }

        public override void UpdateAccessory(Player player, bool hideVisual)
        {
            var traitPlayer = player.GetModPlayer<SoulTraitPlayer>();

            // Only grant trait bonuses if player has Bravery trait
            if (traitPlayer.CurrentTrait == SoulTraitType.Bravery)
            {
                // Grant 6 investment points to Bravery trait
                traitPlayer.ArmorInvestment += 6;

                // Enhanced velocity-based damage bonus
                var bandanaPlayer = player.GetModPlayer<ManlyBandanaPlayer>();
                bandanaPlayer.hasManlyBandana = true;
                bandanaPlayer.hasBurningBandana = true;
            }

            // Hellfire Treads effects (granted regardless of trait)
            player.accRunSpeed = 6.75f;        // Hermes/Spectre Boots run speed
            player.rocketBoots = 3;            // Spectre Boots rocket time
            player.moveSpeed += 0.08f;         // Movement speed bonus
            player.iceSkate = true;            // Ice skating
            player.waterWalk = true;           // Lava wading
            player.fireWalk = true;            // Fire block immunity
            player.lavaMax += 420;             // 7 seconds of lava immunity
        }

        public override void AddRecipes()
        {
            // ManlyBandana + Hellfire Treads + Cobalt Bars
            CreateRecipe()
                .AddIngredient<ManlyBandana>()
                .AddIngredient(ItemID.HellfireTreads)
                .AddIngredient(ItemID.CobaltBar, 10)
                .AddTile(TileID.TinkerersWorkbench)
                .Register();

            // ManlyBandana + Hellfire Treads + Palladium Bars
            CreateRecipe()
                .AddIngredient<ManlyBandana>()
                .AddIngredient(ItemID.HellfireTreads)
                .AddIngredient(ItemID.PalladiumBar, 10)
                .AddTile(TileID.TinkerersWorkbench)
                .Register();
        }
    }
}
