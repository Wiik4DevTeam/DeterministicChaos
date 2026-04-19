using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.Buffs
{
    public class StagnationSicknessBuff : ModBuff
    {
        public override string Texture => "Terraria/Images/Buff_" + BuffID.ChaosState;

        public override void SetStaticDefaults()
        {
            Main.debuff[Type] = true;
            Main.pvpBuff[Type] = true;
            Main.buffNoSave[Type] = true;
            Main.buffNoTimeDisplay[Type] = false;
        }
    }
}