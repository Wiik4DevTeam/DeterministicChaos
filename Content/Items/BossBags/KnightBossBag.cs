using Terraria;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;
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

namespace DeterministicChaos.Content.Items.BossBags
{
    public class KnightBossBag : ModItem
    {
        public override void SetStaticDefaults()
        {
            ItemID.Sets.BossBag[Type] = true;
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

            // Dark Fragments (30-40)
            itemLoot.Add(ItemDropRule.Common(ModContent.ItemType<DarkFragment>(), 1, 30, 40));

            // Dark Shard weapon (guaranteed)
            itemLoot.Add(ItemDropRule.Common(ModContent.ItemType<DarkShard>(), 1));

            // 2 random Roaring weapons (guaranteed)
            itemLoot.Add(new FewFromOptionsNotScaledWithLuckDropRule(2, 1, 1,
                ModContent.ItemType<RoaringSword>(),
                ModContent.ItemType<RoaringBow>(),
                ModContent.ItemType<RoaringGun>(),
                ModContent.ItemType<RoaringTome>(),
                ModContent.ItemType<RoaringSummon>(),
                ModContent.ItemType<RoaringWhip>(),
                ModContent.ItemType<RoaringYoyo>()));

            // Always drop one of Rod of Stagnation or Roaring Shield.
            itemLoot.Add(ItemDropRule.OneFromOptions(1,
                ModContent.ItemType<RodOfStagnation>(),
                ModContent.ItemType<RoaringShield>()));

            // Ring and Lens drop independently at random (25% each).
            itemLoot.Add(ItemDropRule.Common(ModContent.ItemType<RoaringRing>(), 4));
            itemLoot.Add(ItemDropRule.Common(ModContent.ItemType<RoaringLens>(), 4));
        }
    }
}
