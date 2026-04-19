using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.DataStructures;
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
            
            // Show current damage reduction status if equipped
            Player player = Main.LocalPlayer;
            if (player != null && player.active)
            {
                var ribbonPlayer = player.GetModPlayer<FadedRibbonPlayer>();
                
                if (ribbonPlayer.hasFadedRibbon)
                {
                    if (player.statLife >= player.statLifeMax2)
                    {
                        tooltips.Add(new TooltipLine(Mod, "CurrentBonus", $"[c/00FFFF:Damage reduction ACTIVE! (-30% damage taken)]"));
                    }
                    else
                    {
                        int missingHP = player.statLifeMax2 - player.statLife;
                        tooltips.Add(new TooltipLine(Mod, "CurrentBonus", $"[c/008080:Damage reduction inactive (need {missingHP} HP to full)]"));
                    }
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
        public bool hasCuteBow;

        // Life regen bonus thresholds for CuteBow
        private const float RegenHealthThreshold = 0.75f; // Must be at 75%+ HP
        private const int MaxRegenBonus = 30; // Max life regen bonus at full health
        private const int MinRegenBonus = 6;  // Min life regen bonus at 75% health

        public override void ResetEffects()
        {
            hasFadedRibbon = false;
            hasCuteBow = false;
        }

        public override void UpdateLifeRegen()
        {
            if (hasCuteBow && Player.GetModPlayer<SoulTraitPlayer>().CurrentTrait == SoulTraitType.Patience)
            {
                float healthPercent = (float)Player.statLife / Player.statLifeMax2;

                if (healthPercent >= RegenHealthThreshold)
                {
                    // Scale regen bonus from MinRegenBonus at 75% to MaxRegenBonus at 100%
                    float t = (healthPercent - RegenHealthThreshold) / (1f - RegenHealthThreshold);
                    int regenBonus = (int)(MinRegenBonus + t * (MaxRegenBonus - MinRegenBonus));
                    Player.lifeRegen += regenBonus;

                    // Cyan sparkle particles when regen is active
                    if (Main.rand.NextBool(8))
                    {
                        Dust dust = Dust.NewDustDirect(Player.position, Player.width, Player.height, DustID.IceTorch, 0f, -1f);
                        dust.noGravity = true;
                        dust.scale = 0.8f + t * 0.6f;
                        dust.velocity *= 0.3f;
                        dust.color = new Color(0, 255, 255);
                    }
                }
            }
        }

        public override void ModifyHurt(ref Player.HurtModifiers modifiers)
        {
            if (hasFadedRibbon && Player.statLife >= Player.statLifeMax2 && Player.GetModPlayer<SoulTraitPlayer>().CurrentTrait == SoulTraitType.Patience)
            {
                if (hasCuteBow)
                {
                    // Take 50% less damage while at max health (upgraded)
                    modifiers.FinalDamage *= 0.50f;
                }
                else
                {
                    // Take 30% less damage while at max health
                    modifiers.FinalDamage *= 0.70f;
                }
            }
        }
    }
}
