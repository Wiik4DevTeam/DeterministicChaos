using CalamityMod.Items.Materials;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.SoulTraits
{
    public class SoulEssenceRecipeSystem : ModSystem
    {
        public static RecipeGroup EvilMaterialGroup;
        public static RecipeGroup LunarFragmentGroup;

        public override void AddRecipeGroups()
        {
            // Shadow Scale OR Tissue Sample
            EvilMaterialGroup = new RecipeGroup(() => Language.GetTextValue("LegacyMisc.37") + " Evil Material",
                ItemID.ShadowScale, ItemID.TissueSample);
            RecipeGroup.RegisterGroup("DeterministicChaos:EvilMaterial", EvilMaterialGroup);

            // Any Lunar Fragment
            LunarFragmentGroup = new RecipeGroup(() => Language.GetTextValue("LegacyMisc.37") + " Lunar Fragment",
                ItemID.FragmentSolar, ItemID.FragmentVortex, ItemID.FragmentNebula, ItemID.FragmentStardust);
            RecipeGroup.RegisterGroup("DeterministicChaos:LunarFragment", LunarFragmentGroup);
        }
    }

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
            Item.maxStack = 9999;
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
            Item.maxStack = 9999;
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

        public override void AddRecipes()
        {
            // T1 + Evil Material = T2
            CreateRecipe()
                .AddIngredient<SoulEssenceT1>()
                .AddRecipeGroup("DeterministicChaos:EvilMaterial", 1)
                .AddTile(TileID.Bottles)
                .Register();
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
            Item.maxStack = 9999;
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

        public override void AddRecipes()
        {
            // T1 + Putrid Gel = T3
            CreateRecipe()
                .AddIngredient<SoulEssenceT1>()
                .AddIngredient<PurifiedGel>(1)
                .AddTile(TileID.Bottles)
                .Register();

            // T2 + Putrid Gel = T3
            CreateRecipe()
                .AddIngredient<SoulEssenceT2>()
                .AddIngredient<PurifiedGel>(1)
                .AddTile(TileID.Bottles)
                .Register();
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
            Item.maxStack = 9999;
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

        public override void AddRecipes()
        {
            // T1 + Titansblood = T4
            CreateRecipe()
                .AddIngredient<SoulEssenceT1>()
                .AddIngredient<Items.Titansblood>(1)
                .AddTile(TileID.Bottles)
                .Register();

            // T2 + Titansblood = T4
            CreateRecipe()
                .AddIngredient<SoulEssenceT2>()
                .AddIngredient<Items.Titansblood>(1)
                .AddTile(TileID.Bottles)
                .Register();

            // T3 + Titansblood = T4
            CreateRecipe()
                .AddIngredient<SoulEssenceT3>()
                .AddIngredient<Items.Titansblood>(1)
                .AddTile(TileID.Bottles)
                .Register();
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
            Item.maxStack = 9999;
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

        public override void AddRecipes()
        {
            // T1 + Lunar Fragment = T5
            CreateRecipe()
                .AddIngredient<SoulEssenceT1>()
                .AddRecipeGroup("DeterministicChaos:LunarFragment", 1)
                .AddTile(TileID.Bottles)
                .Register();

            // T2 + Lunar Fragment = T5
            CreateRecipe()
                .AddIngredient<SoulEssenceT2>()
                .AddRecipeGroup("DeterministicChaos:LunarFragment", 1)
                .AddTile(TileID.Bottles)
                .Register();

            // T3 + Lunar Fragment = T5
            CreateRecipe()
                .AddIngredient<SoulEssenceT3>()
                .AddRecipeGroup("DeterministicChaos:LunarFragment", 1)
                .AddTile(TileID.Bottles)
                .Register();

            // T4 + Lunar Fragment = T5
            CreateRecipe()
                .AddIngredient<SoulEssenceT4>()
                .AddRecipeGroup("DeterministicChaos:LunarFragment", 1)
                .AddTile(TileID.Bottles)
                .Register();
        }
    }
}
