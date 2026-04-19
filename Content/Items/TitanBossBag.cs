using System.Collections.Generic;
using Terraria;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.Items
{
    public class TitanBossBag : ModItem
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
            // Randomly drop exactly 2 Titan weapons
            itemLoot.Add(new PickTwoDropRule(
                ModContent.ItemType<ForthcomingWrath>(),
                ModContent.ItemType<LodestoneFork>(),
                ModContent.ItemType<Cascade>(),
                ModContent.ItemType<ShatteredGlass>(),
                ModContent.ItemType<Leyline>(),
                ModContent.ItemType<Appendage>()));

            // One accessory per bag
            itemLoot.Add(ItemDropRule.OneFromOptions(1, ModContent.ItemType<TitanicEmblem>()));

            // Light pet
            itemLoot.Add(ItemDropRule.Common(ModContent.ItemType<TitanStar>(), 4));

            // Titansblood (15-30)
            itemLoot.Add(ItemDropRule.Common(ModContent.ItemType<Titansblood>(), 1, 15, 30));

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
        }
    }

    /// <summary>
    /// Drops exactly two unique items, chosen randomly from the provided pool.
    /// </summary>
    internal sealed class PickTwoDropRule : IItemDropRule
    {
        private readonly int[] _items;

        public PickTwoDropRule(params int[] items) => _items = items;

        public List<IItemDropRuleChainAttempt> ChainedRules { get; } = new();

        public bool CanDrop(DropAttemptInfo info) => true;

        public ItemDropAttemptResult TryDroppingItem(DropAttemptInfo info)
        {
            if (_items.Length <= 0)
                return new ItemDropAttemptResult { State = ItemDropAttemptResultState.FailedRandomRoll };

            int first = Main.rand.Next(_items.Length);
            CommonCode.DropItem(info, _items[first], 1);

            if (_items.Length > 1)
            {
                int second = Main.rand.Next(_items.Length - 1);
                if (second >= first)
                    second++;

                CommonCode.DropItem(info, _items[second], 1);
            }

            return new ItemDropAttemptResult { State = ItemDropAttemptResultState.Success };
        }

        public void ReportDroprates(List<DropRateInfo> drops, DropRateInfoChainFeed rateInfoChainFader)
        {
            float rate = _items.Length <= 0 ? 0f : System.Math.Min(2f, _items.Length) / _items.Length;
            foreach (int id in _items)
                drops.Add(new DropRateInfo(id, 1, 1, rate, null));
        }
    }
}
