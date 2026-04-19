using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.SoulTraits.Armor
{
    public class TrueHeartLocket : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 24;
            Item.height = 28;
            Item.value = Item.buyPrice(gold: 10);
            Item.rare = ItemRarityID.LightPurple;
            Item.accessory = true;
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips)
        {
            Color determinationColor = new Color(255, 0, 0);

            foreach (var line in tooltips)
            {
                if (line.Mod == "Terraria" && line.Name == "ItemName")
                {
                    line.OverrideColor = determinationColor;
                }
            }

            // Show current bonuses if equipped
            Player player = Main.LocalPlayer;
            if (player != null && player.active)
            {
                var locketPlayer = player.GetModPlayer<HeartLocketPlayer>();

                if (locketPlayer.hasTrueHeartLocket)
                {
                    float damageBonus = locketPlayer.currentDamageBonus * 100f;
                    float attackSpeedBonus = locketPlayer.currentAttackSpeedBonus * 100f;
                    int defenseBonus = locketPlayer.currentDefenseBonus;
                    float moveSpeedBonus = locketPlayer.currentMoveSpeedBonus * 100f;

                    tooltips.Add(new TooltipLine(Mod, "CurrentBonus",
                        $"[c/FF0000:Missing health bonuses: +{damageBonus:F1}% damage, +{attackSpeedBonus:F1}% attack speed]\n" +
                        $"[c/FF0000:+{defenseBonus} defense, +{moveSpeedBonus:F1}% movement speed]"));
                }
            }
        }

        public override void UpdateAccessory(Player player, bool hideVisual)
        {
            var traitPlayer = player.GetModPlayer<SoulTraitPlayer>();

            // Only grant trait bonuses if player has Determination trait
            if (traitPlayer.CurrentTrait == SoulTraitType.Determination)
            {
                // Grant 6 investment points to Determination trait
                traitPlayer.ArmorInvestment += 6;

                // Enhanced low-health bonuses
                var locketPlayer = player.GetModPlayer<HeartLocketPlayer>();
                locketPlayer.hasHeartLocket = true;
                locketPlayer.hasTrueHeartLocket = true;
            }

            // Cross Necklace effect (granted regardless of trait)
            // Increases invincibility frames after taking damage
            player.longInvince = true;
        }

        public override void AddRecipes()
        {
            // HeartLocket + Cross Necklace + Cobalt Bars
            CreateRecipe()
                .AddIngredient<HeartLocket>()
                .AddIngredient(ItemID.CrossNecklace)
                .AddIngredient(ItemID.CobaltBar, 10)
                .AddTile(TileID.TinkerersWorkbench)
                .Register();

            // HeartLocket + Cross Necklace + Palladium Bars
            CreateRecipe()
                .AddIngredient<HeartLocket>()
                .AddIngredient(ItemID.CrossNecklace)
                .AddIngredient(ItemID.PalladiumBar, 10)
                .AddTile(TileID.TinkerersWorkbench)
                .Register();
        }
    }
}
