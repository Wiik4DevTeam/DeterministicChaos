using System;
using System.Reflection;
using Terraria;
using Terraria.GameContent.ItemDropRules;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.Items
{
    public static class CalamityBossLootHelper
    {
        public static int ResolveCalamityItemId(params string[] names)
        {
            if (!ModLoader.TryGetMod("CalamityMod", out Mod calamity))
                return -1;

            foreach (string name in names)
            {
                if (calamity.TryFind<ModItem>(name, out ModItem item))
                    return item.Type;
            }

            return -1;
        }

        public static bool IsRevengeanceOrDeathActive()
        {
            if (!ModLoader.TryGetMod("CalamityMod", out Mod cal))
                return false;

            try
            {
                Type calWorldType = cal.Code?.GetType("CalamityMod.World.CalamityWorld");
                if (calWorldType == null)
                    return false;

                FieldInfo revengeField = calWorldType.GetField("revenge", BindingFlags.Public | BindingFlags.Static);
                FieldInfo deathField = calWorldType.GetField("death", BindingFlags.Public | BindingFlags.Static);

                bool revenge = revengeField != null && revengeField.GetValue(null) is bool rev && rev;
                bool death = deathField != null && deathField.GetValue(null) is bool d && d;
                return revenge || death;
            }
            catch
            {
                return false;
            }
        }
    }

    public class CalamityRevOrDeathCondition : IItemDropRuleCondition
    {
        public bool CanDrop(DropAttemptInfo info) => CalamityBossLootHelper.IsRevengeanceOrDeathActive();

        public bool CanShowItemDropInUI() => ModLoader.HasMod("CalamityMod");

        public string GetConditionDescription() => "Drops in Revengeance or Death Mode";
    }

    /// <summary>
    /// Bag drops when Expert/Master OR Calamity Revengeance/Death is active.
    /// </summary>
    public class BagDropCondition : IItemDropRuleCondition
    {
        public bool CanDrop(DropAttemptInfo info) =>
            Main.expertMode || CalamityBossLootHelper.IsRevengeanceOrDeathActive();

        public bool CanShowItemDropInUI() => true;

        public string GetConditionDescription() => "Drops in Expert, Master, Revengeance, or Death Mode";
    }

    /// <summary>
    /// Direct drops when NOT Expert/Master AND NOT Revengeance/Death (i.e. Normal only).
    /// </summary>
    public class DirectDropCondition : IItemDropRuleCondition
    {
        public bool CanDrop(DropAttemptInfo info) =>
            !Main.expertMode && !CalamityBossLootHelper.IsRevengeanceOrDeathActive();

        public bool CanShowItemDropInUI() => true;

        public string GetConditionDescription() => "Drops in Normal mode";
    }
}