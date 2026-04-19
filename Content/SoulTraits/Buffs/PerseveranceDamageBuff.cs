using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.SoulTraits.Buffs
{
    public class PerseveranceDamageBuff : ModBuff
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
            // This buff is no longer used, damage stacks moved to Integrity
            player.DelBuff(buffIndex);
            buffIndex--;
        }
    }
}
