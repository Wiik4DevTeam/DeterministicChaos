using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.SoulTraits.Armor
{
    public class CaringApron : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 24;
            Item.height = 28;
            Item.value = Item.buyPrice(gold: 10);
            Item.rare = ItemRarityID.LightPurple;
            Item.accessory = true;
        }

        public override void ModifyTooltips(System.Collections.Generic.List<TooltipLine> tooltips)
        {
            Color kindnessColor = new Color(50, 205, 50);

            foreach (var line in tooltips)
            {
                if (line.Mod == "Terraria" && line.Name == "ItemName")
                {
                    line.OverrideColor = kindnessColor;
                }
            }

            // Show ally count and potion cooldown info if equipped
            Player player = Main.LocalPlayer;
            if (player != null && player.active)
            {
                var apronPlayer = player.GetModPlayer<StainedApronPlayer>();

                if (apronPlayer.hasCaringApron)
                {
                    int nearbyAllies = apronPlayer.GetNearbyAllyCount();
                    if (nearbyAllies > 0)
                    {
                        tooltips.Add(new TooltipLine(Mod, "CurrentBonus", $"[c/32CD32:Tracking {nearbyAllies} nearby allies/NPCs for heal mirroring]"));
                    }
                    else
                    {
                        tooltips.Add(new TooltipLine(Mod, "CurrentBonus", $"[c/228B22:No allies nearby (need teammates or town NPCs within range)]"));
                    }

                    if (player.potionDelay > 0)
                    {
                        float seconds = player.potionDelay / 60f;
                        tooltips.Add(new TooltipLine(Mod, "PotionCooldown", $"[c/32CD32:Potion cooldown: {seconds:F1}s remaining]"));
                    }
                }
            }
        }

        public override void UpdateAccessory(Player player, bool hideVisual)
        {
            var traitPlayer = player.GetModPlayer<SoulTraitPlayer>();

            // Only grant trait bonuses if player has Kindness trait
            if (traitPlayer.CurrentTrait == SoulTraitType.Kindness)
            {
                // Grant 6 investment points to Kindness trait
                traitPlayer.ArmorInvestment += 6;

                // Heal mirroring + potion cooldown reduction
                var apronPlayer = player.GetModPlayer<StainedApronPlayer>();
                apronPlayer.hasStainedApron = true;
                apronPlayer.hasCaringApron = true;
            }

            // Putrid Scent effects (granted regardless of trait)
            player.aggro -= 400;
            player.GetDamage(DamageClass.Generic) += 0.05f;
        }

        public override void AddRecipes()
        {
            // StainedApron + Putrid Scent + Cobalt Bars
            CreateRecipe()
                .AddIngredient<StainedApron>()
                .AddIngredient(ItemID.PutridScent)
                .AddIngredient(ItemID.CobaltBar, 10)
                .AddTile(TileID.TinkerersWorkbench)
                .Register();

            // StainedApron + Putrid Scent + Palladium Bars
            CreateRecipe()
                .AddIngredient<StainedApron>()
                .AddIngredient(ItemID.PutridScent)
                .AddIngredient(ItemID.PalladiumBar, 10)
                .AddTile(TileID.TinkerersWorkbench)
                .Register();
        }
    }
}
