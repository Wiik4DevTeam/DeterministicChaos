using Terraria;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.SoulTraits
{
    public class SoulTraitInvestmentBuff1 : ModBuff
    {
        public override void SetStaticDefaults()
        {
            Main.debuff[Type] = false;
            Main.buffNoSave[Type] = true;
            Main.buffNoTimeDisplay[Type] = false;
        }

        public override void Update(Player player, ref int buffIndex)
        {
            player.GetModPlayer<SoulTraitPlayer>().PotionInvestment += 1;
        }
    }

    public class SoulTraitInvestmentBuff2 : ModBuff
    {
        public override void SetStaticDefaults()
        {
            Main.debuff[Type] = false;
            Main.buffNoSave[Type] = true;
            Main.buffNoTimeDisplay[Type] = false;
        }

        public override void Update(Player player, ref int buffIndex)
        {
            player.GetModPlayer<SoulTraitPlayer>().PotionInvestment += 2;
        }
    }

    public class SoulTraitInvestmentBuff3 : ModBuff
    {
        public override void SetStaticDefaults()
        {
            Main.debuff[Type] = false;
            Main.buffNoSave[Type] = true;
            Main.buffNoTimeDisplay[Type] = false;
        }

        public override void Update(Player player, ref int buffIndex)
        {
            player.GetModPlayer<SoulTraitPlayer>().PotionInvestment += 3;
        }
    }

    public class SoulTraitInvestmentBuff4 : ModBuff
    {
        public override void SetStaticDefaults()
        {
            Main.debuff[Type] = false;
            Main.buffNoSave[Type] = true;
            Main.buffNoTimeDisplay[Type] = false;
        }

        public override void Update(Player player, ref int buffIndex)
        {
            player.GetModPlayer<SoulTraitPlayer>().PotionInvestment += 4;
        }
    }

    public class SoulTraitInvestmentBuff5 : ModBuff
    {
        public override void SetStaticDefaults()
        {
            Main.debuff[Type] = false;
            Main.buffNoSave[Type] = true;
            Main.buffNoTimeDisplay[Type] = false;
        }

        public override void Update(Player player, ref int buffIndex)
        {
            player.GetModPlayer<SoulTraitPlayer>().PotionInvestment += 5;
        }
    }
}
