using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.SoulTraits.Armor
{
    public class SturdySkirt : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 28;
            Item.height = 24;
            Item.value = Item.buyPrice(gold: 10);
            Item.rare = ItemRarityID.LightPurple;
            Item.accessory = true;
            Item.defense = 6;
        }

        public override void ModifyTooltips(System.Collections.Generic.List<TooltipLine> tooltips)
        {
            Color integrityColor = new Color(30, 144, 255);

            foreach (var line in tooltips)
            {
                if (line.Mod == "Terraria" && line.Name == "ItemName")
                {
                    line.OverrideColor = integrityColor;
                }
            }

            // Show current status if equipped
            Player player = Main.LocalPlayer;
            if (player != null && player.active)
            {
                var tutuPlayer = player.GetModPlayer<OldTuTuPlayer>();
                var traitPlayer = player.GetModPlayer<SoulTraitPlayer>();

                bool isIntegrity = traitPlayer.CurrentTrait == SoulTraitType.Integrity;

                if (tutuPlayer.hasSturdySkirt && isIntegrity)
                {
                    if (tutuPlayer.dashTimer > 0)
                    {
                        tooltips.Add(new TooltipLine(Mod, "DashStatus", $"[c/1E90FF:DASHING!]"));
                    }
                    else if (tutuPlayer.dashCooldown > 0)
                    {
                        float cooldownSeconds = tutuPlayer.dashCooldown / 60f;
                        tooltips.Add(new TooltipLine(Mod, "DashStatus", $"[c/4169E1:Dash on cooldown ({cooldownSeconds:F1}s)]"));
                    }
                    else
                    {
                        tooltips.Add(new TooltipLine(Mod, "DashStatus", $"[c/1E90FF:Dash ready! (Calamity Dash key)]"));
                    }

                    if (tutuPlayer.pendingDotDamage > 0)
                    {
                        float remainingSeconds = tutuPlayer.dotTimer / 60f;
                        tooltips.Add(new TooltipLine(Mod, "DotStatus", $"[c/9B30FF:Poison ticking: {(int)tutuPlayer.pendingDotDamage} damage over {remainingSeconds:F1}s]"));
                    }
                }
                else if (!isIntegrity)
                {
                    tooltips.Add(new TooltipLine(Mod, "CurrentBonus", $"[c/FF6666:Requires Integrity trait (Current: {traitPlayer.CurrentTrait})]"));
                }
            }
        }

        public override void UpdateAccessory(Player player, bool hideVisual)
        {
            var traitPlayer = player.GetModPlayer<SoulTraitPlayer>();

            // Only grant trait bonuses if player has Integrity trait
            if (traitPlayer.CurrentTrait == SoulTraitType.Integrity)
            {
                // Grant 6 investment points to Integrity trait
                traitPlayer.ArmorInvestment += 6;

                // Enable dash + DoT conversion
                var tutuPlayer = player.GetModPlayer<OldTuTuPlayer>();
                tutuPlayer.hasOldTuTu = true;
                tutuPlayer.hasSturdySkirt = true;
            }
        }

        public override void AddRecipes()
        {
            // OldTuTu + 3 Shackles + Silk + Cobalt Bars
            CreateRecipe()
                .AddIngredient<OldTuTu>()
                .AddIngredient(ItemID.Shackle, 3)
                .AddIngredient(ItemID.Silk, 10)
                .AddIngredient(ItemID.CobaltBar, 10)
                .AddTile(TileID.TinkerersWorkbench)
                .Register();

            // OldTuTu + 3 Shackles + Silk + Palladium Bars
            CreateRecipe()
                .AddIngredient<OldTuTu>()
                .AddIngredient(ItemID.Shackle, 3)
                .AddIngredient(ItemID.Silk, 10)
                .AddIngredient(ItemID.PalladiumBar, 10)
                .AddTile(TileID.TinkerersWorkbench)
                .Register();
        }
    }
}
