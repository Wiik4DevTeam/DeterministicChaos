using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.SoulTraits.Armor
{
    public class ManlyBandana : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 28;
            Item.height = 20;
            Item.value = Item.buyPrice(gold: 3);
            Item.rare = ItemRarityID.Orange;
            Item.accessory = true;
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips)
        {
            // Bravery color: Orange (255, 165, 0)
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
                
                if (bandanaPlayer.hasManlyBandana)
                {
                    float damageBonus = bandanaPlayer.currentDamageBonus * 100f;
                    tooltips.Add(new TooltipLine(Mod, "CurrentBonus", $"[c/FFA500:Current velocity bonus: +{damageBonus:F1}% damage]"));
                }
            }
        }

        public override void UpdateAccessory(Player player, bool hideVisual)
        {
            var traitPlayer = player.GetModPlayer<SoulTraitPlayer>();
            
            // Only grant bonuses if player has Bravery trait
            if (traitPlayer.CurrentTrait == SoulTraitType.Bravery)
            {
                // Grant 3 investment points to Bravery trait
                traitPlayer.ArmorInvestment += 3;
                
                // Velocity-based damage bonus handled in player class
                player.GetModPlayer<ManlyBandanaPlayer>().hasManlyBandana = true;
            }
        }
    }

    public class ManlyBandanaPlayer : ModPlayer
    {
        public bool hasManlyBandana;
        public bool hasBurningBandana;
        public float currentDamageBonus;

        public override void ResetEffects()
        {
            hasManlyBandana = false;
            hasBurningBandana = false;
            currentDamageBonus = 0f;
        }

        public override void PostUpdateEquips()
        {
            if (hasManlyBandana && Player.GetModPlayer<SoulTraitPlayer>().CurrentTrait == SoulTraitType.Bravery)
            {
                // Calculate velocity magnitude
                float velocity = Player.velocity.Length();
                
                // Max velocity for full bonus (about running speed with boots)
                const float maxVelocity = 16f;
                
                // Scale damage bonus based on velocity
                // ManlyBandana: 0-10%, BurningBandana: 0-20%
                float maxBonus = hasBurningBandana ? 0.20f : 0.10f;
                float velocityPercent = Math.Min(velocity / maxVelocity, 1f);
                currentDamageBonus = maxBonus * velocityPercent;
                
                Player.GetDamage(DamageClass.Generic) += currentDamageBonus;
                
                // Visual trail effect when moving fast
                if (velocityPercent > 0.5f && Main.rand.NextBool(3))
                {
                    int dustType = hasBurningBandana ? DustID.Torch : DustID.Torch;
                    float dustScale = hasBurningBandana ? 1.2f * velocityPercent : 0.8f * velocityPercent;
                    Dust dust = Dust.NewDustDirect(Player.position, Player.width, Player.height, dustType, 0f, 0f, 100, default, dustScale);
                    dust.noGravity = true;
                    dust.velocity *= 0.3f;

                    // Extra fire particles for burning bandana
                    if (hasBurningBandana && Main.rand.NextBool(2))
                    {
                        Dust fireDust = Dust.NewDustDirect(Player.position, Player.width, Player.height, DustID.FlameBurst, -Player.velocity.X * 0.2f, -Player.velocity.Y * 0.2f, 100, default, 1f);
                        fireDust.noGravity = true;
                    }
                }
            }
        }
    }
}
