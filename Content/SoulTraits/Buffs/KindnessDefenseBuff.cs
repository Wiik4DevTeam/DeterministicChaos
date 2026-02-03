using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.SoulTraits.Buffs
{
    public class KindnessDefenseBuff : ModBuff
    {
        public override void SetStaticDefaults()
        {
            Main.buffNoTimeDisplay[Type] = false;
            Main.debuff[Type] = false;
            Main.pvpBuff[Type] = false;
            Main.buffNoSave[Type] = true;
        }

        public override void Update(Player player, ref int buffIndex)
        {
            var traitPlayer = player.GetModPlayer<SoulTraitPlayer>();
            if (traitPlayer.KindnessDefenseTimer <= 0)
            {
                player.DelBuff(buffIndex);
                buffIndex--;
            }
            else
            {
                player.buffTime[buffIndex] = traitPlayer.KindnessDefenseTimer;
            }
        }
    }
}
