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
            InitializeVanillaPotionInvestments();
        }

        private static void InitializeVanillaPotionInvestments()
        {
            // Basic potions: 1 point
            PotionInvestmentValues[ItemID.IronskinPotion] = 1;
            PotionInvestmentValues[ItemID.RegenerationPotion] = 1;
            PotionInvestmentValues[ItemID.SwiftnessPotion] = 1;
            PotionInvestmentValues[ItemID.ArcheryPotion] = 1;

            // Mid-tier potions: 2 points
            PotionInvestmentValues[ItemID.MagicPowerPotion] = 2;
            PotionInvestmentValues[ItemID.ThornsPotion] = 2;
            PotionInvestmentValues[ItemID.HeartreachPotion] = 2;

            // High-tier potions: 3 points
            PotionInvestmentValues[ItemID.EndurancePotion] = 3;
            PotionInvestmentValues[ItemID.LifeforcePotion] = 3;
            PotionInvestmentValues[ItemID.RagePotion] = 3;
            PotionInvestmentValues[ItemID.WrathPotion] = 3;
            PotionInvestmentValues[ItemID.InfernoPotion] = 3;
            PotionInvestmentValues[ItemID.TitanPotion] = 3;
            PotionInvestmentValues[ItemID.SummoningPotion] = 3;
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
            // Track potion investment when consumed
            if (PotionInvestmentValues.TryGetValue(item.type, out int investment))
            {
                var traitPlayer = player.GetModPlayer<SoulTraitPlayer>();
                // Potion investment is temporary, lasting for the buff duration
                // This is handled through a buff system instead
            }

            return base.UseItem(item, player);
        }
    }
}
