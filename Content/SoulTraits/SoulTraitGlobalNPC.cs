using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.SoulTraits
{
    public class SoulTraitGlobalNPC : GlobalNPC
    {
        public override void OnKill(NPC npc)
        {
            // Reset Patience stacks when a boss is killed
            if (npc.boss)
            {
                foreach (Player player in Main.player)
                {
                    if (player.active && !player.dead)
                    {
                        var traitPlayer = player.GetModPlayer<SoulTraitPlayer>();
                        traitPlayer.ResetPatienceMarksOnBossSummon();
                    }
                }
            }
        }

        public override void EditSpawnRate(Player player, ref int spawnRate, ref int maxSpawns)
        {
            // Patience 3 Investment: Enemies are less likely to target you
            // This is handled through aggro reduction, but we can also reduce spawn rates slightly
            var traitPlayer = player.GetModPlayer<SoulTraitPlayer>();
            if (traitPlayer.CurrentTrait == SoulTraitType.Patience && traitPlayer.TotalInvestment >= 3)
            {
                spawnRate = (int)(spawnRate * 1.3f);
            }
        }
    }

    public class SoulTraitGlobalBuff : GlobalBuff
    {
        // Potion buffs that should be shared with allies for Kindness
        private static HashSet<int> SharablePotionBuffs = new HashSet<int>
        {
            BuffID.Ironskin,
            BuffID.Regeneration,
            BuffID.Swiftness,
            BuffID.Archery,
            BuffID.MagicPower,
            BuffID.Thorns,
            BuffID.Heartreach,
            BuffID.Endurance,
            BuffID.Lifeforce,
            BuffID.Rage,
            BuffID.Wrath,
            BuffID.Inferno,
            BuffID.SugarRush,
            BuffID.Titan,
            BuffID.Summoning
        };

        public override void Update(int type, Player player, ref int buffIndex)
        {
            var traitPlayer = player.GetModPlayer<SoulTraitPlayer>();

            // Kindness 12 Investment: +20% potion duration and share with allies
            if (traitPlayer.CurrentTrait == SoulTraitType.Kindness && traitPlayer.TotalInvestment >= 12)
            {
                if (SharablePotionBuffs.Contains(type))
                {
                    // Share buff with nearby allies
                    ShareBuffWithAllies(player, type, player.buffTime[buffIndex]);
                }
            }
        }

        private void ShareBuffWithAllies(Player source, int buffType, int buffTime)
        {
            float range = 400f;
            
            // Share with nearby players
            for (int i = 0; i < Main.maxPlayers; i++)
            {
                Player other = Main.player[i];
                if (other.active && !other.dead && other.whoAmI != source.whoAmI)
                {
                    if (Vector2.Distance(source.Center, other.Center) <= range)
                    {
                        // Only add buff if they dont have it or have less time remaining
                        int existingIndex = other.FindBuffIndex(buffType);
                        if (existingIndex < 0)
                        {
                            other.AddBuff(buffType, buffTime / 2);
                        }
                    }
                }
            }
            
            // Share with nearby friendly NPCs (town NPCs)
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (npc.active && npc.friendly && npc.townNPC)
                {
                    if (Vector2.Distance(source.Center, npc.Center) <= range)
                    {
                        // NPCs can have buffs too
                        npc.AddBuff(buffType, buffTime / 2);
                    }
                }
            }
        }

        public static void RegisterSharablePotion(int buffID)
        {
            SharablePotionBuffs.Add(buffID);
        }
    }

    public class SoulTraitPotionItem : GlobalItem
    {
        // Dictionary to store potion investment values
        private static Dictionary<int, int> PotionInvestmentValues = new Dictionary<int, int>();

        public override void SetStaticDefaults()
        {
            
        }
        public static void RegisterPotionInvestment(int itemID, int investmentPoints)
        {
            PotionInvestmentValues[itemID] = investmentPoints;
        }

        public static int GetPotionInvestment(int itemID)
        {
            return PotionInvestmentValues.TryGetValue(itemID, out int value) ? value : 0;
        }

        public override void GetHealLife(Item item, Player player, bool quickHeal, ref int healValue)
        {
            // Kindness 12 Investment: +20% potion effect
            var traitPlayer = player.GetModPlayer<SoulTraitPlayer>();
            if (traitPlayer.CurrentTrait == SoulTraitType.Kindness && traitPlayer.TotalInvestment >= 12)
            {
                if (item.healLife > 0)
                {
                    healValue = (int)(healValue * 1.2f);
                }
            }
        }

        public override void GetHealMana(Item item, Player player, bool quickHeal, ref int healValue)
        {
            // Kindness 12 Investment: +20% potion effect
            var traitPlayer = player.GetModPlayer<SoulTraitPlayer>();
            if (traitPlayer.CurrentTrait == SoulTraitType.Kindness && traitPlayer.TotalInvestment >= 12)
            {
                if (item.healMana > 0)
                {
                    healValue = (int)(healValue * 1.2f);
                }
            }
        }

        public override bool? UseItem(Item item, Player player)
        {
            // Kindness 12 Investment: Share healing potion with nearby allies
            var traitPlayer = player.GetModPlayer<SoulTraitPlayer>();
            if (traitPlayer.CurrentTrait == SoulTraitType.Kindness && traitPlayer.TotalInvestment >= 12)
            {
                if (item.healLife > 0)
                {
                    ShareHealWithAllies(player, item.healLife);
                }
                if (item.healMana > 0)
                {
                    ShareManaWithAllies(player, item.healMana);
                }
            }

            // Track potion investment when consumed
            if (PotionInvestmentValues.TryGetValue(item.type, out int investment))
            {
                // Potion investment is temporary, lasting for the buff duration
                // This is handled through a buff system instead
            }

            return base.UseItem(item, player);
        }

        private void ShareHealWithAllies(Player source, int healAmount)
        {
            float range = 400f;
            // Allies get 50% of the heal amount
            int allyHeal = (int)(healAmount * 0.5f);
            if (allyHeal < 1)
                allyHeal = 1;

            for (int i = 0; i < Main.maxPlayers; i++)
            {
                Player other = Main.player[i];
                if (other.active && !other.dead && other.whoAmI != source.whoAmI)
                {
                    if (Vector2.Distance(source.Center, other.Center) <= range)
                    {
                        other.statLife = System.Math.Min(other.statLife + allyHeal, other.statLifeMax2);
                        other.HealEffect(allyHeal);
                    }
                }
            }

            // Also heal nearby town NPCs
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (npc.active && npc.friendly && npc.townNPC && npc.life < npc.lifeMax)
                {
                    if (Vector2.Distance(source.Center, npc.Center) <= range)
                    {
                        npc.life = System.Math.Min(npc.life + allyHeal, npc.lifeMax);
                        npc.HealEffect(allyHeal);
                    }
                }
            }
        }

        private void ShareManaWithAllies(Player source, int manaAmount)
        {
            float range = 400f;
            int allyMana = (int)(manaAmount * 0.5f);
            if (allyMana < 1)
                allyMana = 1;

            for (int i = 0; i < Main.maxPlayers; i++)
            {
                Player other = Main.player[i];
                if (other.active && !other.dead && other.whoAmI != source.whoAmI)
                {
                    if (Vector2.Distance(source.Center, other.Center) <= range)
                    {
                        other.statMana = System.Math.Min(other.statMana + allyMana, other.statManaMax2);
                        other.ManaEffect(allyMana);
                    }
                }
            }
        }
    }
}
