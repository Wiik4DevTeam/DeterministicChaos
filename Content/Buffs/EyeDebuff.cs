using Terraria;
using Terraria.ModLoader;

namespace DeterministicChaos.Content.Buffs
{
    // Visual debuff for the Roaring Sword eye mark system
    public class EyeDebuff : ModBuff
    {
        public override void SetStaticDefaults()
        {
            Main.debuff[Type] = true;
            Main.pvpBuff[Type] = true;
            Main.buffNoSave[Type] = true;
        }

        public override void Update(NPC npc, ref int buffIndex)
        {
            // The actual effect is handled by RoaringSwordMarkGlobalNPC
            // This buff is purely visual for the debuff icon
        }
    }
}
