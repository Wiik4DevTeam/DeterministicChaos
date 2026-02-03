using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.SoulTraits
{
    public class SoulTraitGlobalItem : GlobalItem
    {
        // Dictionary to store investment values for specific items
        private static Dictionary<int, int> ArmorInvestmentValues = new Dictionary<int, int>();
        private static Dictionary<int, int> WeaponInvestmentValues = new Dictionary<int, int>();

        public override void SetStaticDefaults()
        {
            
        }

        public static void RegisterArmorInvestment(int itemID, int investmentPoints)
        {
            ArmorInvestmentValues[itemID] = investmentPoints;
        }

        public static void RegisterWeaponInvestment(int itemID, int investmentPoints)
        {
            WeaponInvestmentValues[itemID] = investmentPoints;
        }

        public static int GetArmorInvestment(int itemID)
        {
            return ArmorInvestmentValues.TryGetValue(itemID, out int value) ? value : 0;
        }

        public static int GetWeaponInvestment(int itemID)
        {
            return WeaponInvestmentValues.TryGetValue(itemID, out int value) ? value : 0;
        }

        public override void UpdateEquip(Item item, Player player)
        {
            // Add armor investment when equipped
            if (ArmorInvestmentValues.TryGetValue(item.type, out int armorInvestment))
            {
                player.GetModPlayer<SoulTraitPlayer>().ArmorInvestment += armorInvestment;
            }
        }

        public override void HoldItem(Item item, Player player)
        {
            // Add weapon investment when holding
            if (WeaponInvestmentValues.TryGetValue(item.type, out int weaponInvestment))
            {
                player.GetModPlayer<SoulTraitPlayer>().WeaponInvestment += weaponInvestment;
            }
        }
    }
}
