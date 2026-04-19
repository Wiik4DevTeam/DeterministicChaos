using System.Collections.Generic;
using System.Reflection;
using Terraria;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.Items
{
    public class JevilBossBag : ModItem
    {
        public override void SetStaticDefaults()
        {
            ItemID.Sets.BossBag[Type] = true;
            ItemID.Sets.PreHardmodeLikeBossBag[Type] = true;
            Item.ResearchUnlockCount = 3;
        }

        public override void SetDefaults()
        {
            Item.maxStack = Item.CommonMaxStack;
            Item.consumable = true;
            Item.width = 24;
            Item.height = 24;
            Item.rare = ItemRarityID.Purple;
            Item.expert = true;
        }

        public override bool CanRightClick() => true;

        public override void ModifyItemLoot(ItemLoot itemLoot)
        {
            int shadowDiamondId = CalamityBossLootHelper.ResolveCalamityItemId("ShadowDiamond");
            int laudanumId = CalamityBossLootHelper.ResolveCalamityItemId("Laudanum");
            int heartOfDarknessId = CalamityBossLootHelper.ResolveCalamityItemId("HeartOfDarkness", "HeartofDarkness");
            int stressPillsId = CalamityBossLootHelper.ResolveCalamityItemId("StressPills");

            if (shadowDiamondId > 0)
                itemLoot.Add(ItemDropRule.Common(shadowDiamondId, 1, 1, 1));

            if (laudanumId > 0)
                itemLoot.Add(ItemDropRule.ByCondition(new CalamityRevOrDeathCondition(), laudanumId, 60));

            if (heartOfDarknessId > 0)
                itemLoot.Add(ItemDropRule.ByCondition(new CalamityRevOrDeathCondition(), heartOfDarknessId, 60));

            if (stressPillsId > 0)
                itemLoot.Add(ItemDropRule.ByCondition(new CalamityRevOrDeathCondition(), stressPillsId, 60));

            int lesserLuckId = ResolveFirstItemId("LesserLuckPotion", "LuckPotionLesser");
            int normalLuckId = ResolveFirstItemId("LuckPotion");
            int greaterLuckId = ResolveFirstItemId("GreaterLuckPotion", "LuckPotionGreater");

            if (normalLuckId <= 0)
                normalLuckId = ItemID.HealingPotion;

            List<IItemDropRule> luckRules = new List<IItemDropRule>
            {
                ItemDropRule.Common(lesserLuckId > 0 ? lesserLuckId : normalLuckId, 1, 2, 4),
                ItemDropRule.Common(normalLuckId, 1, 1, 3),
                ItemDropRule.Common(greaterLuckId > 0 ? greaterLuckId : normalLuckId, 1, 1, 1)
            };
            itemLoot.Add(new OneFromRulesRule(1, luckRules.ToArray()));

            itemLoot.Add(ItemDropRule.OneFromOptions(1,
                ModContent.ItemType<DeckOfCards>(),
                ModContent.ItemType<OopsAllCrits>()));

            // 1 guaranteed weapon + 50% chance of a second
            int[] weaponPool = new int[]
            {
                ModContent.ItemType<Devilsknife>(),
                ModContent.ItemType<AceOfSpades>(),
                ModContent.ItemType<QueenOfDiamonds>(),
                ModContent.ItemType<KingOfHearts>(),
                ModContent.ItemType<JackOfClubs>()
            };
            itemLoot.Add(ItemDropRule.OneFromOptions(1, weaponPool));
            itemLoot.Add(ItemDropRule.OneFromOptions(2, weaponPool));

            itemLoot.Add(ItemDropRule.ByCondition(new HasSoulTraitCondition(), ModContent.ItemType<Soulflicker>(), 1));
        }

        private static int ResolveFirstItemId(params string[] names)
        {
            foreach (string name in names)
            {
                FieldInfo field = typeof(ItemID).GetField(name, BindingFlags.Public | BindingFlags.Static);
                if (field == null)
                    continue;

                object value = field.GetValue(null);
                if (value is int intValue)
                    return intValue;

                if (value is short shortValue)
                    return shortValue;
            }

            return -1;
        }
    }
}
