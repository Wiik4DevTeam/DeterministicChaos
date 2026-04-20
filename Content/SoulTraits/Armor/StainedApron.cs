using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using DeterministicChaos.Content.Items.Prefixes;
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
            
            // Show ally count if equipped
            Player player = Main.LocalPlayer;
            if (player != null && player.active)
            {
                var apronPlayer = player.GetModPlayer<StainedApronPlayer>();
                
                if (apronPlayer.hasStainedApron)
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
        public bool hasCaringApron;
        
        // Track health of nearby teammates and NPCs for healing detection
        private int[] lastTeammateHealth = new int[Main.maxPlayers];
        private int[] lastNPCHealth = new int[Main.maxNPCs];
        private int healCooldown = 0;

        public override void ResetEffects()
        {
            hasStainedApron = false;
            hasCaringApron = false;
        }

        public override void PostUpdate()
        {
            if (healCooldown > 0)
                healCooldown--;
            
            if (!hasStainedApron || Player.GetModPlayer<SoulTraitPlayer>().CurrentTrait != SoulTraitType.Kindness)
                return;

            float range = 400f;
            int totalHealMirrored = 0;
            
            // Check nearby players for healing events
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
                
                // If ally gained health (threshold of 1 to catch small heals like Kindness trait)
                if (healthGained >= 1 && healthGained < 200)
                {
                    totalHealMirrored += healthGained;
                }
            }
            
            // Check nearby town NPCs for healing events
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                
                if (!npc.active || !npc.friendly || !npc.townNPC)
                    continue;
                
                // Check if in range
                if (Vector2.Distance(Player.Center, npc.Center) > range)
                    continue;
                
                // Check if NPC gained health since last frame
                int healthGained = npc.life - lastNPCHealth[i];
                
                // If NPC gained health
                if (healthGained >= 1 && healthGained < 200)
                {
                    totalHealMirrored += healthGained;
                }
            }
            
            // Apply mirrored healing with cooldown and cap
            if (totalHealMirrored > 0 && healCooldown <= 0)
            {
                // Cap healing at 50 HP per tick to prevent excessive healing
                int cappedHeal = System.Math.Min(totalHealMirrored, 50);
                cappedHeal = Player.GetModPlayer<PrefixEffectPlayer>().ScaleHeal(cappedHeal);
                
                // Only heal if not at max health
                if (Player.statLife < Player.statLifeMax2)
                {
                    Player.statLife = System.Math.Min(Player.statLife + cappedHeal, Player.statLifeMax2);
                    
                    // Show healing text
                    CombatText.NewText(Player.getRect(), CombatText.HealLife, cappedHeal);
                }
                
                // Caring Apron: reduce potion sickness cooldown
                if (hasCaringApron && Player.potionDelay > 0)
                {
                    // Reduce by 1/4th the heal amount in seconds, capped at 1 second (60 ticks)
                    int reductionTicks = System.Math.Min((int)(cappedHeal / 4f * 60f), 60);
                    Player.potionDelay = System.Math.Max(0, Player.potionDelay - reductionTicks);
                    
                    // Green sparkle feedback
                    if (reductionTicks > 0)
                    {
                        for (int d = 0; d < 4; d++)
                        {
                            Dust dust = Dust.NewDustDirect(Player.position, Player.width, Player.height, DustID.GreenTorch, 0f, -1f, 100, default, 0.8f);
                            dust.noGravity = true;
                            dust.velocity *= 0.5f;
                        }
                    }
                }
                
                // Small cooldown to prevent spam
                healCooldown = 10;
            }

            // Save snapshots AFTER the comparison so that heals from projectile updates
            // (which run after player updates) are detected next frame.
            for (int i = 0; i < Main.maxPlayers; i++)
            {
                if (Main.player[i].active)
                    lastTeammateHealth[i] = Main.player[i].statLife;
            }
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                if (Main.npc[i].active)
                    lastNPCHealth[i] = Main.npc[i].life;
            }
        }
        
        public int GetNearbyAllyCount()
        {
            int count = 0;
            float range = 400f;
            
            // Count nearby players
            for (int i = 0; i < Main.maxPlayers; i++)
            {
                Player other = Main.player[i];
                if (!other.active || other.whoAmI == Player.whoAmI)
                    continue;
                    
                if (Player.team == 0 || other.team != Player.team)
                    continue;
                    
                if (Vector2.Distance(Player.Center, other.Center) <= range)
                    count++;
            }
            
            // Count nearby friendly NPCs (town NPCs)
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (npc.active && npc.friendly && npc.townNPC)
                {
                    if (Vector2.Distance(Player.Center, npc.Center) <= range)
                        count++;
                }
            }
            
            return count;
        }
    }
}
