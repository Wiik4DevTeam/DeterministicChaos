using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.SoulTraits.Armor
{
    public class FadedRibbon : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 24;
            Item.height = 24;
            Item.value = Item.buyPrice(gold: 3);
            Item.rare = ItemRarityID.Cyan;
            Item.accessory = true;
        }

        public override void ModifyTooltips(System.Collections.Generic.List<TooltipLine> tooltips)
        {
            // Patience color: Cyan (0, 255, 255)
            Color patienceColor = new Color(0, 255, 255);
            
            foreach (var line in tooltips)
            {
                if (line.Mod == "Terraria" && line.Name == "ItemName")
                {
                    line.OverrideColor = patienceColor;
                }
            }
        }

        public override void UpdateAccessory(Player player, bool hideVisual)
        {
            var traitPlayer = player.GetModPlayer<SoulTraitPlayer>();
            
            // Only grant bonuses if player has Patience trait
            if (traitPlayer.CurrentTrait == SoulTraitType.Patience)
            {
                // Grant 3 investment points to Patience trait
                traitPlayer.ArmorInvestment += 3;
                
                // Damage reduction at max health handled in player class
                player.GetModPlayer<FadedRibbonPlayer>().hasFadedRibbon = true;
            }
        }
    }

    public class FadedRibbonPlayer : ModPlayer
    {
        public bool hasFadedRibbon;

        public override void ResetEffects()
        {
            hasFadedRibbon = false;
        }

        public override void ModifyHurt(ref Player.HurtModifiers modifiers)
        {
            if (hasFadedRibbon && Player.statLife >= Player.statLifeMax2 && Player.GetModPlayer<SoulTraitPlayer>().CurrentTrait == SoulTraitType.Patience)
            {
                // Take 30% less damage while at max health
                modifiers.FinalDamage *= 0.70f;
            }
        }
    }
}
