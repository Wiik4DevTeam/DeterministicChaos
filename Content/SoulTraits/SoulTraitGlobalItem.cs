using System;
using System.Collections.Generic;
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

namespace DeterministicChaos.Content.SoulTraits
{
    public class SoulTraitGlobalItem : GlobalItem
    {
        // Dictionary to store investment values for specific items
        private static Dictionary<int, int> ArmorInvestmentValues = new Dictionary<int, int>();
        private static Dictionary<int, int> WeaponInvestmentValues = new Dictionary<int, int>();
        private static Dictionary<int, SoulTraitType> WeaponTraitRequirements = new Dictionary<int, SoulTraitType>();

        public override void SetStaticDefaults()
        {
            
        }

        public static void RegisterArmorInvestment(int itemID, int investmentPoints)
        {
            ArmorInvestmentValues[itemID] = investmentPoints;
        }

        public static void RegisterWeaponInvestment(int itemID, int investmentPoints, SoulTraitType requiredTrait = SoulTraitType.None)
        {
            WeaponInvestmentValues[itemID] = investmentPoints;
            if (requiredTrait != SoulTraitType.None)
                WeaponTraitRequirements[itemID] = requiredTrait;
        }

        public static int GetArmorInvestment(int itemID)
        {
            return ArmorInvestmentValues.TryGetValue(itemID, out int value) ? value : 0;
        }

        public static int GetWeaponInvestment(int itemID)
        {
            return WeaponInvestmentValues.TryGetValue(itemID, out int value) ? value : 0;
        }

        public static SoulTraitType GetWeaponTraitRequirement(int itemID)
        {
            return WeaponTraitRequirements.TryGetValue(itemID, out SoulTraitType trait) ? trait : SoulTraitType.None;
        }

        public override void UpdateEquip(Item item, Player player)
        {
            // Add armor investment when equipped
            if (ArmorInvestmentValues.TryGetValue(item.type, out int armorInvestment))
            {
                player.GetModPlayer<SoulTraitPlayer>().ArmorInvestment += armorInvestment;
            }
        }

        public override bool OnPickup(Item item, Player player)
        {
            var traitPlayer = player.GetModPlayer<SoulTraitPlayer>();

            // Perseverance 3 Investment: Health/mana pickups grant the other's effect
            if (traitPlayer.CurrentTrait == SoulTraitType.Perseverance && traitPlayer.TotalInvestment >= 3)
            {
                // Health pickup → also restore mana
                if (item.healLife > 0 && player.statMana < player.statManaMax2)
                {
                    int manaAmount = Math.Min(item.healLife, player.statManaMax2 - player.statMana);
                    player.statMana += manaAmount;
                    player.ManaEffect(manaAmount);
                }

                // Mana pickup → also restore health
                if (item.healMana > 0 && player.statLife < player.statLifeMax2)
                {
                    int healthAmount = player.GetModPlayer<PrefixEffectPlayer>().ScaleHeal(
                        Math.Min(item.healMana, player.statLifeMax2 - player.statLife));
                    player.statLife += healthAmount;
                    player.HealEffect(healthAmount);
                }
            }

            return true;
        }
    }
}
