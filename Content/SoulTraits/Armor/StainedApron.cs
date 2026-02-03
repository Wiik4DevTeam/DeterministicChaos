using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.SoulTraits.Armor
{
    public class StainedApron : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 24;
            Item.height = 28;
            Item.value = Item.buyPrice(gold: 3);
            Item.rare = ItemRarityID.Green;
            Item.accessory = true;
        }

        public override void ModifyTooltips(System.Collections.Generic.List<TooltipLine> tooltips)
        {
            // Kindness color: Green (50, 205, 50)
            Color kindnessColor = new Color(50, 205, 50);
            
            foreach (var line in tooltips)
            {
                if (line.Mod == "Terraria" && line.Name == "ItemName")
                {
                    line.OverrideColor = kindnessColor;
                }
            }
        }

        public override void UpdateAccessory(Player player, bool hideVisual)
        {
            var traitPlayer = player.GetModPlayer<SoulTraitPlayer>();
            
            // Only grant bonuses if player has Kindness trait
            if (traitPlayer.CurrentTrait == SoulTraitType.Kindness)
            {
                // Grant 3 investment points to Kindness trait
                traitPlayer.ArmorInvestment += 3;
                
                // Self-healing from teammate healing handled in player class
                player.GetModPlayer<StainedApronPlayer>().hasStainedApron = true;
            }
        }
    }

    public class StainedApronPlayer : ModPlayer
    {
        public bool hasStainedApron;
        
        // Track health of nearby teammates for healing detection
        private int[] lastTeammateHealth = new int[Main.maxPlayers];
        private int healCooldown = 0;

        public override void ResetEffects()
        {
            hasStainedApron = false;
        }

        public override void PostUpdate()
        {
            if (healCooldown > 0)
                healCooldown--;
            
            if (!hasStainedApron || Player.GetModPlayer<SoulTraitPlayer>().CurrentTrait != SoulTraitType.Kindness)
                return;

            // Check nearby allies for healing events
            float range = 400f;
            int totalHealMirrored = 0;
            
            for (int i = 0; i < Main.maxPlayers; i++)
            {
                Player other = Main.player[i];
                
                // Skip self
                if (!other.active || other.whoAmI == Player.whoAmI)
                    continue;
                
                // Team check, must be on same team (and team must be set, 0 = no team)
                if (Player.team == 0 || other.team != Player.team)
                    continue;
                
                // Check if in range
                if (Vector2.Distance(Player.Center, other.Center) > range)
                    continue;
                
                // Check if ally gained health since last frame
                int healthGained = other.statLife - lastTeammateHealth[i];
                
                // If ally gained health and it was a significant heal (not just regen)
                // Limit to reasonable healing amounts to prevent exploits
                if (healthGained > 5 && healthGained < 200)
                {
                    totalHealMirrored += healthGained;
                }
            }
            
            // Apply mirrored healing with cooldown and cap
            if (totalHealMirrored > 0 && healCooldown <= 0)
            {
                // Cap healing at 50 HP per tick to prevent excessive healing
                int cappedHeal = System.Math.Min(totalHealMirrored, 50);
                Player.statLife = System.Math.Min(Player.statLife + cappedHeal, Player.statLifeMax2);
                
                // Show healing text
                CombatText.NewText(Player.getRect(), CombatText.HealLife, cappedHeal);
                
                // Small cooldown to prevent spam
                healCooldown = 10;
            }
        }

        public override void PostUpdateBuffs()
        {
            // Store current health of all players for next frame comparison
            for (int i = 0; i < Main.maxPlayers; i++)
            {
                if (Main.player[i].active)
                {
                    lastTeammateHealth[i] = Main.player[i].statLife;
                }
            }
        }
    }
}
