using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.SoulTraits
{
    public class SoulEssenceT1 : ModItem
    {
        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 20;
        }

        public override void SetDefaults()
        {
            Item.width = 20;
            Item.height = 26;
            Item.useStyle = ItemUseStyleID.DrinkLiquid;
            Item.useAnimation = 15;
            Item.useTime = 15;
            Item.useTurn = true;
            Item.UseSound = SoundID.Item3;
            Item.maxStack = 30;
            Item.consumable = true;
            Item.rare = ItemRarityID.Blue;
            Item.value = Item.buyPrice(silver: 50);
            Item.buffType = ModContent.BuffType<SoulTraitInvestmentBuff1>();
            Item.buffTime = 60 * 60 * 4;
        }

        public override bool CanUseItem(Player player)
        {
            RemoveLowerTierBuffs(player);
            return true;
        }

        private void RemoveLowerTierBuffs(Player player)
        {
            // No lower tier buffs to remove for T1
        }
    }

    public class SoulEssenceT2 : ModItem
    {
        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 20;
        }

        public override void SetDefaults()
        {
            Item.width = 20;
            Item.height = 26;
            Item.useStyle = ItemUseStyleID.DrinkLiquid;
            Item.useAnimation = 15;
            Item.useTime = 15;
            Item.useTurn = true;
            Item.UseSound = SoundID.Item3;
            Item.maxStack = 30;
            Item.consumable = true;
            Item.rare = ItemRarityID.Green;
            Item.value = Item.buyPrice(gold: 1);
            Item.buffType = ModContent.BuffType<SoulTraitInvestmentBuff2>();
            Item.buffTime = 60 * 60 * 4;
        }

        public override bool CanUseItem(Player player)
        {
            RemoveLowerTierBuffs(player);
            return true;
        }

        private void RemoveLowerTierBuffs(Player player)
        {
            player.ClearBuff(ModContent.BuffType<SoulTraitInvestmentBuff1>());
        }
    }

    public class SoulEssenceT3 : ModItem
    {
        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 20;
        }

        public override void SetDefaults()
        {
            Item.width = 20;
            Item.height = 26;
            Item.useStyle = ItemUseStyleID.DrinkLiquid;
            Item.useAnimation = 15;
            Item.useTime = 15;
            Item.useTurn = true;
            Item.UseSound = SoundID.Item3;
            Item.maxStack = 30;
            Item.consumable = true;
            Item.rare = ItemRarityID.Orange;
            Item.value = Item.buyPrice(gold: 2);
            Item.buffType = ModContent.BuffType<SoulTraitInvestmentBuff3>();
            Item.buffTime = 60 * 60 * 4;
        }

        public override bool CanUseItem(Player player)
        {
            RemoveLowerTierBuffs(player);
            return true;
        }

        private void RemoveLowerTierBuffs(Player player)
        {
            player.ClearBuff(ModContent.BuffType<SoulTraitInvestmentBuff1>());
            player.ClearBuff(ModContent.BuffType<SoulTraitInvestmentBuff2>());
        }
    }

    public class SoulEssenceT4 : ModItem
    {
        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 20;
        }

        public override void SetDefaults()
        {
            Item.width = 20;
            Item.height = 26;
            Item.useStyle = ItemUseStyleID.DrinkLiquid;
            Item.useAnimation = 15;
            Item.useTime = 15;
            Item.useTurn = true;
            Item.UseSound = SoundID.Item3;
            Item.maxStack = 30;
            Item.consumable = true;
            Item.rare = ItemRarityID.LightRed;
            Item.value = Item.buyPrice(gold: 5);
            Item.buffType = ModContent.BuffType<SoulTraitInvestmentBuff4>();
            Item.buffTime = 60 * 60 * 4;
        }

        public override bool CanUseItem(Player player)
        {
            RemoveLowerTierBuffs(player);
            return true;
        }

        private void RemoveLowerTierBuffs(Player player)
        {
            player.ClearBuff(ModContent.BuffType<SoulTraitInvestmentBuff1>());
            player.ClearBuff(ModContent.BuffType<SoulTraitInvestmentBuff2>());
            player.ClearBuff(ModContent.BuffType<SoulTraitInvestmentBuff3>());
        }
    }

    public class SoulEssenceT5 : ModItem
    {
        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 20;
        }

        public override void SetDefaults()
        {
            Item.width = 20;
            Item.height = 26;
            Item.useStyle = ItemUseStyleID.DrinkLiquid;
            Item.useAnimation = 15;
            Item.useTime = 15;
            Item.useTurn = true;
            Item.UseSound = SoundID.Item3;
            Item.maxStack = 30;
            Item.consumable = true;
            Item.rare = ItemRarityID.Pink;
            Item.value = Item.buyPrice(gold: 10);
            Item.buffType = ModContent.BuffType<SoulTraitInvestmentBuff5>();
            Item.buffTime = 60 * 60 * 4;
        }

        public override bool CanUseItem(Player player)
        {
            RemoveLowerTierBuffs(player);
            return true;
        }

        private void RemoveLowerTierBuffs(Player player)
        {
            player.ClearBuff(ModContent.BuffType<SoulTraitInvestmentBuff1>());
            player.ClearBuff(ModContent.BuffType<SoulTraitInvestmentBuff2>());
            player.ClearBuff(ModContent.BuffType<SoulTraitInvestmentBuff3>());
            player.ClearBuff(ModContent.BuffType<SoulTraitInvestmentBuff4>());
        }
    }
}
