using Terraria;
using Terraria.ModLoader;
using DeterministicChaos.Content.NPCs;

namespace DeterministicChaos.Content.Buffs
{
    // Debuff applied by Forthcoming Wrath's charged strike.
    // Deals 0.5% of the NPC's current health as damage every second for 10 seconds.
    // Actual damage application is handled in TitansBaneGlobalNPC to support per-tick HP scaling.
    public class TitansBane : ModBuff
    {
        public override void SetStaticDefaults()
        {
            Main.debuff[Type] = true;
            Main.buffNoSave[Type] = true;
            Main.pvpBuff[Type] = true;
        }

        public override void Update(NPC npc, ref int buffIndex)
        {
            // Signal the GlobalNPC that this NPC is currently afflicted.
            npc.GetGlobalNPC<TitansBaneGlobalNPC>().hasTitansBane = true;
        }
    }
}
