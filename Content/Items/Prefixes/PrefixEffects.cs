using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using DeterministicChaos.Content.SoulTraits;
using DeterministicChaos.Content.Items.Accessories;
using DeterministicChaos.Content.Items.BossBags;
using DeterministicChaos.Content.Items.BossSummons;
using DeterministicChaos.Content.Items.Consumables;
using DeterministicChaos.Content.Items.DamageClasses;
using DeterministicChaos.Content.Items.Globals;
using DeterministicChaos.Content.Items.Materials;
using DeterministicChaos.Content.Items.Placeable;
using DeterministicChaos.Content.Items.Rarities;
using DeterministicChaos.Content.Items.Weapons;

namespace DeterministicChaos.Content.Items.Prefixes
{
    public class PrefixEffectPlayer : ModPlayer
    {
        // Counts of equipped accessories with each prefix
        public int SoulfulCount = 0;
        public int EmpatheticCount = 0;
        public int LingeringCount = 0;

        // Derived multipliers
        public float HealingMultiplier => 1f + EmpatheticCount * 0.07f;
        public float BuffDurationMultiplier => 1f + LingeringCount * 0.06f;

        public override void ResetEffects()
        {
            SoulfulCount = 0;
            EmpatheticCount = 0;
            LingeringCount = 0;
        }

        public override void PostUpdateEquips()
        {
            // Apply Soulful investment bonus
            if (SoulfulCount > 0)
            {
                var stp = Player.GetModPlayer<SoulTraitPlayer>();
                if (stp.CurrentTrait != SoulTraitType.None)
                {
                    stp.ArmorInvestment += SoulfulCount;
                }
            }
        }

        
        // Scales a heal amount by the Empathetic multiplier.
        // Call this for all manual heal operations.
        
        public int ScaleHeal(int baseHeal)
        {
            if (EmpatheticCount <= 0 || baseHeal <= 0)
                return baseHeal;
            return Math.Max(1, (int)(baseHeal * HealingMultiplier));
        }

        
        // Scales a buff duration by the Lingering multiplier.
        // Only applies to positive buffs (not debuffs).
        
        public int ScaleBuffDuration(int baseDuration, int buffType)
        {
            if (LingeringCount <= 0 || baseDuration <= 2)
                return baseDuration;

            // Don't extend debuffs
            if (buffType >= 0 && buffType < BuffLoader.BuffCount && Main.debuff[buffType])
                return baseDuration;

            return (int)(baseDuration * BuffDurationMultiplier);
        }
    }

    
    // Scans equipped accessories each frame to count prefix instances
    // and hooks into potion healing.
    
    public class PrefixEffectGlobalItem : GlobalItem
    {
        public override void UpdateAccessory(Item item, Player player, bool hideVisual)
        {
            if (!item.accessory)
                return;

            var prefixPlayer = player.GetModPlayer<PrefixEffectPlayer>();

            if (item.prefix == ModContent.PrefixType<SoulfulPrefix>())
                prefixPlayer.SoulfulCount++;
            else if (item.prefix == ModContent.PrefixType<EmpatheticPrefix>())
                prefixPlayer.EmpatheticCount++;
            else if (item.prefix == ModContent.PrefixType<LingeringPrefix>())
                prefixPlayer.LingeringCount++;
        }

        public override void GetHealLife(Item item, Player player, bool quickHeal, ref int healValue)
        {
            // Empathetic prefix: +7% potion healing per equipped accessory
            var prefixPlayer = player.GetModPlayer<PrefixEffectPlayer>();
            if (prefixPlayer.EmpatheticCount > 0 && healValue > 0)
            {
                healValue = prefixPlayer.ScaleHeal(healValue);
            }
        }

        public override void ModifyTooltips(Item item, List<TooltipLine> tooltips)
        {
            Player player = Main.LocalPlayer;
            var prefixPlayer = player.GetModPlayer<PrefixEffectPlayer>();

            // Accessory prefix tooltip lines
            if (item.accessory)
            {
                if (item.prefix == ModContent.PrefixType<SoulfulPrefix>())
                {
                    tooltips.Add(new TooltipLine(Mod, "PrefixSoulful", "+1 soul investment") { IsModifier = true, IsModifierBad = false });
                }
                else if (item.prefix == ModContent.PrefixType<EmpatheticPrefix>())
                {
                    tooltips.Add(new TooltipLine(Mod, "PrefixEmpathetic", "+7% healing power") { IsModifier = true, IsModifierBad = false });
                }
                else if (item.prefix == ModContent.PrefixType<LingeringPrefix>())
                {
                    tooltips.Add(new TooltipLine(Mod, "PrefixLingering", "+6% buff duration") { IsModifier = true, IsModifierBad = false });
                }
            }

            // Show scaled healing on potions/items with healLife
            if (prefixPlayer.EmpatheticCount > 0 && item.healLife > 0)
            {
                int scaledHeal = prefixPlayer.ScaleHeal(item.healLife);
                if (scaledHeal != item.healLife)
                {
                    foreach (var line in tooltips)
                    {
                        if (line.Name == "HealLife")
                        {
                            line.Text = Regex.Replace(line.Text, @"\d+", scaledHeal.ToString());
                            line.OverrideColor = new Color(100, 255, 100);
                            break;
                        }
                    }
                }
            }

            // Show scaled buff duration on buff potions
            if (prefixPlayer.LingeringCount > 0 && item.buffTime > 2 && item.buffType > 0)
            {
                // Don't scale debuffs
                if (item.buffType < BuffLoader.BuffCount && !Main.debuff[item.buffType])
                {
                    int scaledTime = prefixPlayer.ScaleBuffDuration(item.buffTime, item.buffType);
                    if (scaledTime != item.buffTime)
                    {
                        foreach (var line in tooltips)
                        {
                            if (line.Name == "BuffTime")
                            {
                                int totalSeconds = scaledTime / 60;
                                int minutes = totalSeconds / 60;
                                int seconds = totalSeconds % 60;
                                line.Text = minutes > 0
                                    ? $"{minutes} minute" + (minutes != 1 ? "s" : "") + (seconds > 0 ? $" {seconds} second" + (seconds != 1 ? "s" : "") : "") + " duration"
                                    : $"{seconds} second" + (seconds != 1 ? "s" : "") + " duration";
                                line.OverrideColor = new Color(100, 255, 100);
                                break;
                            }
                        }
                    }
                }
            }
        }
    }
}
