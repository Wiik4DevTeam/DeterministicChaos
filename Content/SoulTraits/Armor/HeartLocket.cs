using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.SoulTraits.Armor
{
    public class HeartLocket : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 24;
            Item.height = 28;
            Item.value = Item.buyPrice(gold: 3);
            Item.rare = ItemRarityID.Red;
            Item.accessory = true;
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips)
        {
            // Determination color: Red (255, 0, 0)
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
                
                if (locketPlayer.hasHeartLocket)
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
            
            // Only grant bonuses if player has Determination trait
            if (traitPlayer.CurrentTrait == SoulTraitType.Determination)
            {
                // Grant 3 investment points to Determination trait
                traitPlayer.ArmorInvestment += 3;
                
                // Stat bonuses based on missing health handled in player class
                player.GetModPlayer<HeartLocketPlayer>().hasHeartLocket = true;
            }
        }
    }

    public class HeartLocketPlayer : ModPlayer
    {
        public bool hasHeartLocket;
        public float currentDamageBonus;
        public float currentAttackSpeedBonus;
        public int currentDefenseBonus;
        public float currentMoveSpeedBonus;

        public override void ResetEffects()
        {
            hasHeartLocket = false;
            currentDamageBonus = 0f;
            currentAttackSpeedBonus = 0f;
            currentDefenseBonus = 0;
            currentMoveSpeedBonus = 0f;
        }

        public override void PostUpdateEquips()
        {
            if (hasHeartLocket && Player.GetModPlayer<SoulTraitPlayer>().CurrentTrait == SoulTraitType.Determination)
            {
                // Calculate missing health percentage (0 to 1)
                float missingHealthPercent = 1f - (Player.statLife / (float)Player.statLifeMax2);
                
                // Maximum bonuses at 1 HP: +15% damage, +15% attack speed, +10 defense, +10% move speed
                float bonusMultiplier = missingHealthPercent;
                
                currentDamageBonus = 0.15f * bonusMultiplier;
                currentAttackSpeedBonus = 0.15f * bonusMultiplier;
                currentDefenseBonus = (int)(10 * bonusMultiplier);
                currentMoveSpeedBonus = 0.10f * bonusMultiplier;
                
                Player.GetDamage(DamageClass.Generic) += currentDamageBonus;
                Player.GetAttackSpeed(DamageClass.Generic) += currentAttackSpeedBonus;
                Player.statDefense += currentDefenseBonus;
                Player.moveSpeed += currentMoveSpeedBonus;
                
                // Visual effect when at low health (below 50%)
                if (missingHealthPercent > 0.5f && Main.rand.NextBool(5))
                {
                    Dust dust = Dust.NewDustDirect(Player.position, Player.width, Player.height, DustID.LifeDrain, 0f, -1f, 100, default, 0.8f * missingHealthPercent);
                    dust.noGravity = true;
                    dust.velocity *= 0.5f;
                }
            }
        }
    }
}
